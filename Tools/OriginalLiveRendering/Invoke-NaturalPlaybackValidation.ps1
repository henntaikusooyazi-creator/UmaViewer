param(
    [Parameter(Mandatory)][string] $UnityExecutable,
    [Parameter(Mandatory)][string] $OutputPath,
    [Parameter(Mandatory)][int] $MusicId,
    [Parameter(Mandatory)][string] $ConfirmedProducerProfile,
    [ValidateRange(0, 7)][int] $RightTrack = 0,
    [ValidateRange(-1, 7)][int] $LeftTrack = -1
)
$ErrorActionPreference = 'Stop'
if ($RightTrack -eq $LeftTrack) { throw 'Explicit ordered channels must be distinct.' }
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = [IO.Path]::GetFullPath($OutputPath)
if ($output -eq $repo -or $output.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Keep all private playback results outside the repository.'
}
$busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" |
    Where-Object { $_.CommandLine -and $_.CommandLine.Contains($repo) }
if ($busy) { throw 'The target project already has an Editor; leave it untouched.' }
$profile = (Resolve-Path -LiteralPath $ConfirmedProducerProfile).Path
$null = New-Item -ItemType Directory -Path $output -Force
$saved = @{}
foreach ($relative in @('ProjectSettings/GraphicsSettings.asset', 'ProjectSettings/QualitySettings.asset',
        'ProjectSettings/ShaderGraphSettings.asset', 'Assets/Plugins/CySpringPlugin.dll.meta',
        'Assets/RT/RT_Screen.renderTexture', 'Config.json')) {
    $path = Join-Path $repo $relative
    $saved[$path] = if (Test-Path -LiteralPath $path) { [IO.File]::ReadAllBytes($path) } else { $null }
}
$process = $null
try {
    # Disable unrelated downloads before scene Awake/Start, while preserving the
    # caller's exact configuration bytes after this dedicated Editor exits.
    $configPath = Join-Path $repo 'Config.json'
    $config = if ($null -ne $saved[$configPath]) {
        [Text.Encoding]::UTF8.GetString($saved[$configPath]) | ConvertFrom-Json -AsHashtable
    } else { @{} }
    $config['DownloadMissingResources'] = $false
    $config['Language'] = 1
    $config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $configPath -Encoding utf8
    $arguments = @('-batchmode', '-force-d3d11', '-projectPath', ('"' + $repo + '"'),
        '-executeMethod', 'OriginalLivePlaybackValidation.Run', '-liveMusicId', $MusicId,
        '-liveProfile', ('"' + $profile + '"'), '-liveOutput', ('"' + $output + '"'),
        '-logFile', ('"' + (Join-Path $output 'unity.private.log') + '"'))
    $arguments += @('-liveRightTrack', $RightTrack, '-liveLeftTrack', $LeftTrack)
    $process = Start-Process -FilePath $UnityExecutable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
}
finally {
    if (!$process -or $process.HasExited) {
        foreach ($path in $saved.Keys) {
            if ($null -ne $saved[$path]) { [IO.File]::WriteAllBytes($path, $saved[$path]) }
            elseif (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
        }
    }
}
if (!$process -or !$process.HasExited -or $process.ExitCode -ne 0) {
    throw 'Dedicated playback Editor did not pass; inspect the external private result and log.'
}
$summary = Get-Content -Raw -LiteralPath (Join-Path $output 'playback.private.json') | ConvertFrom-Json
if ($summary.status -ne 'NaturalViewerPlaybackCompleted') { throw 'Natural Viewer playback acceptance failed.' }
Write-Output "Natural Viewer playback PASS: $($summary.validOwnerFrames) owner updates, $($summary.renderedFrames) generated frames; $($summary.warmupLateOnlyFrames) initial late-only frames held."
