﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using System.Text;
using System.Globalization;
using System.Security;
using System.Text.Json;
using System.Text.RegularExpressions;

//using ErrorUtilities = Microsoft.Build.Shared.ErrorUtilities;
//using VisualStudioConstants = Microsoft.Build.Shared.VisualStudioConstants;
//using ProjectFileErrorUtilities = Microsoft.Build.Shared.ProjectFileErrorUtilities;
//using BuildEventFileInfo = Microsoft.Build.Shared.BuildEventFileInfo;
//using ResourceUtilities = Microsoft.Build.Shared.ResourceUtilities;
//using ExceptionUtilities = Microsoft.Build.Shared.ExceptionHandling;
using System.Collections.ObjectModel;
using Microsoft.Build.Shared;
using System.Runtime.InteropServices;

#pragma warning disable CS8618, CS8625, CS8601, CS8600, CS8604, CS0162, CS8603, CS0168

namespace Microsoft.Build.Construction
{
    /// <remarks>
    /// This class contains the functionality to parse a solution file and return a corresponding
    /// MSBuild project file containing the projects and dependencies defined in the solution.
    /// </remarks>
    public sealed class SolutionFile
    {
        #region Solution specific constants

        // An example of a project line looks like this:
        //  Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "ClassLibrary1", "ClassLibrary1\ClassLibrary1.csproj", "{05A5AD00-71B5-4612-AF2F-9EA9121C4111}"
        private static readonly Lazy<Regex> s_crackProjectLine = new Lazy<Regex>(
            () => new Regex
                (
                "^" // Beginning of line
                + "Project\\(\"(?<PROJECTTYPEGUID>.*)\"\\)"
                + "\\s*=\\s*" // Any amount of whitespace plus "=" plus any amount of whitespace
                + "\"(?<PROJECTNAME>.*)\""
                + "\\s*,\\s*" // Any amount of whitespace plus "," plus any amount of whitespace
                + "\"(?<RELATIVEPATH>.*)\""
                + "\\s*,\\s*" // Any amount of whitespace plus "," plus any amount of whitespace
                + "\"(?<PROJECTGUID>.*)\""
                + "$", // End-of-line
                RegexOptions.Compiled
                )
            );

        // An example of a property line looks like this:
        //      AspNetCompiler.VirtualPath = "/webprecompile"
        // Because website projects now include the target framework moniker as
        // one of their properties, <PROPERTYVALUE> may now have '=' in it. 

        private static readonly Lazy<Regex> s_crackPropertyLine = new Lazy<Regex>(
            () => new Regex
                (
                "^" // Beginning of line
                + "(?<PROPERTYNAME>[^=]*)"
                + "\\s*=\\s*" // Any amount of whitespace plus "=" plus any amount of whitespace
                + "(?<PROPERTYVALUE>.*)"
                + "$", // End-of-line
                RegexOptions.Compiled
                )
            );

        internal const int slnFileMinUpgradableVersion = 7; // Minimum version for MSBuild to give a nice message
        internal const int slnFileMinVersion = 9; // Minimum version for MSBuild to actually do anything useful
        internal const int slnFileMaxVersion = 12;

        private const string vbProjectGuid = "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}";
        private const string csProjectGuid = "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}";
        private const string cpsProjectGuid = "{13B669BE-BB05-4DDF-9536-439F39A36129}";
        private const string cpsCsProjectGuid = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
        private const string cpsVbProjectGuid = "{778DAE3C-4631-46EA-AA77-85C1314464D9}";
        private const string cpsFsProjectGuid = "{6EC3EE1D-3C4E-46DD-8F32-0CC8E7565705}";
        private const string vjProjectGuid = "{E6FDF86B-F3D1-11D4-8576-0002A516ECE8}";
        private const string vcProjectGuid = "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}";
        private const string fsProjectGuid = "{F2A71F9B-5D33-465A-A702-920D77279786}";
        private const string dbProjectGuid = "{C8D11400-126E-41CD-887F-60BD40844F9E}";
        private const string wdProjectGuid = "{2CFEAB61-6A3B-4EB8-B523-560B4BEEF521}";
        private const string synProjectGuid = "{BBD0F5D1-1CC4-42FD-BA4C-A96779C64378}";
        private const string webProjectGuid = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";
        private const string solutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
        private const string sharedProjectGuid = "{D954291E-2A0B-460D-934E-DC6B0785DB48}";

        private const char CommentStartChar = '#';
        #endregion
        #region Member data
        private string _solutionFile;
        private string _solutionFilterFile;
        private HashSet<string> _solutionFilter;
        private bool _parsingForConversionOnly;

        // The list of projects in this SLN, keyed by the project GUID.
        private Dictionary<string, ProjectInSolution> _projects;

        // The list of projects in the SLN, in order of their appearance in the SLN.
        private List<ProjectInSolution> _projectsInOrder;

        // The list of solution configurations in the solution
        private List<SolutionConfigurationInSolution> _solutionConfigurations;

        // cached default configuration name for GetDefaultConfigurationName
        private string _defaultConfigurationName;

        // cached default platform name for GetDefaultPlatformName
        private string _defaultPlatformName;

        // VisualStudionVersion specified in Dev12+ solutions
        private Version _currentVisualStudioVersion;
        private int _currentLineNumber;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        internal SolutionFile()
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// This property returns the list of warnings that were generated during solution parsing
        /// </summary>
        internal List<string> SolutionParserWarnings { get; } = new List<string>();

