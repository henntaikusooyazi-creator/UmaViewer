param(
    [Parameter(Mandatory)][string] $UnityExecutable,
    [Parameter(Mandatory)][string] $OutputPath,
    [string] $ConfirmedProducerProfile,
    [string] $ConfirmedOwnerClock,
    [string] $ConfirmedNaturalCue
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = [IO.Path]::GetFullPath($OutputPath)
if ($output.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $output -eq $repo) {
    throw 'Keep generated results outside the repository.'
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
# Unity can serialize these assets after a temporary pipeline switch even when the
# API references were restored. Retain the exact pre-test bytes, including any
# existing local edits; restore only after the dedicated Editor has exited.
$saved = @{}
foreach ($relative in @('ProjectSettings/GraphicsSettings.asset', 'ProjectSettings/QualitySettings.asset', 'ProjectSettings/ShaderGraphSettings.asset',
        'Assets/Plugins/CySpringPlugin.dll.meta')) {
    $path = Join-Path $repo $relative
    if (Test-Path -LiteralPath $path) { $saved[$path] = [IO.File]::ReadAllBytes($path) }
}
$process = $null
try {
    $arguments = @('-batchmode', '-force-d3d11', '-projectPath', ('"' + $repo + '"'),
        '-executeMethod', 'OriginalRenderingIntegrationValidation.Run',
        '-integrationOutput', ('"' + $output + '"'), '-logFile', ('"' + (Join-Path $output 'unity.log') + '"'))
    if ($ConfirmedProducerProfile) {
        $profile = (Resolve-Path -LiteralPath $ConfirmedProducerProfile).Path
        $arguments += @('-confirmedProducerProfile', ('"' + $profile + '"'))
    }
    if ($ConfirmedOwnerClock) {
        $clock = (Resolve-Path -LiteralPath $ConfirmedOwnerClock).Path
        $arguments += @('-confirmedOwnerClock', ('"' + $clock + '"'))
    }
    if ($ConfirmedNaturalCue) {
        $cue = (Resolve-Path -LiteralPath $ConfirmedNaturalCue).Path
        $arguments += @('-confirmedNaturalCue', ('"' + $cue + '"'))
    }
    $process = Start-Process -FilePath $UnityExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
}
finally {
    if ($process -and $process.HasExited) {
        foreach ($path in $saved.Keys) { [IO.File]::WriteAllBytes($path, $saved[$path]) }
    }
}
if (!$process -or !$process.HasExited) { throw 'Dedicated Editor did not complete; no running process is terminated by this wrapper.' }
if ($process.ExitCode -ne 0) { throw "Integration Editor exited $($process.ExitCode); inspect the external summary and log." }
$summary = Get-Content -Raw -LiteralPath (Join-Path $output 'integration-summary.json') | ConvertFrom-Json
if ($summary.status -ne 'ActualControlIntegrationGPUValidated_TestOnly') { throw 'Integration acceptance did not pass.' }
Write-Output "Actual Control integration PASS: $($summary.checks) checks; project settings bytes preserved."
