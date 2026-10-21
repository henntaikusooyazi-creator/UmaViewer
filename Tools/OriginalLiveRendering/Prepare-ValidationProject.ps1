param(
    [Parameter(Mandatory)][string] $ProjectPath
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$target = [IO.Path]::GetFullPath($ProjectPath)
if (Test-Path -LiteralPath $target) { throw 'Choose a new, empty validation project path.' }
if ($target.StartsWith($repo + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Keep generated validation assets outside the repository.'
}
foreach ($directory in @('Assets/Runtime', 'Assets/Editor', 'Assets/Shaders', 'Packages', 'ProjectSettings')) {
    New-Item -ItemType Directory -Path (Join-Path $target $directory) -Force | Out-Null
}
# Only the engine-facing components are compiled here. The real timeline integration
# belongs to the full Viewer build; this project does not replace it with mock types.
foreach ($name in @('OriginalMultiCameraRenderer', 'OriginalMaskCoverageGenerator',
        'OriginalCompositeCueAdapter', 'OriginalCompositeProducerMath', 'OriginalCameraProducer')) {
    foreach ($suffix in @('.cs', '.cs.meta')) {
        Copy-Item -LiteralPath (Join-Path $repo "Assets/Scripts/OriginalRendering/$name$suffix") -Destination (Join-Path $target 'Assets/Runtime')
    }
}
foreach ($name in @('OriginalMultiCamera', 'OriginalMaskCoverage')) {
    foreach ($suffix in @('.shader', '.shader.meta')) {
        Copy-Item -LiteralPath (Join-Path $repo "Assets/Shaders/OriginalRendering/$name$suffix") -Destination (Join-Path $target 'Assets/Shaders')
    }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Validation/OriginalRenderingValidation.cs') -Destination (Join-Path $target 'Assets/Editor')
foreach ($name in @('LiveDiagonalCompositeTestOnly.cs', 'LiveDiagonalCompositeTestOnly.shader')) {
    $directory = if ($name.EndsWith('.cs')) { 'Assets/Runtime' } else { 'Assets/Shaders' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "Validation/Legacy/$name") -Destination (Join-Path $target $directory)
}
Copy-Item -LiteralPath (Join-Path $repo 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $target 'ProjectSettings')
$manifest = @{dependencies=@{'com.unity.render-pipelines.universal'='14.0.12'; 'com.unity.modules.imageconversion'='1.0.0'}}
[IO.File]::WriteAllText((Join-Path $target 'Packages/manifest.json'), ($manifest | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
Write-Output 'Prepared a separate synthetic GPU validation project. No Viewer scenes, settings or packages were changed.'