        /// <summary>
        /// This property returns the list of comments that were generated during the solution parsing
        /// </summary>
        internal List<string> SolutionParserComments { get; } = new List<string>();

        /// <summary>
        /// This property returns the list of error codes for warnings/errors that were generated during solution parsing. 
        /// </summary>
        internal List<string> SolutionParserErrorCodes { get; } = new List<string>();

        /// <summary>
        /// Returns the actual major version of the parsed solution file
        /// </summary>
        internal int Version { get; private set; }

        /// <summary>
        /// Returns Visual Studio major version
        /// </summary>
        internal int VisualStudioVersion
        {
            get
            {
                if (_currentVisualStudioVersion != null)
                {
                    return _currentVisualStudioVersion.Major;
                }
                else
                {
                    return Version - 1;
                }
            }
        }

        /// <summary>
        /// Returns true if the solution contains any web projects
        /// </summary>
        internal bool ContainsWebProjects { get; private set; }

        /// <summary>
        /// Returns true if the solution contains any .wdproj projects.  Used to determine
        /// whether we need to load up any projects to examine dependencies. 
        /// </summary>
        internal bool ContainsWebDeploymentProjects { get; private set; }

        /// <summary>
        /// All projects in this solution, in the order they appeared in the solution file
        /// </summary>
        public IReadOnlyList<ProjectInSolution> ProjectsInOrder => _projectsInOrder.AsReadOnly();

        /// <summary>
        /// The collection of projects in this solution, accessible by their guids as a 
        /// string in "{XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}" form
        /// </summary>
        public IReadOnlyDictionary<string, ProjectInSolution> ProjectsByGuid => new ReadOnlyDictionary<string, ProjectInSolution>(_projects);

        /// <summary>
        /// This is the read/write accessor for the solution file which we will parse.  This
        /// must be set before calling any other methods on this class.
        /// </summary>
        /// <value></value>
        internal string FullPath
        {
            get => _solutionFile;

            set
            {
                // Should already be canonicalized to a full path
                //ErrorUtilities.VerifyThrowInternalRooted(value);
                if (FileUtilities.IsSolutionFilterFilename(value))
                {
                    ParseSolutionFilter(value);
                }
                else
                {
                    _solutionFile = value;
                    _solutionFilter = null;

                    SolutionFileDirectory = Path.GetDirectoryName(_solutionFile);
                }
            }
        }

        internal string SolutionFileDirectory
        {
            get;
            // This setter is only used by the unit tests
            set;
        }

        /// <summary>
        /// For unit-testing only.
        /// </summary>
        /// <value></value>
        internal StreamReader SolutionReader { get; set; }

        /// <summary>
        /// The list of all full solution configurations (configuration + platform) in this solution
        /// </summary>
        public IReadOnlyList<SolutionConfigurationInSolution> SolutionConfigurations => _solutionConfigurations.AsReadOnly();

        #endregion

        #region Methods

        internal bool ProjectShouldBuild(string projectFile)
        {
            return _solutionFilter?.Contains(FileUtilities.FixFilePath(projectFile)) != false;
        }

        /// <summary>
        /// This method takes a path to a solution file, parses the projects and project dependencies
        /// in the solution file, and creates internal data structures representing the projects within
        /// the SLN.
        /// </summary>
        public static SolutionFile Parse(string solutionFile)
        {
            var parser = new SolutionFile { FullPath = solutionFile };
            parser.ParseSolutionFile();
            return parser;
        }

        /// <summary>
        /// Returns "true" if it's a project that's expected to be buildable, or false if it's 
        /// not (e.g. a solution folder) 
        /// </summary>
        /// <param name="project">The project in the solution</param>
        /// <returns>Whether the project is expected to be buildable</returns>
        internal static bool IsBuildableProject(ProjectInSolution project)
        {
            return project.ProjectType != SolutionProjectType.SolutionFolder && project.ProjectConfigurations.Count > 0;
        }

        /// <summary>
        /// Given a solution file, parses the header and returns the major version numbers of the solution file
        /// and the visual studio. 
        /// Throws InvalidProjectFileException if the solution header is invalid, or if the version is less than 
        /// our minimum version. 
        /// </summary>
        internal static void GetSolutionFileAndVisualStudioMajorVersions(string solutionFile, out int solutionVersion, out int visualStudioMajorVersion)
        {
            //ErrorUtilities.VerifyThrow(!String.IsNullOrEmpty(solutionFile), "null solution file passed to GetSolutionFileMajorVersion!");
            //ErrorUtilities.VerifyThrowInternalRooted(solutionFile);

            const string slnFileHeaderNoVersion = "Microsoft Visual Studio Solution File, Format Version ";
            const string slnFileVSVLinePrefix = "VisualStudioVersion";
            FileStream fileStream = null;
            StreamReader reader = null;
            bool validVersionFound = false;

            solutionVersion = 0;
            visualStudioMajorVersion = 0;

            try
            {
                // Open the file
                fileStream = File.OpenRead(solutionFile);
                reader = new StreamReader(fileStream, Encoding.GetEncoding(0)); // HIGHCHAR: If solution files have no byte-order marks, then assume ANSI rather than ASCII.

                // Read first 4 lines of the solution file. 
                // The header is expected to be in line 1 or 2
                // VisualStudioVersion is expected to be in line 3 or 4.
                for (int i = 0; i < 4; i++)
                {
                    string line = reader.ReadLine();

                    if (line == null)
                    {
                        break;
                    }

                    if (line.Trim().StartsWith(slnFileHeaderNoVersion, StringComparison.Ordinal))
                    {
                        // Found it.  Validate the version.
                        string fileVersionFromHeader = line.Substring(slnFileHeaderNoVersion.Length);

                        if (!System.Version.TryParse(fileVersionFromHeader, out Version version))
                        {
                            throw new Exception();
                            //ProjectFileErrorUtilities.ThrowInvalidProjectFile
                            //    (
                            //        "SubCategoryForSolutionParsingErrors",
                            //        new BuildEventFileInfo(solutionFile),
                            //        "SolutionParseVersionMismatchError",
                            //        slnFileMinUpgradableVersion,
                            //        slnFileMaxVersion
                            //    );
                        }

                        solutionVersion = version.Major;

                        // Validate against our min & max
                        if (solutionVersion < slnFileMinUpgradableVersion)
                        {
                            throw new Exception();
                        }

                        validVersionFound = true;
                    }
                    else if (line.Trim().StartsWith(slnFileVSVLinePrefix, StringComparison.Ordinal))
                    {
                        Version visualStudioVersion = ParseVisualStudioVersion(line);
                        if (visualStudioVersion != null)
                        {
                            visualStudioMajorVersion = visualStudioVersion.Major;
                        }
                    }
                }
            }
            finally
            {
                fileStream?.Dispose();
                reader?.Dispose();
            }

            if (validVersionFound)
            {
                return;
            }

            // Didn't find the header in lines 1-4, so the solution file is invalid.
            throw new Exception();
        }

