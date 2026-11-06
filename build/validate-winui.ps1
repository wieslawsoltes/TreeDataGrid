<#
.SYNOPSIS
  Builds and validates the WinUI 3 (Windows App SDK) projects that link the Uno port's sources.

.DESCRIPTION
  1. Builds TreeDataGrid.Controls.WinUI, both WinUI samples and the WinUI test host with MSBuild
     (WinUI class libraries with XAML need Visual Studio's MSBuild and its Windows App SDK tools).
  2. Runs the linked Uno unit tests on the WinUI UI thread (TreeDataGrid.WinUI.Tests.exe).
  3. Runs the linked sample tests with dotnet test.
  4. Runs every native suite registered in App.Validation.cs in a fresh WinUI sample process,
     and the Activity Monitor smoke run.
  Run it from an interactive desktop session: the samples open windows.
#>
param(
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = $(if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'win-arm64' } else { 'win-x64' }),
    [string]$Output = 'artifacts/winui',
    [string[]]$Suite = @(),
    [int]$SuiteTimeoutSeconds = 600,
    [switch]$SkipBuild,
    [switch]$NoSourceLink
)

$ErrorActionPreference = 'Stop'
# `powershell -File` passes a comma-separated list as one string.
$Suite = @($Suite | ForEach-Object { $_ -split ',' } | Where-Object { $_ })
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
New-Item -ItemType Directory -Force -Path $Output | Out-Null
$Output = (Resolve-Path $Output).Path
$failures = New-Object System.Collections.Generic.List[string]

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if (-not $path) { throw 'MSBuild from Visual Studio or Build Tools is required.' }
    $native = Join-Path (Split-Path $path) ($env:PROCESSOR_ARCHITECTURE.ToLowerInvariant() + '\MSBuild.exe')
    if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64' -and (Test-Path $native)) { return $native }
    return $path
}

function Invoke-Checked([string]$Name, [scriptblock]$Command) {
    Write-Host "== $Name"
    & $Command
    if ($LASTEXITCODE -ne 0) { $failures.Add("$Name (exit $LASTEXITCODE)"); Write-Host "FAILED: $Name" }
}

# Runs a console-subsystem WinUI app, captures its output and enforces a timeout.
function Invoke-App([string]$Name, [string]$Exe, [string[]]$Arguments, [string]$Marker) {
    $log = Join-Path $Output ($Name + '.log')
    $start = [Diagnostics.Stopwatch]::StartNew()
    $start_ = @{ FilePath = $Exe; WorkingDirectory = (Split-Path $Exe); PassThru = $true; NoNewWindow = $true
        RedirectStandardOutput = $log; RedirectStandardError = ($log + '.err') }
    # Windows PowerShell rejects an empty argument list.
    if ($Arguments.Count -gt 0) { $start_.ArgumentList = $Arguments }
    $process = Start-Process @start_
    $null = $process.Handle # Windows PowerShell only reports ExitCode for a cached handle.
    if (-not $process.WaitForExit($SuiteTimeoutSeconds * 1000)) {
        & taskkill /T /F /PID $process.Id | Out-Null
        $failures.Add("$Name (timeout)")
        Write-Host "TIMEOUT: $Name"
        return
    }
    $text = (Get-Content $log -Raw -ErrorAction SilentlyContinue) + (Get-Content ($log + '.err') -Raw -ErrorAction SilentlyContinue)
    $passed = $process.ExitCode -eq 0 -and (-not $Marker -or $text -match [regex]::Escape($Marker))
    $status = if ($passed) { 'PASSED' } else { 'FAILED' }
    Write-Host ("{0} {1} ({2:N1}s)" -f $status, $Name, $start.Elapsed.TotalSeconds)
    if (-not $passed) {
        $failures.Add("$Name (exit $($process.ExitCode))")
        Write-Host ($text -split "`n" | Select-Object -Last 40 | Out-String)
    }
}

$properties = @("-p:Configuration=$Configuration", "-p:RuntimeIdentifier=$RuntimeIdentifier")
if ($NoSourceLink) { $properties += '-p:DisableSourceLink=true' }
$projects = @(
    'src/TreeDataGrid.Controls.WinUI/TreeDataGrid.Controls.WinUI.csproj',
    'samples/TreeDataGridWinUISample/TreeDataGridWinUISample.csproj',
    'samples/TreeDataGridWinUIActivityMonitor/TreeDataGridWinUIActivityMonitor.csproj',
    'tests/TreeDataGrid.WinUI.Tests/TreeDataGrid.WinUI.Tests.csproj')
if (-not $SkipBuild) {
    $msbuild = Find-MSBuild
    foreach ($project in $projects) {
        Invoke-Checked "build $project" { & $msbuild $project -restore -nologo -v:minimal '-clp:ErrorsOnly;Summary' @properties }
    }
    if ($failures.Count -gt 0) { throw "Build failed: $($failures -join ', ')" }
}

$bin = "bin/$Configuration/net10.0-windows10.0.26100.0/$RuntimeIdentifier"
$tests = Join-Path $root "tests/TreeDataGrid.WinUI.Tests/$bin/TreeDataGrid.WinUI.Tests.exe"
$sample = Join-Path $root "samples/TreeDataGridWinUISample/$bin/TreeDataGridWinUISample.exe"
$monitor = Join-Path $root "samples/TreeDataGridWinUIActivityMonitor/$bin/TreeDataGridWinUIActivityMonitor.exe"

if ($Suite.Count -eq 0) {
    Invoke-App 'unit-tests' $tests @() 'WINUI_TESTS_RESULT: passed='
    $sampleTestArgs = @('test', 'samples/TreeDataGridWinUISample.Tests/TreeDataGridWinUISample.Tests.csproj', '-c', $Configuration, '--nologo')
    if ($NoSourceLink) { $sampleTestArgs += '-p:DisableSourceLink=true' }
    Invoke-Checked 'sample tests' { & dotnet @sampleTestArgs }
    Invoke-App 'activity-monitor-smoke' $monitor @('--smoke') 'UNO_ACTIVITY_MONITOR_SMOKE_PASSED'
    $Suite = [regex]::Matches((Get-Content 'samples/TreeDataGridUnoSample/App.Validation.cs' -Raw), 'case "([a-z-]+)":') |
        ForEach-Object { $_.Groups[1].Value }
}
foreach ($name in $Suite) {
    Invoke-App "suite-$name" $sample @('--smoke', '--suite', $name) "UNO_SUITE_PASSED: $name;"
}

if ($failures.Count -gt 0) {
    Write-Host "WINUI_VALIDATION_FAILED: $($failures.Count): $($failures -join '; ')"
    exit 1
}
Write-Host "WINUI_VALIDATION_PASSED: unit tests, sample tests, Activity Monitor smoke and $($Suite.Count) native suites"
