$ErrorActionPreference = 'Stop'; # stop on all errors
$toolsDir   = "$(Split-Path -parent $MyInvocation.MyCommand.Definition)"
$fileLocation = Join-Path $toolsDir 'MSBuildStructuredLogSetup.exe'

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  unzipLocation = $toolsDir
  fileType      = 'EXE'
  file         = $fileLocation

  softwareName  = 'MSBuild Structured Log Viewer'

  validExitCodes= @(0)
  silentArgs   = '-s'           # Squirrel
}

Install-ChocolateyInstallPackage @packageArgs