[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('format', 'check', 'tests', 'build', 'validate', 'audit', 'all')]
    [string]$Task,

    [Parameter(Position = 1)]
    [ValidateSet('only')]
    [string]$Mode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Task -eq 'format' -and $Mode -eq 'only') {
    throw 'format cambia archivos. Para verificar sin cambios utiliza check only.'
}

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$solution = 'MyWindows.slnx'
$cli = 'src/MyWindows.Cli/MyWindows.Cli.csproj'
$runId = '{0}-{1}-{2}' -f (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ'), $PID, [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runDirectory = Join-Path $root (Join-Path 'artifacts/quality' $runId)
$script:testSummary = $null
$script:completed = 0
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$steps = switch ($Task) {
    'format' { @('restore', 'format-write') }
    'check' { @('restore', 'format-check', 'debug-build', 'repository-audit') }
    'tests' { @('restore', 'debug-build', 'tests') }
    'build' { @('restore', 'release-build') }
    'validate' { @('restore', 'release-build', 'profiles-validate', 'profiles-resolve', 'repository-audit') }
    'audit' { @('repository-audit') }
    'all' { @('restore', 'format-check', 'debug-build', 'tests', 'release-build', 'profiles-validate', 'profiles-resolve', 'repository-audit') }
}

function Get-StepArguments {
    param([string]$Step)

    switch ($Step) {
        'restore' { return @('restore', $solution) }
        'format-write' { return @('format', $solution, '--no-restore') }
        'format-check' { return @('format', $solution, '--verify-no-changes', '--no-restore') }
        'debug-build' { return @('build', $solution, '--configuration', 'Debug', '--no-restore') }
        'tests' {
            return @('test', $solution, '--configuration', 'Debug', '--no-build', '--no-restore', '--logger', 'trx;LogFileName=tests.trx', '--results-directory', (Join-Path $runDirectory 'test-results'))
        }
        'release-build' { return @('build', $solution, '--configuration', 'Release', '--no-restore') }
        'profiles-validate' { return @('run', '--project', $cli, '--configuration', 'Release', '--no-build', '--no-restore', '--', 'profiles', 'validate', 'profiles') }
        'profiles-resolve' { return @('run', '--project', $cli, '--configuration', 'Release', '--no-build', '--no-restore', '--', 'profiles', 'resolve', 'profiles') }
    }
}

function Read-TestSummary {
    $report = Join-Path $runDirectory 'test-results/tests.trx'
    if (-not (Test-Path -LiteralPath $report -PathType Leaf)) {
        throw 'MSTest no ha producido el informe TRX requerido.'
    }

    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($report)
    $counters = $xml.SelectSingleNode("//*[local-name()='Counters']")
    if ($null -eq $counters) {
        throw 'El informe TRX no contiene contadores verificables.'
    }

    $total = [int]$counters.GetAttribute('total')
    $passed = [int]$counters.GetAttribute('passed')
    $failed = [int]$counters.GetAttribute('failed')
    $notExecuted = [int]$counters.GetAttribute('notExecuted')
    if ($total -le 0 -or $passed -ne $total -or $failed -ne 0 -or $notExecuted -ne 0) {
        throw "MSTest: $passed/$total correctas, $failed fallidas, $notExecuted sin ejecutar."
    }

    return "$passed/$total tests"
}

function Invoke-QualityStep {
    param([string]$Step)

    $number = $script:completed + 1
    $log = Join-Path $runDirectory "$Step.log"
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    Write-Host ('  [{0}/{1}] {2,-20} ' -f $number, $steps.Count, $Step) -NoNewline

    $exitCode = 1
    $failureDetail = $null
    try {
        $ErrorActionPreference = 'Continue'
        if ($Step -eq 'repository-audit') {
            & (Join-Path $PSScriptRoot 'maintenance/Test-Repository.ps1') *> $log
            $exitCode = if ($?) { 0 } else { 1 }
        }
        else {
            $arguments = @(Get-StepArguments -Step $Step)
            & dotnet @arguments *> $log
            $exitCode = $LASTEXITCODE
        }
    }
    catch {
        $failureDetail = $_.Exception.Message
    }
    finally {
        $ErrorActionPreference = 'Stop'
        $timer.Stop()
    }

    if ($null -eq $failureDetail -and $exitCode -eq 0 -and $Step -eq 'tests') {
        try {
            $script:testSummary = Read-TestSummary
        }
        catch {
            $failureDetail = $_.Exception.Message
        }
    }

    if ($null -ne $failureDetail -or $exitCode -ne 0) {
        Write-Host ('FAIL ({0:n1}s)' -f $timer.Elapsed.TotalSeconds) -ForegroundColor Red
        if ($failureDetail) { Write-Host "  $failureDetail" -ForegroundColor Red }
        if (Test-Path -LiteralPath $log -PathType Leaf) {
            Get-Content -LiteralPath $log -Tail 100 | ForEach-Object { Write-Host $_ }
        }
        Write-Host "  Registro completo: $log" -ForegroundColor Yellow
        throw "Gate '$Step' fallido."
    }

    $detail = if ($Step -eq 'tests') { " · $script:testSummary" } else { '' }
    Write-Host ('PASS ({0:n1}s){1}' -f $timer.Elapsed.TotalSeconds, $detail) -ForegroundColor Green
    $script:completed++
}

Push-Location -LiteralPath $root
try {
    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
    Write-Host ''
    Write-Host "MYWINDOWS | QUALITY | $($Task.ToUpperInvariant())$(if ($Mode) { ' ONLY' })"
    Write-Host '--------------------------------------------------------------'
    foreach ($step in $steps) {
        Invoke-QualityStep -Step $step
    }
    $stopwatch.Stop()
    Write-Host '--------------------------------------------------------------'
    Write-Host ('PASS | {0} gates | {1:n1}s' -f $script:completed, $stopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
    Write-Host "Logs: $runDirectory"
}
catch {
    $stopwatch.Stop()
    Write-Host '--------------------------------------------------------------'
    Write-Host ('FAIL | {0}/{1} gates | {2:n1}s' -f $script:completed, $steps.Count, $stopwatch.Elapsed.TotalSeconds) -ForegroundColor Red
    Write-Host "Logs: $runDirectory"
    throw
}
finally {
    Pop-Location
}