        private void ParseSolutionFilter(string solutionFilterFile)
        {
            _solutionFilterFile = solutionFilterFile;
            try
            {
                _solutionFile = ParseSolutionFromSolutionFilter(solutionFilterFile, out JsonElement solution);
                if (!File.Exists(_solutionFile))
                {
                    throw new Exception();
                }

                SolutionFileDirectory = Path.GetDirectoryName(_solutionFile);

                _solutionFilter = new HashSet<string>(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
                foreach (JsonElement project in solution.GetProperty("projects").EnumerateArray())
                {
                    _solutionFilter.Add(FileUtilities.FixFilePath(project.GetString()));
                }
            }
            catch (Exception e) when (e is JsonException || e is KeyNotFoundException || e is InvalidOperationException)
            {
                throw;
                //ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile
                //(
                //    false, /* Just throw the exception */
                //    "SubCategoryForSolutionParsingErrors",
                //    new BuildEventFileInfo(solutionFilterFile),
                //    e,
                //    "SolutionFilterJsonParsingError",
                //    solutionFilterFile
                //);
            }
        }

        internal static string ParseSolutionFromSolutionFilter(string solutionFilterFile, out JsonElement solution)
        {
            try
            {
                // This is to align MSBuild with what VS permits in loading solution filter files. These are not in them by default but can be added manually.
                JsonDocumentOptions options = new JsonDocumentOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
                JsonDocument text = JsonDocument.Parse(File.ReadAllText(solutionFilterFile), options);
                solution = text.RootElement.GetProperty("solution");
                return FileUtilities.GetFullPath(solution.GetProperty("path").GetString(), Path.GetDirectoryName(solutionFilterFile));
            }
            catch (Exception e) when (e is JsonException || e is KeyNotFoundException || e is InvalidOperationException)
            {
                throw;
                //ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile
                //(
                //    false, /* Just throw the exception */
                //    "SubCategoryForSolutionParsingErrors",
                //    new BuildEventFileInfo(solutionFilterFile),
                //    e,
                //    "SolutionFilterJsonParsingError",
                //    solutionFilterFile
                //);
            }
            solution = new JsonElement();
            return string.Empty;
        }


        /// <summary>
        /// Adds a configuration to this solution
        /// </summary>
        internal void AddSolutionConfiguration(string configurationName, string platformName)
        {
            _solutionConfigurations.Add(new SolutionConfigurationInSolution(configurationName, platformName));
        }

        /// <summary>
        /// Reads a line from the StreamReader, trimming leading and trailing whitespace.
        /// </summary>
        /// <returns></returns>
        private string ReadLine()
        {
            string line = SolutionReader.ReadLine();
            _currentLineNumber++;

            return line?.Trim();
        }

        /// <summary>
        /// This method takes a path to a solution file, parses the projects and project dependencies
        /// in the solution file, and creates internal data structures representing the projects within
        /// the SLN.  Used for conversion, which means it allows situations that we refuse to actually build. 
        /// </summary>
        internal void ParseSolutionFileForConversion()
        {
            _parsingForConversionOnly = true;
            ParseSolutionFile();
        }

        /// <summary>
        /// This method takes a path to a solution file, parses the projects and project dependencies
        /// in the solution file, and creates internal data structures representing the projects within
        /// the SLN.
        /// </summary>
        internal void ParseSolutionFile()
        {

            FileStream fileStream = null;
            SolutionReader = null;

            try
            {
                // Open the file
                fileStream = File.OpenRead(_solutionFile);
                SolutionReader = new StreamReader(fileStream, Encoding.GetEncoding(0)); // HIGHCHAR: If solution files have no byte-order marks, then assume ANSI rather than ASCII.
                ParseSolution();
            }
            catch (Exception e)
            {
                throw;
            }
            finally
            {
                fileStream?.Dispose();
                SolutionReader?.Dispose();
            }
        }

        /// <summary>
        /// Parses the SLN file represented by the StreamReader in this.reader, and populates internal
        /// data structures based on the SLN file contents.
        /// </summary>
        internal void ParseSolution()
        {
            _projects = new Dictionary<string, ProjectInSolution>(StringComparer.OrdinalIgnoreCase);
            _projectsInOrder = new List<ProjectInSolution>();
            ContainsWebProjects = false;
            Version = 0;
            _currentLineNumber = 0;
            _solutionConfigurations = new List<SolutionConfigurationInSolution>();
            _defaultConfigurationName = null;
            _defaultPlatformName = null;

            // the raw list of project configurations in solution configurations, to be processed after it's fully read in.
            Dictionary<string, string> rawProjectConfigurationsEntries = null;

            ParseFileHeader();

            string str;
            while ((str = ReadLine()) != null)
            {
                if (str.StartsWith("Project(", StringComparison.Ordinal))
                {
                    ParseProject(str);
                }
                else if (str.StartsWith("GlobalSection(NestedProjects)", StringComparison.Ordinal))
                {
                    ParseNestedProjects();
                }
                else if (str.StartsWith("GlobalSection(SolutionConfigurationPlatforms)", StringComparison.Ordinal))
                {
                    ParseSolutionConfigurations();
                }
                else if (str.StartsWith("GlobalSection(ProjectConfigurationPlatforms)", StringComparison.Ordinal))
                {
                    rawProjectConfigurationsEntries = ParseProjectConfigurations();
                }
                else if (str.StartsWith("VisualStudioVersion", StringComparison.Ordinal))
                {
                    _currentVisualStudioVersion = ParseVisualStudioVersion(str);
                }
                else
                {
                    // No other section types to process at this point, so just ignore the line
                    // and continue.
                }
            }

            if (_solutionFilter != null)
            {
                HashSet<string> projectPaths = new HashSet<string>(_projectsInOrder.Count, RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
                foreach (ProjectInSolution project in _projectsInOrder)
                {
                    projectPaths.Add(FileUtilities.FixFilePath(project.RelativePath));
                }
                foreach (string project in _solutionFilter)
                {
                    if (!projectPaths.Contains(project))
                    {
                        throw new Exception();
                        //ProjectFileErrorUtilities.ThrowInvalidProjectFile
                        //(
                        //    "SubCategoryForSolutionParsingErrors",
                        //    new BuildEventFileInfo(FileUtilities.GetFullPath(project, Path.GetDirectoryName(_solutionFile))),
                        //    "SolutionFilterFilterContainsProjectNotInSolution",
                        //    _solutionFilterFile,
                        //    project,
                        //    _solutionFile
                        //);
                    }
                }
            }

            if (rawProjectConfigurationsEntries != null)
            {
                ProcessProjectConfigurationSection(rawProjectConfigurationsEntries);
            }

            // Cache the unique name of each project, and check that we don't have any duplicates.
            var projectsByUniqueName = new Dictionary<string, ProjectInSolution>(StringComparer.OrdinalIgnoreCase);
            var projectsByOriginalName = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ProjectInSolution proj in _projectsInOrder)
            {
                // Find the unique name for the project.  This method also caches the unique name,
                // so it doesn't have to be recomputed later.
                string uniqueName = proj.GetUniqueProjectName();

                if (proj.ProjectType == SolutionProjectType.WebProject)
                {
                    // Examine port information and determine if we need to disambiguate similarly-named projects with different ports.
                    if (Uri.TryCreate(proj.RelativePath, UriKind.Absolute, out Uri uri))
                    {
                        if (!uri.IsDefaultPort)
                        {
                            // If there are no other projects with the same name as this one, then we will keep this project's unique name, otherwise
                            // we will create a new unique name with the port added.
                            foreach (ProjectInSolution otherProj in _projectsInOrder)
                            {
                                if (ReferenceEquals(proj, otherProj))
                                {
                                    continue;
                                }

                                if (String.Equals(otherProj.ProjectName, proj.ProjectName, StringComparison.OrdinalIgnoreCase))
                                {
                                    uniqueName = $"{uniqueName}:{uri.Port}";
                                    proj.UpdateUniqueProjectName(uniqueName);
                                    break;
                                }
                            }
                        }
                    }
                }

                // Detect collision caused by unique name's normalization
                if (projectsByUniqueName.TryGetValue(uniqueName, out ProjectInSolution project))
                {
                    // Did normalization occur in the current project?
                    if (uniqueName != proj.ProjectName)
                    {
                        // Generates a new unique name
                        string tempUniqueName = $"{uniqueName}_{proj.GetProjectGuidWithoutCurlyBrackets()}";
                        proj.UpdateUniqueProjectName(tempUniqueName);
                        uniqueName = tempUniqueName;
                    }
                    // Did normalization occur in a previous project?
                    else if (uniqueName != project.ProjectName)
                    {
                        // Generates a new unique name
                        string tempUniqueName = $"{uniqueName}_{project.GetProjectGuidWithoutCurlyBrackets()}";
                        project.UpdateUniqueProjectName(tempUniqueName);

                        projectsByUniqueName.Remove(uniqueName);
                        projectsByUniqueName.Add(tempUniqueName, project);
                    }
                }

                bool uniqueNameExists = projectsByUniqueName.ContainsKey(uniqueName);

                // Add the unique name (if it does not exist) to the hash table 
                if (!uniqueNameExists)
                {
                    projectsByUniqueName.Add(uniqueName, proj);
                }

                bool didntAlreadyExist = !uniqueNameExists && projectsByOriginalName.Add(proj.GetOriginalProjectName());

                if (!didntAlreadyExist)
                {
                    throw new Exception();
                }
                //ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(
                //    didntAlreadyExist,
                //    "SubCategoryForSolutionParsingErrors",
                //    new BuildEventFileInfo(FullPath),
                //    "SolutionParseDuplicateProject",
                //    uniqueNameExists ? uniqueName : proj.ProjectName);
            }
        } // ParseSolutionFile()

        /// <summary>
        /// This method searches the first two lines of the solution file opened by the specified
        /// StreamReader for the solution file header.  An exception is thrown if it is not found.
        /// 
        /// The solution file header looks like this:
        /// 
        ///     Microsoft Visual Studio Solution File, Format Version 9.00
        /// 
        /// </summary>
        private void ParseFileHeader()
        {
            //ErrorUtilities.VerifyThrow(SolutionReader != null, "ParseFileHeader(): reader is null!");

            const string slnFileHeaderNoVersion = "Microsoft Visual Studio Solution File, Format Version ";

            // Read the file header.  This can be on either of the first two lines.
            for (int i = 1; i <= 2; i++)
            {
                string str = ReadLine();
                if (str == null)
                {
                    break;
                }

                if (str.StartsWith(slnFileHeaderNoVersion, StringComparison.Ordinal))
                {
                    // Found it.  Validate the version.
                    ValidateSolutionFileVersion(str.Substring(slnFileHeaderNoVersion.Length));
                    return;
                }
            }

            // Didn't find the header on either the first or second line, so the solution file
            // is invalid.
            throw new Exception();
            //ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(false, "SubCategoryForSolutionParsingErrors",
            //    new BuildEventFileInfo(FullPath), "SolutionParseNoHeaderError");
        }

        /// <summary>
        /// This method parses the Visual Studio version in Dev 12 solution files
        /// The version line looks like this:
        /// 
        /// VisualStudioVersion = 12.0.20311.0 VSPRO_PLATFORM
        /// 
        /// If such a line is found, the version is stored in this.currentVisualStudioVersion
        /// </summary>
        private static Version ParseVisualStudioVersion(string str)
        {
            Version currentVisualStudioVersion = null;
            char[] delimiterChars = { ' ', '=' };
            string[] words = str.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length >= 2)
            {
                string versionStr = words[1];
                if (!System.Version.TryParse(versionStr, out currentVisualStudioVersion))
                {
                    currentVisualStudioVersion = null;
                }
            }

            return currentVisualStudioVersion;
        }
        /// <summary>
        /// This method extracts the whole part of the version number from the specified line
        /// containing the solution file format header, and throws an exception if the version number
        /// is outside of the valid range.
        /// 
        /// The solution file header looks like this:
        /// 
        ///     Microsoft Visual Studio Solution File, Format Version 9.00
        /// 
        /// </summary>
        /// <param name="versionString"></param>
        private void ValidateSolutionFileVersion(string versionString)
        {
            if (!System.Version.TryParse(versionString, out Version version))
            {
                throw new Exception();
            }

            Version = version.Major;

            // Validate against our min & max

            // If the solution file version is greater than the maximum one we will create a comment rather than warn
            // as users such as blend opening a dev10 project cannot do anything about it.
            if (Version > slnFileMaxVersion)
            {
                //SolutionParserComments.Add(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("UnrecognizedSolutionComment", Version));
            }
        }

