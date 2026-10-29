# Library only. The caller supplies explicit owned files and a process-idle check.
Set-StrictMode -Version Latest

function Get-OwnedFileHash([string]$Path) {
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256 -ErrorAction Stop).Hash.ToLowerInvariant()
}

function Assert-OwnedRegularFile([string]$Path) {
    if (![IO.Path]::IsPathFullyQualified($Path)) { throw 'Absolute file path required' }
    $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    if ($item.PSIsContainer) { throw 'File required' }
    while ($null -ne $item) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse point rejected' }
        $item = $item.Directory
        if ($null -ne $item -and $item -is [IO.DirectoryInfo]) {
            while ($null -ne $item) {
                if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse parent rejected' }
                $item = $item.Parent
            }
        }
    }
}

function Invoke-OwnedFileUpgrade {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Plan,
        [Parameter(Mandatory)][scriptblock]$AssertIdle,
        [Parameter(Mandatory)][string]$ReceiptDirectory
    )
    $ErrorActionPreference = 'Stop'
    if ($Plan.schema -cne 'owned-file-upgrade-v1') { throw 'Unknown upgrade plan' }
    $files = @($Plan.files)
    if ($files.Count -lt 1 -or $files.Count -gt 3) { throw 'One to three explicitly owned files required' }
    $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($f in $files) {
        foreach ($p in @($f.target, $f.source)) {
            Assert-OwnedRegularFile $p
            if (!$paths.Add([IO.Path]::GetFullPath($p))) { throw 'Duplicate or aliased plan path' }
        }
        if ($f.expected_sha256 -cnotmatch '^[a-f0-9]{64}$' -or
            $f.source_sha256 -cnotmatch '^[a-f0-9]{64}$') { throw 'Explicit hashes required' }
        if ((Get-OwnedFileHash $f.target) -cne $f.expected_sha256 -or
            (Get-OwnedFileHash $f.source) -cne $f.source_sha256) { throw 'Owned file changed' }
    }
    foreach ($pin in @($Plan.protected)) {
        Assert-OwnedRegularFile $pin.path
        if ($paths.Contains([IO.Path]::GetFullPath($pin.path))) { throw 'Protected target rejected' }
        if ((Get-OwnedFileHash $pin.path) -cne $pin.sha256) { throw 'Protected input changed' }
    }
    & $AssertIdle
    if (Test-Path -LiteralPath $ReceiptDirectory) { throw 'Fresh receipt directory required' }
    $null = New-Item -ItemType Directory -Path $ReceiptDirectory
    $completed = [Collections.Generic.List[object]]::new()
    $receipt = [ordered]@{schema='owned-file-upgrade-result-v1'; status='Preparing'; changed=0;
        rollback='not_needed'; runtime_contract=$false}
    try {
        for ($i=0; $i -lt $files.Count; ++$i) {
            $f = $files[$i]
            & $AssertIdle
            if ((Get-OwnedFileHash $f.target) -cne $f.expected_sha256 -or
                (Get-OwnedFileHash $f.source) -cne $f.source_sha256) { throw 'File changed before commit' }
            $backup = Join-Path $ReceiptDirectory ("{0:00}.before" -f $i)
            Copy-Item -LiteralPath $f.target -Destination $backup -ErrorAction Stop
            if ((Get-OwnedFileHash $backup) -cne $f.expected_sha256) { throw 'Backup verification failed' }
            $stage = $f.target + '.owned-stage-' + [guid]::NewGuid().ToString('N')
            Copy-Item -LiteralPath $f.source -Destination $stage -ErrorAction Stop
            if ((Get-OwnedFileHash $stage) -cne $f.source_sha256) { throw 'Staged bytes changed' }
            & $AssertIdle
            if ((Get-OwnedFileHash $f.target) -cne $f.expected_sha256) { throw 'Target changed before replacement' }
            [IO.File]::Move($stage, $f.target, $true)
            $completed.Add(@{file=$f;backup=$backup})
            if ((Get-OwnedFileHash $f.target) -cne $f.source_sha256) { throw 'Committed hash mismatch' }
        }
        & $AssertIdle
        foreach ($pin in @($Plan.protected)) {
            if ((Get-OwnedFileHash $pin.path) -cne $pin.sha256) { throw 'Protected input changed after upgrade' }
        }
        $receipt.status = 'Installed'
        $receipt.changed = $completed.Count
    } catch {
        $receipt.status = 'Rejected'
        $receipt.error = $_.Exception.Message
        $receipt.changed = $completed.Count
        if ($completed.Count -gt 0) {
            try {
                & $AssertIdle
                for ($i=$completed.Count-1; $i -ge 0; --$i) {
                    $c=$completed[$i]
                    & $AssertIdle
                    if ((Get-OwnedFileHash $c.file.target) -cne $c.file.source_sha256 -or
                        (Get-OwnedFileHash $c.backup) -cne $c.file.expected_sha256) { throw 'Rollback ownership changed' }
                    Copy-Item -LiteralPath $c.backup -Destination $c.file.target -ErrorAction Stop
                    if ((Get-OwnedFileHash $c.file.target) -cne $c.file.expected_sha256) { throw 'Rollback hash mismatch' }
                }
                $receipt.rollback = 'Verified'
            } catch {
                $receipt.rollback = 'PendingManualRecovery'
                $receipt.rollback_error = $_.Exception.Message
            }
        }
    }
    $receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $ReceiptDirectory 'RESULT.json')
    [pscustomobject]$receipt
}
