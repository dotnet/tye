// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
#if !CLR2COMPATIBILITY
using System.Collections.Concurrent;
#else
using Microsoft.Build.Shared.Concurrent;
#endif
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for file IO.
    /// PERF\COVERAGE NOTE: Try to keep classes in 'shared' as granular as possible. All the methods in
    /// each class get pulled into the resulting assembly.
    /// </summary>
    internal static partial class FileUtilities
    {
        // A list of possible test runners. If the program running has one of these substrings in the name, we assume
        // this is a test harness.

        // This flag, when set, indicates that we are running tests. Initially assume it's true. It also implies that
        // the currentExecutableOverride is set to a path (that is non-null). Assume this is not initialized when we
        // have the impossible combination of runningTests = false and currentExecutableOverride = null.

        // This is the fake current executable we use in case we are running tests.

        /// <summary>
        /// The directory where MSBuild stores cache information used during the build.
        /// </summary>
        internal static string cacheDirectory = null;

        /// <summary>
        /// FOR UNIT TESTS ONLY
        /// Clear out the static variable used for the cache directory so that tests that
        /// modify it can validate their modifications.
        /// </summary>
        internal static void ClearCacheDirectoryPath()
        {
            cacheDirectory = null;
        }

        internal static readonly StringComparison PathComparison = GetIsFileSystemCaseSensitive() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// Determines whether the file system is case sensitive.
        /// Copied from https://github.com/dotnet/runtime/blob/73ba11f3015216b39cb866d9fb7d3d25e93489f2/src/libraries/Common/src/System/IO/PathInternal.CaseSensitivity.cs#L41-L59
        /// </summary>
        public static bool GetIsFileSystemCaseSensitive()
        {
            try
            {
                string pathWithUpperCase = Path.Combine(Path.GetTempPath(), "CASESENSITIVETEST" + Guid.NewGuid().ToString("N"));
                using (new FileStream(pathWithUpperCase, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 0x1000, FileOptions.DeleteOnClose))
                {
                    string lowerCased = pathWithUpperCase.ToLowerInvariant();
                    return !File.Exists(lowerCased);
                }
            }
            catch (Exception exc)
            {
                // In case something goes terribly wrong, we don't want to fail just because
                // of a casing test, so we assume case-insensitive-but-preserving.
                Debug.Fail("Casing test failed: " + exc);
                return false;
            }
        }

        /// <summary>
        /// Copied from https://github.com/dotnet/corefx/blob/056715ff70e14712419d82d51c8c50c54b9ea795/src/Common/src/System/IO/PathInternal.Windows.cs#L61
        /// MSBuild should support the union of invalid path chars across the supported OSes, so builds can have the same behaviour crossplatform: https://github.com/Microsoft/msbuild/issues/781#issuecomment-243942514
        /// </summary>
        internal static readonly char[] InvalidPathChars = new char[]
        {
            '|', '\0',
            (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
            (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
            (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
            (char)31
        };

        /// <summary>
        /// Copied from https://github.com/dotnet/corefx/blob/387cf98c410bdca8fd195b28cbe53af578698f94/src/System.Runtime.Extensions/src/System/IO/Path.Windows.cs#L18
        /// MSBuild should support the union of invalid path chars across the supported OSes, so builds can have the same behaviour crossplatform: https://github.com/Microsoft/msbuild/issues/781#issuecomment-243942514
        /// </summary>
        internal static readonly char[] InvalidFileNameChars = new char[]
        {
            '\"', '<', '>', '|', '\0',
            (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
            (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
            (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
            (char)31, ':', '*', '?', '\\', '/'
        };

        internal static readonly char[] Slashes = { '/', '\\' };

        internal static readonly string DirectorySeparatorString = Path.DirectorySeparatorChar.ToString();

        private static readonly ConcurrentDictionary<string, bool> FileExistenceCache = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);


        private static string GetFullPath(string path)
        {
            return Path.GetFullPath(path);
        }

        internal static string FixFilePath(string path)
        {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', '/');//.Replace("//", "/");
        }

        /// <summary>
        /// Determines the full path for the given file-spec.
        /// ASSUMES INPUT IS STILL ESCAPED
        /// </summary>
        /// <param name="fileSpec">The file spec to get the full path of.</param>
        /// <param name="currentDirectory"></param>
        /// <returns>full path</returns>
        internal static string GetFullPath(string fileSpec, string currentDirectory)
        {
            // Sending data out of the engine into the filesystem, so time to unescape.
            fileSpec = FixFilePath(EscapingUtilities.UnescapeAll(fileSpec));

            // Data coming back from the filesystem into the engine, so time to escape it back.
            string fullPath = EscapingUtilities.Escape(NormalizePath(Path.Combine(currentDirectory, fileSpec)));

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !EndsWithSlash(fullPath))
            {
                if (FileUtilitiesRegex.IsDrivePattern(fileSpec) ||
                    FileUtilitiesRegex.IsUncPattern(fullPath))
                {
                    // append trailing slash if Path.GetFullPath failed to (this happens with drive-specs and UNC shares)
                    fullPath += Path.DirectorySeparatorChar;
                }
            }

            return fullPath;
        }

        /// <summary>
        /// Indicates if the given file-spec ends with a slash.
        /// </summary>
        /// <param name="fileSpec">The file spec.</param>
        /// <returns>true, if file-spec has trailing slash</returns>
        internal static bool EndsWithSlash(string fileSpec)
        {
            return (fileSpec.Length > 0)
                ? IsSlash(fileSpec[fileSpec.Length - 1])
                : false;
        }

        /// <summary>
        /// Indicates if the given character is a slash.
        /// </summary>
        /// <param name="c"></param>
        /// <returns>true, if slash</returns>
        internal static bool IsSlash(char c)
        {
            return (c == Path.DirectorySeparatorChar) || (c == Path.AltDirectorySeparatorChar);
        }


        /// <summary>
        /// Gets the canonicalized full path of the provided path.
        /// Guidance for use: call this on all paths accepted through public entry
        /// points that need normalization. After that point, only verify the path
        /// is rooted, using ErrorUtilities.VerifyThrowPathRooted.
        /// ASSUMES INPUT IS ALREADY UNESCAPED.
        /// </summary>
        internal static string NormalizePath(string path)
        {
            string fullPath = GetFullPath(path);
            return FixFilePath(fullPath);
        }

        internal static bool IsSolutionFilterFilename(string filename)
        {
            return HasExtension(filename, ".slnf");
        }

        private static bool HasExtension(string filename, string extension)
        {
            if (String.IsNullOrEmpty(filename))
                return false;

            return filename.EndsWith(extension, PathComparison);
        }

        /// <summary>
        /// If on Unix, convert backslashes to slashes for strings that resemble paths.
        /// The heuristic is if something resembles paths (contains slashes) check if the
        /// first segment exists and is a directory.
        /// Use a native shared method to massage file path. If the file is adjusted,
        /// that qualifies is as a path.
        ///
        /// @baseDirectory is just passed to LooksLikeUnixFilePath, to help with the check
        /// </summary>
        internal static string MaybeAdjustFilePath(string value, string baseDirectory = "")
        {
            var comparisonType = StringComparison.Ordinal;

            // Don't bother with arrays or properties or network paths, or those that
            // have no slashes.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || string.IsNullOrEmpty(value)
                || value.StartsWith("$(", comparisonType) || value.StartsWith("@(", comparisonType)
                || value.StartsWith("\\\\", comparisonType))
            {
                return value;
            }

            // For Unix-like systems, we may want to convert backslashes to slashes
            Span<char> newValue = ConvertToUnixSlashes(value.ToCharArray());

            // Find the part of the name we want to check, that is remove quotes, if present
            bool shouldAdjust = newValue.IndexOf('/') != -1 && LooksLikeUnixFilePath(RemoveQuotes(newValue), baseDirectory);
            return shouldAdjust ? newValue.ToString() : value;
        }

        private static Span<char> ConvertToUnixSlashes(Span<char> path)
        {
            return path.IndexOf('\\') == -1 ? path : CollapseSlashes(path);
        }

        /// <summary>
        /// If on Unix, check if the string looks like a file path.
        /// The heuristic is if something resembles paths (contains slashes) check if the
        /// first segment exists and is a directory.
        ///
        /// If @baseDirectory is not null, then look for the first segment exists under
        /// that
        /// </summary>
        internal static bool LooksLikeUnixFilePath(string value, string baseDirectory = "")
            => LooksLikeUnixFilePath(value.AsSpan(), baseDirectory);

        internal static bool LooksLikeUnixFilePath(ReadOnlySpan<char> value, string baseDirectory = "")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

            // The first slash will either be at the beginning of the string or after the first directory name
            int directoryLength = value.Slice(1).IndexOf('/') + 1;
            bool shouldCheckDirectory = directoryLength != 0;

            // Check for actual files or directories under / that get missed by the above logic
            bool shouldCheckFileOrDirectory = !shouldCheckDirectory && value.Length > 0 && value[0] == '/';
            ReadOnlySpan<char> directory = value.Slice(0, directoryLength);

            return (shouldCheckDirectory && Directory.Exists(Path.Combine(baseDirectory, directory.ToString())))
                || (shouldCheckFileOrDirectory && Directory.Exists(value.ToString()));
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Span<char> CollapseSlashes(Span<char> str)
        {
            int sliceLength = 0;

            // Performs Regex.Replace(str, @"[\\/]+", "/")
            for (int i = 0; i < str.Length; i++)
            {
                bool isCurSlash = IsAnySlash(str[i]);
                bool isPrevSlash = i > 0 && IsAnySlash(str[i - 1]);

                if (!isCurSlash || !isPrevSlash)
                {
                    str[sliceLength] = str[i] == '\\' ? '/' : str[i];
                    sliceLength++;
                }
            }

            return str.Slice(0, sliceLength);
        }

        internal static bool IsAnySlash(char c) => c == '/' || c == '\\';


        private static Span<char> RemoveQuotes(Span<char> path)
        {
            int endId = path.Length - 1;
            char singleQuote = '\'';
            char doubleQuote = '\"';

            bool hasQuotes = path.Length > 2
                && ((path[0] == singleQuote && path[endId] == singleQuote)
                || (path[0] == doubleQuote && path[endId] == doubleQuote));

            return hasQuotes ? path.Slice(1, endId - 1) : path;
        }
    }

    internal static class FileUtilitiesRegex
    {
        private static readonly char _backSlash = '\\';
        private static readonly char _forwardSlash = '/';

        /// <summary>
        /// Indicates whether the specified string follows the pattern drive pattern (for example "C:", "D:").
        /// </summary>
        /// <param name="pattern">Input to check for drive pattern.</param>
        /// <returns>true if follows the drive pattern, false otherwise.</returns>
        internal static bool IsDrivePattern(string pattern)
        {
            // Format must be two characters long: "<drive letter>:"
            return pattern.Length == 2 &&
                StartsWithDrivePattern(pattern);
        }

        /// <summary>
        /// Indicates whether the specified string follows the pattern drive pattern (for example "C:/" or "C:\").
        /// </summary>
        /// <param name="pattern">Input to check for drive pattern with slash.</param>
        /// <returns>true if follows the drive pattern with slash, false otherwise.</returns>
        internal static bool IsDrivePatternWithSlash(string pattern)
        {
            return pattern.Length == 3 &&
                    StartsWithDrivePatternWithSlash(pattern);
        }

        /// <summary>
        /// Indicates whether the specified string starts with the drive pattern (for example "C:").
        /// </summary>
        /// <param name="pattern">Input to check for drive pattern.</param>
        /// <returns>true if starts with drive pattern, false otherwise.</returns>
        internal static bool StartsWithDrivePattern(string pattern)
        {
            // Format dictates a length of at least 2,
            // first character must be a letter,
            // second character must be a ":"
            return pattern.Length >= 2 &&
                ((pattern[0] >= 'A' && pattern[0] <= 'Z') || (pattern[0] >= 'a' && pattern[0] <= 'z')) &&
                pattern[1] == ':';
        }

        /// <summary>
        /// Indicates whether the specified string starts with the drive pattern (for example "C:/" or "C:\").
        /// </summary>
        /// <param name="pattern">Input to check for drive pattern.</param>
        /// <returns>true if starts with drive pattern with slash, false otherwise.</returns>
        internal static bool StartsWithDrivePatternWithSlash(string pattern)
        {
            // Format dictates a length of at least 3,
            // first character must be a letter,
            // second character must be a ":"
            // third character must be a slash.
            return pattern.Length >= 3 &&
                StartsWithDrivePattern(pattern) &&
                (pattern[2] == _backSlash || pattern[2] == _forwardSlash);
        }

        /// <summary>
        /// Indicates whether the specified file-spec comprises exactly "\\server\share" (with no trailing characters).
        /// </summary>
        /// <param name="pattern">Input to check for UNC pattern.</param>
        /// <returns>true if comprises UNC pattern.</returns>
        internal static bool IsUncPattern(string pattern)
        {
            //Return value == pattern.length means:
            //  meets minimum unc requirements
            //  pattern does not end in a '/' or '\'
            //  if a subfolder were found the value returned would be length up to that subfolder, therefore no subfolder exists
            return StartsWithUncPatternMatchLength(pattern) == pattern.Length;
        }

        /// <summary>
        /// Indicates whether the specified file-spec begins with "\\server\share".
        /// </summary>
        /// <param name="pattern">Input to check for UNC pattern.</param>
        /// <returns>true if starts with UNC pattern.</returns>
        internal static bool StartsWithUncPattern(string pattern)
        {
            //Any non -1 value returned means there was a match, therefore is begins with the pattern.
            return StartsWithUncPatternMatchLength(pattern) != -1;
        }

        /// <summary>
        /// Indicates whether the file-spec begins with a UNC pattern and how long the match is.
        /// </summary>
        /// <param name="pattern">Input to check for UNC pattern.</param>
        /// <returns>length of the match, -1 if no match.</returns>
        internal static int StartsWithUncPatternMatchLength(string pattern)
        {
            if (!MeetsUncPatternMinimumRequirements(pattern))
            {
                return -1;
            }

            bool prevCharWasSlash = true;
            bool hasShare = false;

            for (int i = 2; i < pattern.Length; i++)
            {
                //Real UNC paths should only contain backslashes. However, the previous
                // regex pattern accepted both so functionality will be retained.
                if (pattern[i] == _backSlash ||
                    pattern[i] == _forwardSlash)
                {
                    if (prevCharWasSlash)
                    {
                        //We get here in the case of an extra slash.
                        return -1;
                    }
                    else if (hasShare)
                    {
                        return i;
                    }

                    hasShare = true;
                    prevCharWasSlash = true;
                }
                else
                {
                    prevCharWasSlash = false;
                }
            }

            if (!hasShare)
            {
                //no subfolder means no unc pattern. string is something like "\\abc" in this case
                return -1;
            }

            return pattern.Length;
        }

        /// <summary>
        /// Indicates whether or not the file-spec meets the minimum requirements of a UNC pattern.
        /// </summary>
        /// <param name="pattern">Input to check for UNC pattern minimum requirements.</param>
        /// <returns>true if the UNC pattern is a minimum length of 5 and the first two characters are be a slash, false otherwise.</returns>
#if !NET35
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        internal static bool MeetsUncPatternMinimumRequirements(string pattern)
        {
            return pattern.Length >= 5 &&
                (pattern[0] == _backSlash ||
                pattern[0] == _forwardSlash) &&
                (pattern[1] == _backSlash ||
                pattern[1] == _forwardSlash);
        }
    }
}
