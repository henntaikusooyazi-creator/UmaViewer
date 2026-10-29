param(
    [Parameter(Mandatory)][string] $UnityExecutable,
    [Parameter(Mandatory)][string] $OutputPath,
    [Parameter(Mandatory)][int] $MusicId,
    [Parameter(Mandatory)][string] $ConfirmedProducerProfile,
    [ValidateRange(0, 7)][int] $RightTrack = 0,
    [ValidateRange(-1, 7)][int] $LeftTrack = -1,
    [switch] $FrameInputs,
    [switch] $AudioClock,
    [switch] $AudioClockStartup,
    [switch] $AudioClockStaged,
    [switch] $DeferChannelEvidence,
    [switch] $AudioPcm,
    [string] $AudioClockCheckpoint,
    [switch] $AudioClockTailToExistingDirector,
    [ValidateRange(240, 600)][int] $MaximumWallSeconds = 240
)
$ErrorActionPreference = 'Stop'
if ($AudioClockTailToExistingDirector -and !$AudioClock) { throw 'An explicit audio clock is required for the tail policy.' }
if ($AudioClockStartup -and !$AudioClock) { throw 'Startup consumption requires the explicit audio clock.' }
if ($AudioClockStaged -and !$AudioClockStartup) { throw 'Staged source consumption requires explicit startup ownership.' }
if ($DeferChannelEvidence -and $LeftTrack -lt 0) { throw 'Deferred channel evidence requires two explicit channels.' }
if ($AudioClockCheckpoint -and !$AudioClock) { throw 'A clock checkpoint requires the explicit audio clock.' }
if ($AudioPcm -and !$AudioClock) { throw 'PCM observation requires the explicit audio clock.' }
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
if ($AudioClockCheckpoint) {
    $checkpoint = (Resolve-Path -LiteralPath $AudioClockCheckpoint).Path
    if ($checkpoint -eq $repo -or $checkpoint.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        (Get-Item -LiteralPath $checkpoint).Length -gt 65536) { throw 'A bounded external checkpoint is required.' }
}
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
    if ($FrameInputs) { $arguments += @('-liveFrameInputs', 'true') }
    if ($AudioClock) { $arguments += @('-liveAudioClock', 'true') }
    if ($AudioClockStartup) { $arguments += @('-liveAudioClockStartup', 'true') }
    if ($AudioClockStaged) { $arguments += @('-liveAudioClockStaged', 'true') }
    if ($DeferChannelEvidence) { $arguments += @('-liveDeferChannelEvidence', 'true') }
    if ($AudioPcm) { $arguments += @('-liveAudioPcm', 'true') }
    if ($AudioClockCheckpoint) { $arguments += @('-liveClockCheckpoint', ('"' + $checkpoint + '"')) }
    if ($AudioClockTailToExistingDirector) { $arguments += @('-liveAudioClockTailToExistingDirector', 'true') }
    $arguments += @('-liveMaximumWallSeconds', $MaximumWallSeconds)
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
if ($AudioClock -and (!$summary.audioClockRequested -or ($summary.audioClockJoined + $summary.legacyTailFrames) -ne $summary.validOwnerFrames -or
        $summary.audioClockRejected -ne 0 -or $summary.audioClockFailures -ne 0 -or $summary.audioClockReads -le 100)) {
    throw 'Current Viewer audio -> owner -> rendered serial did not pass.'
}
if ($AudioClockStartup -and (!$summary.audioClockStartupRequested -or $summary.warmupLateOnlyFrames -ne 0)) {
    throw 'The explicit startup clock did not own the initial update.'
}
if ($AudioClockStaged -and (!$summary.audioClockStagedRequested -or $summary.stagedSourceConsumes -ne $summary.audioClockJoined -or
        $summary.stagedSourceCaptures -lt $summary.stagedSourceConsumes -or $summary.stagedSourceInvalidations -ne 0 -or $summary.sourceCaptureObserverFailures -ne 0)) {
    throw 'The staged producer/consumer lifetime did not join the actual owner updates.'
}
if ($FrameInputs) {
    $inputs = Get-Content -Raw -LiteralPath (Join-Path $output 'frame-inputs.private.json') | ConvertFrom-Json
    if ($inputs.mode -ne 'ArtificialSevenStageDiagnostic' -or $inputs.stages -ne 7 -or
            $inputs.inputRenders -le 0 -or $inputs.rgbSamples -le 0 -or $inputs.pixels -le 0 -or
            $inputs.nonFarDepthSamples -le 0 -or $inputs.maxRgbError -gt 1 -or
            $inputs.completedStages -ne ($inputs.inputRenders * 7) -or
            $inputs.stageRgbComparisons -ne ($inputs.rgbSamples * 7)) {
        throw 'Current-frame depth/parameter RGB evidence did not pass.'
    }
}
if ($AudioClockCheckpoint -and (!$summary.smoothingRequested -or $summary.smoothingCommitted -ne $summary.audioClockJoined -or
        $summary.smoothingDiscarded -ne 0 -or $summary.smoothingHeld -ne 0)) {
    throw 'The explicit smoothing checkpoint did not commit exclusively through joined updates.'
}
if ($AudioPcm -and (!$summary.pcmRequested -or !$summary.pcmComplete -or $summary.pcmFailures -ne 0 -or
        $summary.pcmClockSamples -ne $summary.audioClockJoined)) {
    throw 'The selected streaming callbacks did not join the current clip and owner observations.'
}
Write-Output "Natural Viewer playback PASS: $($summary.validOwnerFrames) owner updates, $($summary.renderedFrames) generated frames; $($summary.warmupLateOnlyFrames) initial late-only frames held."
