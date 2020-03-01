using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;

namespace Tye
{
    internal static class CommonArguments
    {
        private static readonly string[] FileFormats = new[] { "tye.yaml", "tye.yml", "*.csproj", "*.fsproj", "*.sln" };

        public static Argument<FileInfo> Path_Optional
        {
            get
            {
                return new Argument<FileInfo>((r) => TryParsePath(r, required: false), isDefault: true)
                {
                    Arity = ArgumentArity.ZeroOrOne,
                    Description = "file or directory, can be a yaml, sln, or project file",
                    Name = "path",
                };
            }
        }

        public static Argument<FileInfo> Path_Required
        {
            get
            {
                return new Argument<FileInfo>((r) => TryParsePath(r, required: true), isDefault: true)
                {
                    Arity = ArgumentArity.ZeroOrOne,
                    Description = "file or directory, can be a yaml, sln, or project file",
                    Name = "path",
                };
            }
        }

        public static bool TryFindSupportedFile(string directoryPath, out string? filePath, out string? errorMessage)
        {
            foreach (var format in FileFormats)
            {
                var files = Directory.GetFiles(directoryPath, format);

                if (files.Length == 1)
                {
                    errorMessage = null;
                    filePath = files[0];
                    return true;
                }

                if (files.Length > 1)
                {
                    errorMessage = $"More than one matching files was found in directory '{directoryPath}'.";
                    filePath = default;
                    return false;
                }
            }

            errorMessage = $"No project project file or solution was found in directory '{directoryPath}'.";
            filePath = default;
            return false;
        }

        static FileInfo TryParsePath(ArgumentResult result, bool required)
        {
            var token = result.Tokens.Count switch
            {
                0 => ".",
                1 => result.Tokens[0].Value,
                _ => throw new InvalidOperationException("Unexpected token count."),
            };

            if (string.IsNullOrEmpty(token))
            {
                token = ".";
            }

            if (File.Exists(token))
            {
                return new FileInfo(token);
            }

            if (Directory.Exists(token))
            {
                if (TryFindSupportedFile(token, out var filePath, out var errorMessage))
                {
                    return new FileInfo(filePath);
                }
                else if (required)
                {
                    result.ErrorMessage = errorMessage;
                    return default!;
                }
                else
                {
                    return default!;
                }
            }

            result.ErrorMessage = $"The file '{token}' could not be found.";
            return default!;
        }
    }
}