        /// <summary>
        /// 
        /// This method processes a "Project" section in the solution file opened by the specified
        /// StreamReader, and returns a populated ProjectInSolution instance, if successful.
        /// An exception is thrown if the solution file is invalid.
        ///
        /// The format of the parts of a Project section that we care about is as follows:
        ///
        ///  Project("{Project type GUID}") = "Project name", "Relative path to project file", "{Project GUID}"
        ///      ProjectSection(ProjectDependencies) = postProject
        ///          {Parent project unique name} = {Parent project unique name}
        ///          ...
        ///      EndProjectSection
        ///  EndProject
        /// 
        /// </summary>
        private void ParseProject(string firstLine)
        {
            //ErrorUtilities.VerifyThrow(!string.IsNullOrEmpty(firstLine), "ParseProject() got a null firstLine!");
            //ErrorUtilities.VerifyThrow(SolutionReader != null, "ParseProject() got a null reader!");

            var proj = new ProjectInSolution(this);

            // Extract the important information from the first line.
            ParseFirstProjectLine(firstLine, proj);

            // Search for project dependencies.  Keeping reading lines until we either 1.) reach
            // the end of the file, 2.) see "ProjectSection(ProjectDependencies)" at the beginning
            // of the line, or 3.) see "EndProject" at the beginning of the line.
            string line;
            while ((line = ReadLine()) != null)
            {
                // If we see an "EndProject", well ... that's the end of this project!
                if (line == "EndProject")
                {
                    break;
                }
                else if (line.StartsWith("ProjectSection(ProjectDependencies)", StringComparison.Ordinal))
                {
                    // We have a ProjectDependencies section.  Each subsequent line should identify
                    // a dependency.
                    line = ReadLine();
                    while ((line?.StartsWith("EndProjectSection", StringComparison.Ordinal) == false))
                    {
                        // This should be a dependency.  The GUID identifying the parent project should
                        // be both the property name and the property value.
                        Match match = s_crackPropertyLine.Value.Match(line);
                        if (!match.Success)
                        {
                            throw new Exception();
                        }

                        string referenceGuid = match.Groups["PROPERTYNAME"].Value.Trim();
                        proj.AddDependency(referenceGuid);

                        line = ReadLine();
                    }
                }
                else if (line.StartsWith("Project(", StringComparison.Ordinal))
                {
                    // The line with new project is already read and we can't go one line back - we have no choice but to recursively parse spotted project
                    ParseProject(line);

                    // We're not waiting for the EndProject for malformed project, so we carry on
                    break;
                }
            }

            // Add the project to the collection
            AddProjectToSolution(proj);
            // If the project is an etp project then parse the etp project file 
            // to get the projects contained in it.

        } // ParseProject()

