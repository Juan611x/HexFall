<#
.SYNOPSIS
  Headless WebGL build of the HexFall Unity project.

.DESCRIPTION
  Runs HexFall.Editor.Build.WebGLBuilder.BuildFromCommandLine in batch mode.
  The Unity editor must be closed for this project. Set UNITY_EDITOR to override
  the editor path (default: Unity Hub install matching ProjectVersion.txt).

  NOTE: a batch build dirties some tracked settings files (URP assets,
  ProjectSettings.asset) and creates game/Data. The script does not restore them;
  see docs/BUILD.md for the restore command.

.PARAMETER OutputPath
  Build output directory, relative to the Unity project. Default: Builds/WebGL.
#>
param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'game'

$versionLine = Select-String -Path (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion:\s*(\S+)'
if (-not $versionLine) { throw 'Could not read m_EditorVersion from ProjectVersion.txt.' }
$version = $versionLine.Matches[0].Groups[1].Value

if ($env:UNITY_EDITOR) {
    $unity = $env:UNITY_EDITOR
} else {
    $unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}
if (-not (Test-Path $unity)) {
    throw "Unity $version not found at '$unity'. Install it (with the WebGL module) or set UNITY_EDITOR."
}

$lockfile = Join-Path $project 'Temp/UnityLockfile'
if ((Test-Path $lockfile) -and (Get-Process -Name 'Unity' -ErrorAction SilentlyContinue)) {
    throw 'The project appears to be open in the Unity editor. Close it and retry.'
}

$buildsDir = Join-Path $project 'Builds'
New-Item -ItemType Directory -Force -Path $buildsDir | Out-Null
$log = Join-Path $buildsDir 'webgl_build.log'

$unityArgs = @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', $project,
    '-executeMethod', 'HexFall.Editor.Build.WebGLBuilder.BuildFromCommandLine',
    '-logFile', $log
)
if ($OutputPath) { $unityArgs += @('-buildOutput', $OutputPath) }

Write-Host "Building WebGL with Unity $version (log: $log) ..."
$process = Start-Process -FilePath $unity -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
$exitCode = $process.ExitCode

if (Test-Path $log) {
    Select-String -Path $log -Pattern 'HEXFALL_BUILD_RESULT' | Select-Object -Last 1 | ForEach-Object { Write-Host $_.Line }
}
exit $exitCode
