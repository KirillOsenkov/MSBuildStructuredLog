$ErrorActionPreference = 'Stop'; # stop on all errors

$packageArgs = @{
  packageName   = $env:ChocolateyPackageName
  softwareName  = 'MSBuild Structured Log Viewer'  #part or all of the Display Name as you see it in Programs and Features. It should be enough to be unique
  fileType      = 'EXE' #only one of these: MSI or EXE (ignore MSU for now)
  silentArgs   = '--uninstall -s'           # Squirrel
  validExitCodes= @(0) #please insert other valid exit codes here
}

$uninstall = Get-UninstallRegistryKey -softwareName $packageArgs.softwareName

$packageArgs.file = ($uninstall.InstallLocation + "\update.exe")

Uninstall-ChocolateyPackage @packageArgs