        /// <summary>
        /// Adds a given project to the project collections of this class
        /// </summary>
        /// <param name="proj">proj</param>
        private void AddProjectToSolution(ProjectInSolution proj)
        {
            if (!String.IsNullOrEmpty(proj.ProjectGuid))
            {
                _projects[proj.ProjectGuid] = proj;
            }
            _projectsInOrder.Add(proj);
        }

        /// <summary>
        /// Validate relative path of a project
        /// </summary>
        /// <param name="proj">proj</param>
        private void ValidateProjectRelativePath(ProjectInSolution proj)
        {
            // Verify the relative path is not null
            if (proj.RelativePath.IndexOfAny(Path.GetInvalidPathChars()) != -1)
            {
                throw new Exception();
            }
        }

        /// <summary>
        /// Strips a single pair of leading/trailing double-quotes from a string.
        /// </summary>
        private static string TrimQuotes
            (
            string property
            )
        {
            // If the incoming string starts and ends with a double-quote, strip the double-quotes.
            if (!string.IsNullOrEmpty(property) && (property[0] == '"') && (property[property.Length - 1] == '"'))
            {
                return property.Substring(1, property.Length - 2);
            }
            else
            {
                return property;
            }
        }

        /// <summary>
        /// Parse the first line of a Project section of a solution file. This line should look like:
        ///
        ///  Project("{Project type GUID}") = "Project name", "Relative path to project file", "{Project GUID}"
        /// 
        /// </summary>
        /// <param name="firstLine"></param>
        /// <param name="proj"></param>
        internal void ParseFirstProjectLine
        (
            string firstLine,
            ProjectInSolution proj
        )
        {
            Match match = s_crackProjectLine.Value.Match(firstLine);
            if (!match.Success)
            {
                throw new Exception();
            }

            string projectTypeGuid = match.Groups["PROJECTTYPEGUID"].Value.Trim();
            proj.ProjectName = match.Groups["PROJECTNAME"].Value.Trim();
            proj.RelativePath = match.Groups["RELATIVEPATH"].Value.Trim();
            proj.ProjectGuid = match.Groups["PROJECTGUID"].Value.Trim();

            // If the project name is empty (as in some bad solutions) set it to some generated generic value.  
            // This allows us to at least generate reasonable target names etc. instead of crashing. 
            if (String.IsNullOrEmpty(proj.ProjectName))
            {
                proj.ProjectName = "EmptyProjectName." + Guid.NewGuid();
            }

            // Validate project relative path
            ValidateProjectRelativePath(proj);

            // Figure out what type of project this is.
            if ((String.Equals(projectTypeGuid, vbProjectGuid, StringComparison.OrdinalIgnoreCase)) ||
                (String.Equals(projectTypeGuid, csProjectGuid, StringComparison.OrdinalIgnoreCase)) ||
                (String.Equals(projectTypeGuid, cpsProjectGuid, StringComparison.OrdinalIgnoreCase)) ||
                (String.Equals(projectTypeGuid, cpsCsProjectGuid, StringComparison.OrdinalIgnoreCase)) ||
                (String.Equals(projectTypeGuid, cpsVbProjectGuid, StringComparison.OrdinalIgnoreCase)) ||
                (String.Equals(projectTypeGuid, cpsFsProjectGuid, StringComparison.OrdinalIgnoreCase)) ||
                (String.Equals(projectTypeGuid, fsProjectGuid, StringComparison.OrdinalIgnoreCase)) ||
                (String.Equals(projectTypeGuid, dbProjectGuid, StringComparison.OrdinalIgnoreCase)) ||
                (String.Equals(projectTypeGuid, vjProjectGuid, StringComparison.OrdinalIgnoreCase)) ||
                (String.Equals(projectTypeGuid, synProjectGuid, StringComparison.OrdinalIgnoreCase)))
            {
                proj.ProjectType = SolutionProjectType.KnownToBeMSBuildFormat;
            }
            else if (String.Equals(projectTypeGuid, sharedProjectGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.SharedProject;
            }
            else if (String.Equals(projectTypeGuid, solutionFolderGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.SolutionFolder;
            }
            // MSBuild format VC projects have the same project type guid as old style VC projects.
            // If it's not an old-style VC project, we'll assume it's MSBuild format
            else if (String.Equals(projectTypeGuid, vcProjectGuid, StringComparison.OrdinalIgnoreCase))
            {
                if (String.Equals(proj.Extension, ".vcproj", StringComparison.OrdinalIgnoreCase))
                {
                    if (!_parsingForConversionOnly)
                    {
                        throw new Exception();
                    }
                    // otherwise, we're parsing this solution file because we want the P2P information during 
                    // conversion, and it's perfectly valid for an unconverted solution file to still contain .vcprojs
                }
                else
                {
                    proj.ProjectType = SolutionProjectType.KnownToBeMSBuildFormat;
                }
            }
            else if (String.Equals(projectTypeGuid, webProjectGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.WebProject;
                ContainsWebProjects = true;
            }
            else if (String.Equals(projectTypeGuid, wdProjectGuid, StringComparison.OrdinalIgnoreCase))
            {
                proj.ProjectType = SolutionProjectType.WebDeploymentProject;
                ContainsWebDeploymentProjects = true;
            }
            else
            {
                proj.ProjectType = SolutionProjectType.Unknown;
            }
        }

        /// <summary>
        /// Read nested projects section.
        /// This is required to find a unique name for each project's target
        /// </summary>
        internal void ParseNestedProjects()
        {
            do
            {
                string str = ReadLine();
                if ((str == null) || (str == "EndGlobalSection"))
                {
                    break;
                }

                // Ignore empty line or comment
                if (String.IsNullOrWhiteSpace(str) || str[0] == CommentStartChar)
                {
                    continue;
                }

                Match match = s_crackPropertyLine.Value.Match(str);
                if (!match.Success)
                {
                    throw new Exception();
                }

                string projectGuid = match.Groups["PROPERTYNAME"].Value.Trim();
                string parentProjectGuid = match.Groups["PROPERTYVALUE"].Value.Trim();

                if (!_projects.TryGetValue(projectGuid, out ProjectInSolution proj))
                {
                    if (proj == null)
                    {
                        throw new Exception();
                    }
                }

                proj.ParentProjectGuid = parentProjectGuid;
            } while (true);
        }

        /// <summary>
        /// Read solution configuration section. 
        /// </summary>
        /// <remarks>
        /// A sample section:
        /// 
        /// GlobalSection(SolutionConfigurationPlatforms) = preSolution
        ///     Debug|Any CPU = Debug|Any CPU
        ///     Release|Any CPU = Release|Any CPU
        /// EndGlobalSection
        /// </remarks>
        internal void ParseSolutionConfigurations()
        {
            var nameValueSeparators = '=';
            var configPlatformSeparators = new[] { SolutionConfigurationInSolution.ConfigurationPlatformSeparator };

            do
            {
                string str = ReadLine();

                if ((str == null) || (str == "EndGlobalSection"))
                {
                    break;
                }

                // Ignore empty line or comment
                if (String.IsNullOrWhiteSpace(str) || str[0] == CommentStartChar)
                {
                    continue;
                }

                string[] configurationNames = str.Split(nameValueSeparators);

                // There should be exactly one '=' character, separating two names.
                if (configurationNames.Length != 2)
                {
                    throw new Exception();
                }

                string fullConfigurationName = configurationNames[0].Trim();

                //Fixing bug 555577: Solution file can have description information, in which case we ignore.
                if (String.Equals(fullConfigurationName, "DESCRIPTION", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Both names must be identical
                if (fullConfigurationName != configurationNames[1].Trim())
                {
                    throw new Exception();
                }

                string[] configurationPlatformParts = fullConfigurationName.Split(configPlatformSeparators);

                if (configurationPlatformParts.Length != 2)
                {
                    throw new Exception();
                }

                _solutionConfigurations.Add(new SolutionConfigurationInSolution(configurationPlatformParts[0], configurationPlatformParts[1]));
            } while (true);
        }

        /// <summary>
        /// Read project configurations in solution configurations section.
        /// </summary>
        /// <remarks>
        /// A sample (incomplete) section:
        /// 
        /// GlobalSection(ProjectConfigurationPlatforms) = postSolution
        /// 	{6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
        /// 	{6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Any CPU.Build.0 = Debug|Any CPU
        /// 	{6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.ActiveCfg = Release|Any CPU
        /// 	{6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Mixed Platforms.Build.0 = Release|Any CPU
        /// 	{6185CC21-BE89-448A-B3C0-D1C27112E595}.Debug|Win32.ActiveCfg = Debug|Any CPU
        /// 	{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Any CPU.ActiveCfg = Release|Win32
        /// 	{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Mixed Platforms.ActiveCfg = Release|Win32
        /// 	{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Mixed Platforms.Build.0 = Release|Win32
        /// 	{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Win32.ActiveCfg = Release|Win32
        /// 	{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Win32.Build.0 = Release|Win32
        /// EndGlobalSection
        /// </remarks>
        /// <returns>An unprocessed hashtable of entries in this section</returns>
        internal Dictionary<string, string> ParseProjectConfigurations()
        {
            var rawProjectConfigurationsEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            do
            {
                string str = ReadLine();

                if ((str == null) || (str == "EndGlobalSection"))
                {
                    break;
                }

                // Ignore empty line or comment
                if (String.IsNullOrWhiteSpace(str) || str[0] == CommentStartChar)
                {
                    continue;
                }

                string[] nameValue = str.Split('=');

                // There should be exactly one '=' character, separating the name and value. 
                if (nameValue.Length != 2)
                {
                    throw new Exception();
                }

                rawProjectConfigurationsEntries[nameValue[0].Trim()] = nameValue[1].Trim();
            } while (true);

            return rawProjectConfigurationsEntries;
        }

        /// <summary>
        /// Read the project configuration information for every project in the solution, using pre-cached 
        /// solution section data. 
        /// </summary>
        /// <param name="rawProjectConfigurationsEntries">Cached data from the project configuration section</param>
        internal void ProcessProjectConfigurationSection(Dictionary<string, string> rawProjectConfigurationsEntries)
        {
            // Instead of parsing the data line by line, we parse it project by project, constructing the 
            // entry name (e.g. "{A6F99D27-47B9-4EA4-BFC9-25157CBDC281}.Release|Any CPU.ActiveCfg") and retrieving its 
            // value from the raw data. The reason for this is that the IDE does it this way, and as the result
            // the '.' character is allowed in configuration names although it technically separates different
            // parts of the entry name string. This could lead to ambiguous results if we tried to parse 
            // the entry name instead of constructing it and looking it up. Although it's pretty unlikely that
            // this would ever be a problem, it's safer to do it the same way VS IDE does it.
            foreach (ProjectInSolution project in _projectsInOrder)
            {
                // Solution folders don't have configurations
                if (project.ProjectType != SolutionProjectType.SolutionFolder)
                {
                    foreach (SolutionConfigurationInSolution solutionConfiguration in _solutionConfigurations)
                    {
                        // The "ActiveCfg" entry defines the active project configuration in the given solution configuration
                        // This entry must be present for every possible solution configuration/project combination.
                        string entryNameActiveConfig = string.Format(CultureInfo.InvariantCulture, "{0}.{1}.ActiveCfg",
                            project.ProjectGuid, solutionConfiguration.FullName);

                        // The "Build.0" entry tells us whether to build the project configuration in the given solution configuration.
                        // Technically, it specifies a configuration name of its own which seems to be a remnant of an initial, 
                        // more flexible design of solution configurations (as well as the '.0' suffix - no higher values are ever used). 
                        // The configuration name is not used, and the whole entry means "build the project configuration" 
                        // if it's present in the solution file, and "don't build" if it's not.
                        string entryNameBuild = string.Format(CultureInfo.InvariantCulture, "{0}.{1}.Build.0",
                            project.ProjectGuid, solutionConfiguration.FullName);

                        if (rawProjectConfigurationsEntries.TryGetValue(entryNameActiveConfig, out string configurationPlatform))
                        {
                            string[] configurationPlatformParts = configurationPlatform.Split(SolutionConfigurationInSolution.ConfigurationPlatformSeparatorArray);

                            // Project configuration may not necessarily contain the platform part. Some project support only the configuration part.
                            var projectConfiguration = new ProjectConfigurationInSolution(
                                configurationPlatformParts[0],
                                (configurationPlatformParts.Length > 1) ? configurationPlatformParts[1] : string.Empty,
                                rawProjectConfigurationsEntries.ContainsKey(entryNameBuild)
                            );

                            project.SetProjectConfiguration(solutionConfiguration.FullName, projectConfiguration);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the default configuration name for this solution. Usually it's Debug, unless it's not present
        /// in which case it's the first configuration name we find.
        /// </summary>
        public string GetDefaultConfigurationName()
        {
            // Have we done this already? Return the cached name
            if (_defaultConfigurationName != null)
            {
                return _defaultConfigurationName;
            }

            _defaultConfigurationName = string.Empty;

            // Pick the Debug configuration as default if present
            foreach (SolutionConfigurationInSolution solutionConfiguration in SolutionConfigurations)
            {
                if (string.Equals(solutionConfiguration.ConfigurationName, "Debug", StringComparison.OrdinalIgnoreCase))
                {
                    _defaultConfigurationName = solutionConfiguration.ConfigurationName;
                    break;
                }
            }

            // Failing that, just pick the first configuration name as default
            if ((_defaultConfigurationName.Length == 0) && (SolutionConfigurations.Count > 0))
            {
                _defaultConfigurationName = SolutionConfigurations[0].ConfigurationName;
            }

            return _defaultConfigurationName;
        }

        /// <summary>
        /// Gets the default platform name for this solution. Usually it's Mixed Platforms, unless it's not present
        /// in which case it's the first platform name we find.
        /// </summary>
        public string GetDefaultPlatformName()
        {
            // Have we done this already? Return the cached name
            if (_defaultPlatformName != null)
            {
                return _defaultPlatformName;
            }

            _defaultPlatformName = string.Empty;

            // Pick the Mixed Platforms platform as default if present
            foreach (SolutionConfigurationInSolution solutionConfiguration in SolutionConfigurations)
            {
                if (string.Equals(solutionConfiguration.PlatformName, "Mixed Platforms", StringComparison.OrdinalIgnoreCase))
                {
                    _defaultPlatformName = solutionConfiguration.PlatformName;
                    break;
                }
                // We would like this to be chosen if Mixed platforms does not exist.
                else if (string.Equals(solutionConfiguration.PlatformName, "Any CPU", StringComparison.OrdinalIgnoreCase))
                {
                    _defaultPlatformName = solutionConfiguration.PlatformName;
                }
            }

            // Failing that, just pick the first platform name as default
            if ((_defaultPlatformName.Length == 0) && (SolutionConfigurations.Count > 0))
            {
                _defaultPlatformName = SolutionConfigurations[0].PlatformName;
            }

            return _defaultPlatformName;
        }

        /// <summary>
        /// This method takes a string representing one of the project's unique names (guid), and
        /// returns the corresponding "friendly" name for this project.
        /// </summary>
        /// <param name="projectGuid"></param>
        /// <returns></returns>
        internal string GetProjectUniqueNameByGuid(string projectGuid)
        {
            if (_projects.TryGetValue(projectGuid, out ProjectInSolution proj))
            {
                return proj.GetUniqueProjectName();
            }

            return null;
        }

        /// <summary>
        /// This method takes a string representing one of the project's unique names (guid), and
        /// returns the corresponding relative path to this project.
        /// </summary>
        /// <param name="projectGuid"></param>
        /// <returns></returns>
        internal string GetProjectRelativePathByGuid(string projectGuid)
        {
            if (_projects.TryGetValue(projectGuid, out ProjectInSolution proj))
            {
                return proj.RelativePath;
            }

            return null;
        }

        #endregion
    } // class SolutionParser
} // namespace Microsoft.Build.Construction
