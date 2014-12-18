param($installPath, $toolsPath, $package, $project)
    # This is the MSBuild targets file to add
    $targetsFile = [System.IO.Path]::Combine($toolsPath, $package.Id + '.targets')
 
    # Need to load MSBuild assembly if it's not loaded yet.
    Add-Type -AssemblyName 'Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a'

    # Grab the loaded MSBuild project for the project
    $msbuild = [Microsoft.Build.Evaluation.ProjectCollection]::GlobalProjectCollection.GetLoadedProjects($project.FullName) | Select-Object -First 1
 
    # Make the path to the targets file relative.
    $projectUri = new-object Uri($project.FullName, [System.UriKind]::Absolute)
    $targetUri = new-object Uri($targetsFile, [System.UriKind]::Absolute)
    $relativePath = [System.Uri]::UnescapeDataString($projectUri.MakeRelativeUri($targetUri).ToString()).Replace([System.IO.Path]::AltDirectorySeparatorChar, [System.IO.Path]::DirectorySeparatorChar)
 
    # Add the import with a condition, to allow the project to load without the targets present.
    $import = $msbuild.Xml.AddImport($relativePath)
    $import.Condition = "Exists('$relativePath')"

    # Add a target to fail the build when our targets are not imported
    $target = $msbuild.Xml.AddTarget("EnsureMsBuildNuGetPackImported")
    $target.BeforeTargets = "BeforeBuild"
    $target.Condition = "'`$(MsBuildNuGetPackImported)' == ''"

    # if the targets don't exist at the time the target runs, package restore didn't run
    $errorTask = $target.AddTask("Error")
    $errorTask.Condition = "!Exists('$relativePath') And ('`$(RunNuGetPack)' != '' And `$(RunNuGetPack))"
    $errorTask.SetParameter("Text", "You are trying to build with MsBuild.NuGet.Pack, but the MsBuild.NuGet.Pack.targets file is not available on this computer. This is probably because the MsBuild.NuGet.Pack package has not been committed to source control, or NuGet Package Restore is not enabled. Please enable NuGet Package Restore to download them. For more information, see http://go.microsoft.com/fwlink/?LinkID=317567.");
    $errorTask.SetParameter("HelpKeyword", "BCLBUILD2001");

    # if the targets exist at the time the target runs, package restore ran but the build didn't import the targets.
    $errorTask = $target.AddTask("Error")
    $errorTask.Condition = "Exists('$relativePath') And ('`$(RunNuGetPack)' != '' And `$(RunNuGetPack))"
    $errorTask.SetParameter("Text", "MsBuild.NuGet.Pack cannot be run because NuGet packages were restored prior to the build running, and the targets file was unavailable when the build started. Please build the project again to include these packages in the build. You may also need to make sure that your build server does not delete packages prior to each build. For more information, see http://go.microsoft.com/fwlink/?LinkID=317568.");
    $errorTask.SetParameter("HelpKeyword", "BCLBUILD2002");
	
	$projectFolder = $project.Properties.Item("FullPath").Value

	$deleteMeFile = "MsBuild.NuGet.Pack.DeleteMe.txt"

	$project.ProjectItems | ForEach { if ($_.Name -eq $deleteMeFile) { $_.Remove() } }
	 $projectPath = Split-Path $project.FullName -Parent
	Remove-Item "$($projectFolder)$($deleteMeFile)"

	$nuspecs = $project.ProjectItems | Where-Object { $_.Name -Like "*.nuspec" }

	if ($nuspecs.Count -eq 0)
	{
		$nuspecName = "$($project.Name).nuspec"
		$nuspecPath = "$($projectFolder)$nuspecName"

		if(!(Test-Path -Path $nuspecPath))
		{
			$nuspecData = @"
<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>$($project.Name)</id>

	<!-- When empty, this will be set at compile time to the [assembly:AssemblyProduct("")] attribute value -->
    <title></title>

	<!-- When empty, these will be set at compile time to either Thread.CurrentPrincipal.Identity.Name or Environment.UserName -->
    <authors></authors>
    <owners></owners>

	<!-- Ignore, this will get updated at compile time -->
    <version>1.0.0</version>

	<!-- When empty, this will be set at compile time to the [assembly:AssemblyDescription("")] attribute value -->	
    <summary></summary>
	
	<!-- When empty, this will be set at compile time to the [assembly:AssemblyTitle("")] attribute value -->
    <description></description>
    
	<!-- Set this to false if you don't need to support a license -->
    <requireLicenseAcceptance>true</requireLicenseAcceptance>

	<releaseNotes></releaseNotes>
	<tags></tags>
	
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
    <file src="**\$($project.Name).*" target="lib/[targetFramework]" exclude="$($project.Name).*.CodeAnalysisLog.xml;$($project.Name).*.lastcodeanalysissucceeded;*Test*.*" />
	The targetFramework value will be determined by the target framework of the project
	-->

	<!-- Add any additional files here manually -->
  </files>
</package>
"@

			Write-Host "Creating $nuspecName $nuspecPath"
			Add-Content $nuspecPath $nuspecData
			$project.ProjectItems.AddFromFile($nuspecPath)
			$project.ProjectItems.Item($nuspecName).Properties.Item("CopyToOutputDirectory").Value = 2
		}
	}
	else
	{
		ForEach ($nuspec in $nuspecs) 
		{
			$nuspecName = $nuspec.Name
			Write-Host "Setting $nuspecName to Copy if newer on compile" 
			$nuspec.Properties.Item("CopyToOutputDirectory").Value = 2

			# $project.Properties.Item("NuSpecFileName").Value = $nuspecName
		} 
	}

    $project.Save()