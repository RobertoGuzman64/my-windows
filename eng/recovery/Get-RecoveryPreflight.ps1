[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RepositoryPath,
    [string]$ProfilesDirectory,
    [string[]]$Roles = @('repository'),
    [string]$Machine
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:OS -ne 'Windows_NT') {
    throw 'La inspección de recursos de recuperación requiere Windows.'
}

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$profilePath = if ([string]::IsNullOrWhiteSpace($ProfilesDirectory)) {
    Join-Path $root 'profiles'
}
else {
    [IO.Path]::GetFullPath($ProfilesDirectory)
}
$repository = [IO.Path]::GetFullPath($RepositoryPath)
$cli = Join-Path $root 'src\MyWindows.Cli\bin\Release\net10.0\MyWindows.Cli.dll'

if (-not (Test-Path -LiteralPath $profilePath -PathType Container)) {
    throw 'La carpeta de perfiles no existe.'
}
if (-not (Test-Path -LiteralPath $cli -PathType Leaf)) {
    throw 'Falta el CLI Release de MyWindows. Compilarlo antes de consultar.'
}

$dotnet = (Get-Command dotnet.exe -CommandType Application -ErrorAction Stop).Source
$parameters = @($profilePath, $repository)
foreach ($role in $Roles) {
    $parameters += @('--role', $role)
}
if (-not [string]::IsNullOrWhiteSpace($Machine)) {
    $parameters += @('--machine', $Machine)
}

function Invoke-ReadOnlyCommand {
    param([Parameter(Mandatory = $true)][string]$Command)
    $output = & $dotnet $cli $Command @parameters
    $code = $LASTEXITCODE
    if ($code -notin @(0, 3) -or -not $output) {
        throw "Consulta '$Command' fallida; código $code. No se ha ejecutado apply, repair ni remove."
    }
    try {
        $result = ($output -join [Environment]::NewLine) | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw "El comando '$Command' no ha devuelto JSON válido."
    }
    return [ordered]@{ exitCode = $code; result = $result }
}

$plan = Invoke-ReadOnlyCommand -Command 'plan'
$status = Invoke-ReadOnlyCommand -Command 'status'
$verification = Invoke-ReadOnlyCommand -Command 'verify'

[ordered]@{
    schemaVersion = 1
    mode = 'read-only'
    observedAtUtc = [DateTime]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    profilesDirectory = $profilePath
    repositoryPath = $repository
    roles = @($Roles)
    machine = if ([string]::IsNullOrWhiteSpace($Machine)) { $null } else { $Machine }
    plan = $plan
    status = $status
    verify = $verification
    acceptance = 'notEvaluated'
} | ConvertTo-Json -Depth 12
