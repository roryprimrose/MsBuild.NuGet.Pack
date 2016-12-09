namespace MsBuild.NuGet.Pack
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Xml.Linq;
    using Microsoft.Build.Framework;

    /// <summary>
    ///     The merge nu spec task.
    /// </summary>
    public class MergeNuSpecTask : NuSpecTask
    {
        /// <summary>
        ///     Executes a task.
        /// </summary>
        /// <returns>
        ///     true if the task executed successfully; otherwise, false.
        /// </returns>
        /// <inheritdoc />
        public override bool Execute()
        {
            try
            {
                var packageConfig = Path.Combine(ProjectDirectory, "packages.config");

                var nuSpecDocument = OpenXmlDocument(NuSpecPath);

                LoadSettings(nuSpecDocument);

                MergeMetadata(nuSpecDocument, PrimaryOutputAssembly);

                if (File.Exists(packageConfig))
                {
                    MergePackageDependencies(nuSpecDocument, packageConfig);
                }

                var projectPath = Path.Combine(ProjectDirectory, ProjectFile);

                MergeReferences(nuSpecDocument, projectPath);

                MergeFile(nuSpecDocument, PrimaryOutputAssembly);

                nuSpecDocument.Save(NuSpecPath);
            }
            catch (Exception e)
            {
                Log.LogError(e.Message);

                return false;
            }

            return true;
        }

        private string DeterminePackageVersion(string assemblyPath, FileVersionInfo info)
        {
            var version = PackageVersion;

            if (string.IsNullOrWhiteSpace(version) == false)
            {
                if (IsValidVersion(version) == false)
                {
                    var message = string.Format(
                        CultureInfo.CurrentCulture,
                        "The PackageVersion '{0}' is not a valid NuGet version.",
                        PackageVersion);

                    throw new InvalidOperationException(message);
                }

                LogMessage("Using version {0} from the PackageVersion property", MessageImportance.High, version);

                return version;
            }

            var isValidProductVersion = IsValidVersion(info.ProductVersion);

            if (IncludeBuildVersion)
            {
                if (isValidProductVersion)
                {
                    version = info.ProductVersion;
                }
                else
                {
                    version = info.FileVersion;
                }
            }
            else if (UseBuildVersionAsPatch)
            {
                if (isValidProductVersion)
                {
                    version = info.ProductMajorPart + "." + info.ProductMinorPart + "." + info.ProductPrivatePart;
                }
                else
                {
                    version = info.FileMajorPart + "." + info.FileMinorPart + "." + info.FilePrivatePart;
                }
            }
            else
            {
                if (isValidProductVersion)
                {
                    version = info.ProductMajorPart + "." + info.ProductMinorPart + "." + info.ProductBuildPart;
                }
                else
                {
                    version = info.FileMajorPart + "." + info.FileMinorPart + "." + info.FileBuildPart;
                }
            }

            LogMessage("Using version {0} from {1}", MessageImportance.High, version, assemblyPath);

            return version;
        }

        /// <summary>
        ///     Gets the current user.
        /// </summary>
        /// <returns>
        ///     The <see cref="string" />.
        /// </returns>
        private static string GetCurrentUser()
        {
            var fallbackUserName = Environment.UserName;

            if (Thread.CurrentPrincipal == null)
            {
                return fallbackUserName;
            }

            if (Thread.CurrentPrincipal.Identity == null)
            {
                return fallbackUserName;
            }

            var currentUser = Thread.CurrentPrincipal.Identity.Name;

            if (string.IsNullOrWhiteSpace(currentUser))
            {
                return fallbackUserName;
            }

            return currentUser;
        }

        /// <summary>
        ///     Gets the project packages.
        /// </summary>
        /// <param name="packageConfig">
        ///     The package configuration.
        /// </param>
        /// <returns>
        ///     The <see cref="IEnumerable{T}" />.
        /// </returns>
        private static IEnumerable<XElement> GetProjectPackages(string packageConfig)
        {
            var document = OpenXmlDocument(packageConfig);
            var root = GetElement(document, "packages");

            var packages =
                root.Elements()
                    .Where(
                        x =>
                            x.Name.LocalName == "package" &&
                            (x.Attribute("developmentDependency") == null ||
                             x.Attribute("developmentDependency").Value != "true"));


            return packages;
        }

        /// <summary>
        ///     Gets a regex that can be used to check package exclusions.
        /// </summary>
        /// <param name="exclusionPatterns">
        ///     The package exclusion patterns.
        /// </param>
        /// <returns>
        ///     A <see cref="Regex" />.
        /// </returns>
        public static Regex GetPackageExclusions(string exclusionPatterns)
        {
            var regexPatterns = exclusionPatterns.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries).Select(x => Regex.Escape(x).Replace(@"\*", ".*").Replace(@"\?", "."));

            return new Regex("^(" + String.Join("|", regexPatterns) + ")$");
        }

        /// <summary>
        ///     Gets the project references.
        /// </summary>
        /// <param name="projectPath">
        ///     The project path.
        /// </param>
        /// <returns>
        ///     The <see cref="IEnumerable{T}" />.
        /// </returns>
        private static IEnumerable<XElement> GetProjectReferences(string projectPath)
        {
            var projectDocument = OpenXmlDocument(projectPath);
            var defaultNamespace = projectDocument.Root.GetDefaultNamespace();
            var references = projectDocument.Descendants(defaultNamespace + "Reference").ToList();

            foreach (var referenceElement in references)
            {
                if (referenceElement.Attribute("Include").Value.StartsWith("System.") == false)
                {
                    continue;
                }

                yield return referenceElement;
            }
        }

        /// <summary>
        ///     Gets the spec dependencies.
        /// </summary>
        /// <param name="nuSpecDocument">
        ///     The nu spec document.
        /// </param>
        /// <returns>
        ///     The <see cref="XElement" />.
        /// </returns>
        private static XElement GetSpecDependencies(XDocument nuSpecDocument)
        {
            var metadata = GetSpecMetadata(nuSpecDocument);

            var dependencies = GetOrCreateElement(metadata, "dependencies");

            return dependencies;
        }

        /// <summary>
        ///     Gets the spec references.
        /// </summary>
        /// <param name="nuSpecDocument">
        ///     The nu spec document.
        /// </param>
        /// <returns>
        ///     The <see cref="XElement" />.
        /// </returns>
        private static XElement GetSpecReferences(XDocument nuSpecDocument)
        {
            var metadata = GetSpecMetadata(nuSpecDocument);

            var frameworkAssemblies = GetOrCreateElement(metadata, "frameworkAssemblies");

            return frameworkAssemblies;
        }

        /// <summary>
        ///     Gets the target framework.
        /// </summary>
        /// <param name="targetFrameworkVersion">
        ///     The target framework version.
        /// </param>
        /// <param name="targetFrameworkProfile">
        /// </param>
        /// <returns>
        ///     The nuget framework target.
        /// </returns>
        private static string GetTargetFramework(string targetFrameworkVersion, string targetFrameworkProfile)
        {
            if (targetFrameworkVersion == "v2.0")
            {
                return "net20";
            }

            if (targetFrameworkVersion == "v3.0")
            {
                return "net30";
            }

            if (targetFrameworkVersion == "v3.5")
            {
                if (string.Equals(targetFrameworkProfile, "client", StringComparison.OrdinalIgnoreCase))
                {
                    return "net35-client";
                }

                return "net35";
            }

            if (targetFrameworkVersion == "v4.0")
            {
                if (string.Equals(targetFrameworkProfile, "client", StringComparison.OrdinalIgnoreCase))
                {
                    return "net40-client";
                }

                return "net40";
            }

            if (targetFrameworkVersion == "v4.5")
            {
                return "net45";
            }

            if (targetFrameworkVersion == "v4.5.1")
            {
                return "net451";
            }

            if (targetFrameworkVersion == "v4.5.2")
            {
                return "net452";
            }

            if (targetFrameworkVersion == "v4.6")
            {
                return "net46";
            }

            if (targetFrameworkVersion == "v4.6.1")
            {
                return "net461";
            }

            // Default to v4.0
            return "net40";
        }

        private static bool IsValidVersion(string version)
        {
            // Validates semver v1.0 http://semver.org/spec/v1.0.0.html
            return Regex.IsMatch(version, @"^\d+.\d+.\d+(-[a-zA-Z0-9-]+)?$");
        }

        /// <summary>
        ///     Merges the file.
        /// </summary>
        /// <param name="document">
        ///     The document.
        /// </param>
        /// <param name="primaryOutputAssembly">
        ///     The primary output assembly.
        /// </param>
        private void MergeFile(XDocument document, string primaryOutputAssembly)
        {
            LogMessage("Merging file reference from project output");

            var package = GetElement(document, "package");
            var files = GetOrCreateElement(package, "files");
            var defaultNamespace = document.Root.GetDefaultNamespace();
            var outputName = Path.GetFileNameWithoutExtension(primaryOutputAssembly);
            var srcValue = "**\\" + outputName + ".*";
            string targetFramework = null;
            var frameworkFolder = string.Empty;

            if (TargetSpecificFramework)
            {
                targetFramework = GetTargetFramework(TargetFrameworkVersion, TargetFrameworkProfile);
            }

            if (string.IsNullOrWhiteSpace(targetFramework) == false)
            {
                frameworkFolder = "\\" + targetFramework;
            }

            files.Add(
                new XElement(
                    defaultNamespace + "file",
                    new XAttribute("src", srcValue),
                    new XAttribute("target", "lib" + frameworkFolder),
                    new XAttribute("exclude", FileExclusionPattern)));
        }

        /// <summary>
        ///     Loads settings defined in the processing instruction in nuSpecDocument.
        /// </summary>
        /// <param name="nuSpecDocument">
        ///     The nu spec document.
        /// </param>
        private void LoadSettings(XDocument nuSpecDocument)
        {
            // Get processing instruction 
            var settingsPi = nuSpecDocument.Nodes().OfType<XProcessingInstruction>().Where(pi => pi.Target == "MsBuild.NuGet.Pack").SingleOrDefault();

            if (settingsPi == null) return;

            // Convert processing instruction to element for easy access to attributes
            XElement settingsElement;
            try
            {
                settingsElement = XElement.Parse("<" + settingsPi.Target + " " + settingsPi.Data + "/>");
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Processing instruction for MsBuild.NuGet.Task has the wrong form.", ex);
            }

            // Remove processing instruction so settings do not persist into the NuGet package
            settingsPi.Remove();

            // Walk the settings and assign them to their proper properties
            foreach (var settingAttribute in settingsElement.Attributes())
            {
                try
                {
                    switch (settingAttribute.Name.LocalName)
                    {
                        case "IncludeBuildVersion":
                            IncludeBuildVersion = (bool)settingAttribute;
                            break;
                        case "UseBuildVersionAsPatch":
                            UseBuildVersionAsPatch = (bool)settingAttribute;
                            break;
                        case "FileExclusionPattern":
                            FileExclusionPattern = (string)settingAttribute;
                            break;
                        case "PackageExclusionPattern":
                            PackageExclusionPattern = (string)settingAttribute;
                            break;
                    }
                }
                catch(Exception ex)
                {
                    //Do Nothing: just ignore the setting if the data was invalid
                }
            }
        }

        /// <summary>
        ///     Merges the version.
        /// </summary>
        /// <param name="nuSpecDocument">
        ///     The nu spec document.
        /// </param>
        /// <param name="assemblyPath">
        ///     The assembly path.
        /// </param>
        private void MergeMetadata(XDocument nuSpecDocument, string assemblyPath)
        {
            LogMessage("Merging metadata from " + assemblyPath);

            var info = FileVersionInfo.GetVersionInfo(assemblyPath);
            var metadata = GetSpecMetadata(nuSpecDocument);

            SetElementValue(metadata, "title", info.ProductName);

            var version = DeterminePackageVersion(assemblyPath, info);

            SetElementValue(metadata, "version", version);
            SetElementValueIfEmpty(metadata, "summary", info.Comments);
            SetElementValueIfEmpty(metadata, "description", info.FileDescription);

            var currentUser = GetCurrentUser();

            SetElementValueIfEmpty(metadata, "authors", currentUser);
            SetElementValueIfEmpty(metadata, "owners", currentUser);
        }

        /// <summary>
        ///     Merges the package dependencies.
        /// </summary>
        /// <param name="nuSpecDocument">
        ///     The nu spec document.
        /// </param>
        /// <param name="packageConfig">
        ///     The package configuration.
        /// </param>
        private void MergePackageDependencies(XDocument nuSpecDocument, string packageConfig)
        {
            LogMessage("Merging package dependencies from " + packageConfig);

            var specDependencies = GetSpecDependencies(nuSpecDocument);
            var packageDependencies = GetProjectPackages(packageConfig);
            var defaultNamespace = nuSpecDocument.Root.GetDefaultNamespace();

            var packageExclusions = GetPackageExclusions(PackageExclusionPattern);

            var currentSpecDependencies =
                specDependencies.Elements().Where(x => x.Name.LocalName == "dependency").ToList();

            foreach (var packageDependency in packageDependencies)
            {
                var id = packageDependency.Attribute("id").Value;

                // Check the package ID against the exclusion list
                if (packageExclusions.IsMatch(id)) continue; // Skip to the next package

                var version = packageDependency.Attribute("version").Value;

                var specDependency = currentSpecDependencies.SingleOrDefault(x => x.Attribute("id").Value == id);

                if (specDependency == null)
                {
                    // We need to add the package dependency into the nuspec file
                    specDependencies.Add(
                        new XElement(
                            defaultNamespace + "dependency",
                            new XAttribute("id", id),
                            new XAttribute("version", version)));
                }
                else
                {
                    // We need to update the version of the spec
                    specDependency.SetAttributeValue("version", version);
                }
            }
        }

        /// <summary>
        ///     Merges the references.
        /// </summary>
        /// <param name="nuSpecDocument">
        ///     The nu spec document.
        /// </param>
        /// <param name="projectPath">
        ///     The project path.
        /// </param>
        private void MergeReferences(XDocument nuSpecDocument, string projectPath)
        {
            LogMessage("Merging system references from the project");

            var specReferences = GetSpecReferences(nuSpecDocument);
            var references = GetProjectReferences(projectPath);
            var defaultNamespace = nuSpecDocument.Root.GetDefaultNamespace();
            const string TargetFramework = "net";

            var currentSpecDependencies =
                specReferences.Elements().Where(x => x.Name.LocalName == "frameworkAssembly").ToList();

            foreach (var reference in references)
            {
                var include = reference.Attribute("Include").Value;

                var specAssembly =
                    currentSpecDependencies.SingleOrDefault(x => x.Attribute("assemblyName").Value == include);

                if (specAssembly == null)
                {
                    // We need to add the project reference into the nuspec file
                    specReferences.Add(
                        new XElement(
                            defaultNamespace + "frameworkAssembly",
                            new XAttribute("assemblyName", include),
                            new XAttribute("targetFramework", TargetFramework)));
                }
                else
                {
                    // We need to update the assembly reference in the spec
                    specAssembly.SetAttributeValue("assemblyName", include);
                    specAssembly.SetAttributeValue("targetFramework", TargetFramework);
                }
            }
        }

        /// <summary>
        ///     Sets the element value.
        /// </summary>
        /// <param name="parent">
        ///     The parent.
        /// </param>
        /// <param name="elementName">
        ///     Name of the element.
        /// </param>
        /// <param name="value">
        ///     The value.
        /// </param>
        private static void SetElementValue(XElement parent, string elementName, string value)
        {
            var element = GetOrCreateElement(parent, elementName);

            element.Value = value;
        }

        /// <summary>
        ///     Sets the element value if empty.
        /// </summary>
        /// <param name="parent">
        ///     The parent.
        /// </param>
        /// <param name="elementName">
        ///     Name of the element.
        /// </param>
        /// <param name="value">
        ///     The value.
        /// </param>
        private static void SetElementValueIfEmpty(XElement parent, string elementName, string value)
        {
            var element = GetOrCreateElement(parent, elementName);

            if (string.IsNullOrWhiteSpace(element.Value))
            {
                element.Value = value;
            }
        }

        /// <summary>
        ///     Gets or sets the file exclusion pattern.
        /// </summary>
        /// <value>
        ///     The file exclusion pattern.
        /// </value>
        [Required]
        public string FileExclusionPattern
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the package exclusion pattern.
        /// </summary>
        /// <value>
        ///     The package exclusion pattern.
        /// </value>
        [Required]
        public string PackageExclusionPattern
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets a value indicating whether the package should include the build number.
        /// </summary>
        /// <value>
        ///     <c>true</c> if the package should include the build number; otherwise, <c>false</c>.
        /// </value>
        [Required]
        public bool IncludeBuildVersion
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the nuspec path.
        /// </summary>
        /// <value>
        ///     The nuspec path.
        /// </value>
        [Required]
        public string NuSpecPath
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the package version.
        /// </summary>
        /// <value>
        ///     The package version.
        /// </value>
        public string PackageVersion
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the primary output assembly.
        /// </summary>
        /// <value>
        ///     The primary output assembly.
        /// </value>
        [Required]
        public string PrimaryOutputAssembly
        {
            get;
            set;
        }

        /// <summary>
        ///     The projects root directory; set to <code>$(MSBuildProjectDirectory)</code> by default.
        /// </summary>
        [Required]
        public string ProjectDirectory
        {
            get;
            set;
        }

        /// <summary>
        ///     The name of the project; by default will be set to $(MSBuildProjectName).
        /// </summary>
        [Required]
        public string ProjectFile
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the target framework profile.
        /// </summary>
        /// <value>
        ///     The target framework profile.
        /// </value>
        public string TargetFrameworkProfile
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the target framework version.
        /// </summary>
        /// <value>
        ///     The target framework version.
        /// </value>
        [Required]
        public string TargetFrameworkVersion
        {
            get;
            set;
        }

        /// <summary>
        ///     Determines whether the NuGet package will target the framework version of the project.
        /// </summary>
        public bool TargetSpecificFramework
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets a value indicating whether the assembly build version should be used as the semver patch version.
        /// </summary>
        /// <value>
        ///     <c>true</c> if the assembly build version should be used as the semver patch version; otherwise, <c>false</c>.
        /// </value>
        [Required]
        public bool UseBuildVersionAsPatch
        {
            get;
            set;
        }
    }
}