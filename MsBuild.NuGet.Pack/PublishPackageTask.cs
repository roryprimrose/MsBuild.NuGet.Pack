namespace MsBuild.NuGet.Pack
{
    using System.Diagnostics;
    using System.IO;
    using Microsoft.Build.Framework;

    /// <summary>
    ///     The <see cref="PublishPackageTask" />
    ///     class is used to publish a NuGet package to a NuGet server.
    /// </summary>
    public class PublishPackageTask : NuSpecTask
    {
        /// <inheritdoc />
        public override bool Execute()
        {
            var arguments = BuildArguments();

            var processInfo = new ProcessStartInfo(NuGetPath)
            {
                Arguments = arguments, 
                CreateNoWindow = true, 
                RedirectStandardError = true, 
                RedirectStandardOutput = true, 
                UseShellExecute = false, 
                WindowStyle = ProcessWindowStyle.Hidden, 
                WorkingDirectory = OutDir
            };

            LogMessage("Publishing NuGet package using " + "'" + processInfo.FileName + "'");
            LogMessage("Publishing NuGet package using " + "'" + processInfo.FileName + " " + arguments + "'", MessageImportance.Low);

            var process = Process.Start(processInfo);

            var completed = process.WaitForExit(30000);

            if (completed == false)
            {
                LogError("Timeout, publishing the NuGet package took longer than 30 seconds.");
            }

            using (var reader = process.StandardOutput)
            {
                var output = reader.ReadToEnd();

                LogMessage(output);
            }

            using (var errorReader = process.StandardError)
            {
                var error = errorReader.ReadToEnd();

                if (string.IsNullOrWhiteSpace(error) == false)
                {
                    LogError(error);

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     The build arguments.
        /// </summary>
        /// <returns>
        ///     The <see cref="string" />.
        /// </returns>
        private string BuildArguments()
        {
            var arguments = "push ";
            var packagePath = DeterminePackagePath();

            LogMessage("Publishing package '" + packagePath + "'");

            arguments += "\"" + packagePath + "\" ";

            if (string.IsNullOrWhiteSpace(ApiKey) == false)
            {
                arguments += "-ApiKey \"" + ApiKey + "\" ";
            }

            if (string.IsNullOrWhiteSpace(Server) == false)
            {
                arguments += "-Source \"" + Server + "\" ";
            }

            arguments += "-NonInteractive ";

            return arguments;
        }

        /// <summary>
        ///     Determines the package path.
        /// </summary>
        /// <returns>The path of the NuGet package..</returns>
        private string DeterminePackagePath()
        {
            var specName = Path.GetFileNameWithoutExtension(NuSpecPath);
            var version = GetSpecVersion();
            var packageName = specName + "." + version + ".nupkg";
            var packagePath = Path.Combine(OutDir, packageName);

            return packagePath;
        }

        /// <summary>
        ///     Gets the NuSpec version.
        /// </summary>
        /// <returns>The package version.</returns>
        private string GetSpecVersion()
        {
            // Open the nuspec file and extract the version
            var spec = OpenXmlDocument(NuSpecPath);
            var metadata = GetSpecMetadata(spec);
            var versionElement = GetElement(metadata, "version");

            return versionElement.Value;
        }

        /// <summary>
        /// The log message.
        /// </summary>
        /// <param name="message">
        /// The message.
        /// </param>
        private void LogError(string message)
        {
            if (BuildEngine == null)
            {
                return;
            }

            var nuspecName = Path.GetFileName(NuSpecPath);
            var path = Path.Combine(OutDir, nuspecName);

            BuildEngine.LogErrorEvent(
                new BuildErrorEventArgs(
                    "BuildNuSpecTask", 
                    "Failure", 
                    path, 
                    0, 
                    0, 
                    0, 
                    0, 
                    message, 
                    "BuildNuSpecTask", 
                    "BuildNuSpecTask"));
        }

        /// <summary>
        ///     Gets or sets the API key.
        /// </summary>
        /// <value>The API key.</value>
        public string ApiKey
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the nuget path.
        /// </summary>
        /// <value>
        ///     The nuget path.
        /// </value>
        [Required]
        public string NuGetPath
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
        ///     The directory in which the built files were written to.
        /// </summary>
        [Required]
        public string OutDir
        {
            get;
            set;
        }

        /// <summary>
        ///     Gets or sets the server.
        /// </summary>
        /// <value>The server.</value>
        [Required]
        public string Server
        {
            get;
            set;
        }
    }
}