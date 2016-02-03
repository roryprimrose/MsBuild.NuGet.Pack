namespace MsBuild.NuGet.Pack.Tests.IntegrationTests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    ///     The <see cref="BuildNuSpecTaskTests" />
    ///     class tests the <see cref="BuildNuSpecTask" /> class.
    /// </summary>
    [TestClass]
    public class BuildNuSpecTaskTests
    {
        /// <summary>
        /// The nuget path.
        /// </summary>
        private static readonly string _nugetPath = DetermineNuGetPath();

        /// <summary>
        ///     Runs a test for execute throws exception when nuget returns error.
        /// </summary>
        [TestMethod]
        public void ExecuteThrowsExceptionWhenNuGetReturnsErrorTest()
        {
            var specPath = Path.Combine(TestContext.DeploymentDirectory, "test.nuspec");
            const string specData = @"<?xml version=""1.0""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>$($project.Name)</id>
    <authors></authors>
    <owners></owners>

	<!-- Ignore, this will get updated at compile time -->
    <version>1.0.0.0</version>

    <title>$($project.Name)</title>
    <summary></summary>
    <description></description>
    <tags></tags>

	<!-- Set this to false if you don't need to support a license -->
    <requireLicenseAcceptance>true</requireLicenseAcceptance>
	
    <dependencies>
      <!-- Ignore, all referenced NuGet packages will be added here at compile time -->
    </dependencies>

    <frameworkAssemblies>
      <!-- This will get populated at compile time with any System.* references in the project -->
	  <!-- You will need to manually add any non-System.* assemblies here -->
    </frameworkAssemblies>
  </metadata>
  <files>
	<!-- This will automatically add the binary output of this project at compile time.
	It will inject a file element like the following:		
    <file src=""**\$($project.Name).*"" target=""lib"" target=""[DeterminedByProject"" exclude=""$($project.Name).*.CodeAnalysisLog.xml;$($project.Name).*.lastcodeanalysissucceeded"" />
	-->

	<!-- Add any additional files here manually -->
  </files>
</package>
";

            File.WriteAllText(specPath, specData);

            var target = new BuildNuSpecTask
            {
                NuGetPath = _nugetPath, 
                NuSpecPath = specPath, 
                OutDir = TestContext.DeploymentDirectory
            };
            try
            {
                target.Execute();

                Assert.Fail("InvalidOperationException was expected.");
            }
            catch (InvalidOperationException ex)
            {
                Trace.WriteLine(ex);
            }
        }

        #region Static Helper Methods

        /// <summary>
        /// Determines the nu get path.
        /// </summary>
        /// <returns>
        /// The <see cref="string"/>.
        /// </returns>
        private static string DetermineNuGetPath()
        {
            var location = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(location);
            var toolsPath = Path.Combine(directory, "tools");

            return Path.Combine(toolsPath, "nuget.exe");
        }

        #endregion

        /// <summary>
        ///     Gets or sets the test context.
        /// </summary>
        /// <value>
        ///     The test context.
        /// </value>
        public TestContext TestContext
        {
            get;
            set;
        }
    }
}