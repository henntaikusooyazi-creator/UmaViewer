param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
 $public=$root
$vs='C:\Program Files\Microsoft Visual Studio\18\Community'
Import-Module "$vs\Common7\Tools\Microsoft.VisualStudio.DevShell.dll"
if($env:VSCMD_ARG_TGT_ARCH-ne'x64'){Enter-VsDevShell -VsInstallPath $vs -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64'|Out-Null}
foreach($name in @('CL','_CL_','LINK','_LINK_')){[Environment]::SetEnvironmentVariable($name,'','Process')}
if(Test-Path -LiteralPath $OutputDirectory){throw "Fresh output directory required"}
$out=New-Item -ItemType Directory -Path $OutputDirectory
Set-Location -LiteralPath $out.FullName
$flags=@('/nologo','/std:c++20','/EHsc','/W4','/WX','/O1',"/I$public")
& cl @flags "$root\resident_state_tests.cpp" /Fe:state.exe *> state.build.log
if($LASTEXITCODE){throw 'State fixture compile failed'}
& cl @flags /LD "$root\fixture_collector.cpp" /Fe:fixture1.dll *> fixture.build.log
if($LASTEXITCODE){throw 'Own fixture collector compile failed'}
Copy-Item -LiteralPath fixture1.dll -Destination fixture2.dll
& cl @flags "$root\resident_host_tests.cpp" /Fe:host.exe *> host.build.log
if($LASTEXITCODE){throw 'Resident host compile failed'}
& .\state.exe *> state.private.log
if($LASTEXITCODE){throw 'State fixture failed'}
& .\host.exe "$out\fixture1.dll" "$out\fixture2.dll" *> host.private.log
if($LASTEXITCODE){throw 'Win32 resident fixture failed'}
Get-Content state.private.log
Get-Content host.private.log
