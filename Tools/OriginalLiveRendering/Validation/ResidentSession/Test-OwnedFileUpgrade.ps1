param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
. "$PSScriptRoot\OwnedFileUpgrade.ps1"
if(Test-Path -LiteralPath $OutputDirectory){throw 'Fresh test directory required'}
$null=New-Item -ItemType Directory -Path $OutputDirectory
$script:checks=0
function Check([bool]$Condition){$script:checks++;if(!$Condition){throw "Check $script:checks failed"}}
function Fixture([string]$Name){
    $d=Join-Path $OutputDirectory $Name
    $null=New-Item -ItemType Directory -Path $d
    foreach($name in @('target1','target2','source1','source2','protected')){
        [IO.File]::WriteAllText((Join-Path $d $name),$name)
    }
    $files=@(1,2|ForEach-Object{@{target=(Join-Path $d "target$_");source=(Join-Path $d "source$_");
        expected_sha256=(Get-OwnedFileHash (Join-Path $d "target$_"));source_sha256=(Get-OwnedFileHash (Join-Path $d "source$_"))}})
    @{directory=$d;plan=@{schema='owned-file-upgrade-v1';files=$files;protected=@(@{path=(Join-Path $d 'protected');sha256=(Get-OwnedFileHash (Join-Path $d 'protected'))})}}
}
function Reject($F,[scriptblock]$Idle={}){
    $rejected=$false
    try{$r=Invoke-OwnedFileUpgrade -Plan $F.plan -AssertIdle $Idle -ReceiptDirectory "$($F.directory)\receipt"; $rejected=$r.status-eq'Rejected'}catch{$rejected=$true}
    Check $rejected
    foreach($f in $F.plan.files){Check ((Get-OwnedFileHash $f.target)-eq$f.expected_sha256)}
}
$f=Fixture 'success'
$r=Invoke-OwnedFileUpgrade -Plan $f.plan -AssertIdle {} -ReceiptDirectory "$($f.directory)\receipt"
Check ($r.status-eq'Installed'-and$r.changed-eq2)
foreach($file in $f.plan.files){Check ((Get-OwnedFileHash $file.target)-eq$file.source_sha256)}
Check ((Get-OwnedFileHash $f.plan.protected[0].path)-eq$f.plan.protected[0].sha256)

$f=Fixture 'live-owner';Reject $f {throw 'Owner still alive'}
$f=Fixture 'wrong-source';$f.plan.files[0].source_sha256='0'*64;Reject $f
$f=Fixture 'changed-protected';$f.plan.protected[0].sha256='0'*64;Reject $f
$f=Fixture 'unknown-schema';$f.plan.schema='unknown';Reject $f
$f=Fixture 'duplicate';$f.plan.files[1].source=$f.plan.files[0].source;Reject $f
$f=Fixture 'protected-target';$f.plan.protected=@(@{path=$f.plan.files[0].target;sha256=$f.plan.files[0].expected_sha256});Reject $f
$f=Fixture 'invalid-hash';$f.plan.files[0].source_sha256='ABC';Reject $f
$f=Fixture 'relative-path';$f.plan.files[0].source='relative';Reject $f
$f=Fixture 'too-many';$f.plan.files=@($f.plan.files[0]) * 4;Reject $f
$f=Fixture 'existing-receipt';$null=New-Item -ItemType Directory "$($f.directory)\receipt";Reject $f

$f=Fixture 'rollback'
$script:idleCalls=0
$r=Invoke-OwnedFileUpgrade -Plan $f.plan -AssertIdle {
    $script:idleCalls++
    if($script:idleCalls-eq4){throw 'Injected second-file admission failure'}
} -ReceiptDirectory "$($f.directory)\receipt"
Check ($r.status-eq'Rejected'-and$r.changed-eq1-and$r.rollback-eq'Verified')
foreach($file in $f.plan.files){Check ((Get-OwnedFileHash $file.target)-eq$file.expected_sha256)}

$f=Fixture 'owner-arrives'
$script:idleCalls=0
$r=Invoke-OwnedFileUpgrade -Plan $f.plan -AssertIdle {
    $script:idleCalls++
    if($script:idleCalls-ge4){throw 'New owner arrived: no rollback writes'}
} -ReceiptDirectory "$($f.directory)\receipt"
Check ($r.status-eq'Rejected'-and$r.rollback-eq'PendingManualRecovery')
Check ((Get-OwnedFileHash $f.plan.files[0].target)-eq$f.plan.files[0].source_sha256)
Check ((Get-OwnedFileHash $f.plan.files[1].target)-eq$f.plan.files[1].expected_sha256)

@{schema='owned-upgrade-fixture-v1';checks=$script:checks;status='PASS';scope='TestOnly';runtime_contract=$false}|
    ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'RESULT.json')
"PASS $script:checks owned upgrade checks (TestOnly)"
