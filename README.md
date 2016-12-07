This NuGet package provides MsBuild support for Visual Studio projects. It uses MsBuild properties to control how NuGet packages are created and published.

# MsBuild Properties

## Build Package Properties
**RunNuGetPack** (True/False) - defaults to False. Creates a NuGet package when the project is compiled.

**IncludeBuildVersion** (True/False) - defaults to False. Toggles whether the fourth version number (Build) is included in the package version.

**UseBuildVersionAsPatch** (True/False) - defaults to False. Toggles whether the build number of the assembly is pushed into the third version number (Patch) in the package version.

**FileExclusionPattern** - defaults to _\*.CodeAnalysisLog.xml;\*.lastcodeanalysissucceeded;\*Test\*.\*_. Controls which file paths are excluded from the automatic bundling of files into the package.

**PackageExclusionPattern** - defaults to _MsBuild.NuGet.Pack_. Controls which packages are excluded from the automatic inclusion of package dependencies into the package.


## Publish Package Properties
**RunNuGetPublish** (True/False) - defaults to False. Publishes the NuGet package after it has been created.

**NuGetServer** - no default value. Identifies the NuGet server to publish to. This is passed to nuget.exe which will use nuget.org if no value is provided.

**NuGetApiKey** - no default value. Defines the ApiKey for publishing the package to the NuGet server.
