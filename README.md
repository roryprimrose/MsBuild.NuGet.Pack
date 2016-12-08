# MsBuild Properties
This NuGet package provides MsBuild support for Visual Studio projects. It uses MsBuild properties to control how NuGet packages are created and published.

## Build Package Properties
**RunNuGetPack** (True/False) - defaults to True if Project Configuration is Release, otherwise False. Creates a NuGet package when the project is compiled.

**IncludeBuildVersion** (True/False) - defaults to False. Toggles whether the fourth version number (Build) is included in the package version.

**UseBuildVersionAsPatch** (True/False) - defaults to False. Toggles whether the build number of the assembly is pushed into the third version number (Patch) in the package version.

**FileExclusionPattern** - defaults to _\*.CodeAnalysisLog.xml;\*.lastcodeanalysissucceeded;\*Test\*.\*_. Controls which file paths are excluded from the automatic bundling of files into the package.

**PackageExclusionPattern** - defaults to _MsBuild.NuGet.Pack_. Controls which packages are excluded from the automatic inclusion of package dependencies into the package.


## Publish Package Properties
**RunNuGetPublish** (True/False) - defaults to False. Publishes the NuGet package after it has been created.

**NuGetServer** - no default value. Identifies the NuGet server to publish to. This is passed to nuget.exe which will use nuget.org if no value is provided.

**NuGetApiKey** - no default value. Defines the ApiKey for publishing the package to the NuGet server.


# XML Processing Instruction
Some settings can also be controlled by inserting an XML processing instruction at the top of your nuspec file. Settings specified in this manner will override settings specified using MsBuild properties as described above. 

The processing directive should placed immediately after the xml declaration and have the form shown in the example below:

```xml
<?xml version="1.0"?>
<?MsBuild.NuGet.Pack IncludeBuildVersion='true' FileExclusionPattern='*.config'?>
<package>
  ...
</package>
```

Only the settings **IncludeBuildVersion**, **UseBuildVersionAsPatch**, **FileExclusionPattern**, and **PackageExclusionPattern** can be set this way.