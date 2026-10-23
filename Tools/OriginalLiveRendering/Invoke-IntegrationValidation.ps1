param(
    [Parameter(Mandatory)][string] $UnityExecutable,
    [Parameter(Mandatory)][string] $OutputPath,
    [string] $ConfirmedProducerProfile,
    [string] $ConfirmedOwnerClock,
    [string] $ConfirmedNaturalCue,
    [string] $ConfirmedNaturalFade,
    [string] $ObservedMaskAlpha,
    [switch] $PostProcessOnly,
    [switch] $NaturalPostProcessOnly,
    [switch] $PostProcessParametersOnly
)
$ErrorActionPreference = 'Stop'
$postModes = @(@($PostProcessOnly, $NaturalPostProcessOnly, $PostProcessParametersOnly) | Where-Object { $_ })
if ($postModes.Count -gt 1 -or ($postModes.Count -gt 0 -and ($ConfirmedProducerProfile -or $ConfirmedOwnerClock -or $ConfirmedNaturalCue -or $ConfirmedNaturalFade -or $ObservedMaskAlpha))) {
    throw 'Postprocess-only validation takes artificial inputs; observed profile arguments would be ignored.'
}
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
    $method = if ($PostProcessParametersOnly) { 'OriginalPostProcessParameterValidation.Run' } elseif ($NaturalPostProcessOnly) { 'OriginalNaturalPostProcessValidation.Run' } elseif ($PostProcessOnly) { 'OriginalPostProcessValidation.Run' } else { 'OriginalRenderingIntegrationValidation.Run' }
    $arguments = @('-batchmode', '-force-d3d11', '-projectPath', ('"' + $repo + '"'),
        '-executeMethod', $method,
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
    if ($ConfirmedNaturalFade) {
        $fade = (Resolve-Path -LiteralPath $ConfirmedNaturalFade).Path
        $arguments += @('-confirmedNaturalFade', ('"' + $fade + '"'))
    }
    if ($ObservedMaskAlpha) {
        $alpha = (Resolve-Path -LiteralPath $ObservedMaskAlpha).Path
        if ($alpha.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Keep observed mask-alpha inputs outside the repository.'
        }
        $arguments += @('-observedMaskAlpha', ('"' + $alpha + '"'))
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
$expected = if ($PostProcessParametersOnly) { 'PostProcessParametersValidated_TestOnly' } elseif ($NaturalPostProcessOnly) { 'NaturalPostProcessInputsValidated_TestOnly' } elseif ($PostProcessOnly) { 'OriginalPostProcessValidated_TestOnly' } else { 'ActualControlIntegrationGPUValidated_TestOnly' }
if ($summary.status -ne $expected) { throw 'Integration acceptance did not pass.' }
$label = if ($PostProcessParametersOnly) { 'Current-frame producer parameters' } elseif ($NaturalPostProcessOnly) { 'Current-frame postprocess inputs' } elseif ($PostProcessOnly) { 'Explicit postprocess GPU validation' } else { 'Actual Control integration' }
Write-Output "${label} PASS: $($summary.checks) checks; project settings bytes preserved."
