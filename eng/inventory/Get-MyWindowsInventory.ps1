#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('desktop', 'laptop', 'vm')]
    [string]$Role
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Application {
    param([Parameter(Mandatory = $true)][string]$Name)
    Get-Command -Name $Name -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
}

function Get-ApplicationVersion {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    $application = Get-Application -Name $Name
    if ($null -eq $application) {
        return @{ status = 'not-detected'; version = $null }
    }

    try {
        $output = @(& $application.Source @Arguments 2>$null)
        if ($LASTEXITCODE -ne 0) {
            return @{ status = 'error'; version = $null }
        }
        $match = [regex]::Match(($output -join "`n"), $Pattern)
        if (-not $match.Success) {
            return @{ status = 'error'; version = $null }
        }
        return @{ status = 'detected'; version = $match.Value }
    }
    catch {
        return @{ status = 'error'; version = $null }
    }
}

function Get-DotNetSdks {
    $application = Get-Application -Name 'dotnet.exe'
    if ($null -eq $application) {
        return @{ status = 'not-detected'; versions = @() }
    }

    try {
        $output = @(& $application.Source --list-sdks 2>$null)
        if ($LASTEXITCODE -ne 0) {
            return @{ status = 'error'; versions = @() }
        }
        $versions = @(
            foreach ($line in $output) {
                if ($line -match '^\s*(\d+\.\d+\.\d+(?:-[^\s]+)?)\s+\[') {
                    $Matches[1]
                }
            }
        )
        return @{ status = 'detected'; versions = $versions }
    }
    catch {
        return @{ status = 'error'; versions = @() }
    }
}

function Get-PowerToys {
    $roots = @(
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
    )

    try {
        $detected = $false
        $versions = @(
            foreach ($root in $roots) {
                if (Test-Path -LiteralPath $root) {
                    foreach ($item in @(Get-ItemProperty -Path (Join-Path $root '*') -ErrorAction Stop)) {
                        $displayName = $item.PSObject.Properties['DisplayName']
                        $displayVersion = $item.PSObject.Properties['DisplayVersion']
                        if ($null -ne $displayName -and [string]$displayName.Value -match '(?:^|\s)PowerToys(?:\s|$|\()') {
                            $detected = $true
                            if ($null -ne $displayVersion -and -not [string]::IsNullOrWhiteSpace([string]$displayVersion.Value)) {
                                [string]$displayVersion.Value
                            }
                        }
                    }
                }
            }
        )
        if ($detected) {
            return @{ status = 'detected-in-uninstall-registry'; versions = @($versions | Sort-Object -Unique) }
        }
        return @{ status = 'not-detected-in-uninstall-registry'; versions = @() }
    }
    catch {
        return @{ status = 'error'; versions = @() }
    }
}

$osInfo = @{ status = 'error'; caption = $null; version = $null; build = $null; revision = $null; architecture = $null }
try {
    $os = Get-CimInstance -ClassName Win32_OperatingSystem -ErrorAction Stop
    $revision = $null
    try {
        $registry = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction Stop
        if ($null -ne $registry.UBR) {
            $revision = [int]$registry.UBR
        }
    }
    catch {
        $revision = $null
    }
    $osInfo = @{
        status = 'observed'
        caption = [string]$os.Caption
        version = [string]$os.Version
        build = [string]$os.BuildNumber
        revision = $revision
        architecture = [string]$os.OSArchitecture
    }
}
catch {
    $osInfo.status = 'error'
}

$isAdministrator = $null
try {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    $isAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
catch {
    $isAdministrator = $null
}

$executionPolicy = $null
try {
    $executionPolicy = [string](Get-ExecutionPolicy -ErrorAction Stop)
}
catch {
    $executionPolicy = $null
}

$programsFolder = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)

$result = [ordered]@{
    schema = 'mywindows.phase0.inventory.v1'
    role = $Role
    capturedAtUtc = [DateTime]::UtcNow.ToString('o')
    os = $osInfo
    permissions = @{
        isAdministrator = $isAdministrator
        executionPolicy = $executionPolicy
    }
    tools = @{
        currentPowerShellVersion = $PSVersionTable.PSVersion.ToString()
        windowsPowerShell51Available = ($null -ne (Get-Application -Name 'powershell.exe'))
        pwsh = (Get-ApplicationVersion -Name 'pwsh.exe' -Arguments @('--version') -Pattern '\d+\.\d+\.\d+(?:-[\w.]+)?')
        winget = (Get-ApplicationVersion -Name 'winget.exe' -Arguments @('--version') -Pattern 'v?\d+\.\d+(?:\.\d+)+')
        git = (Get-ApplicationVersion -Name 'git.exe' -Arguments @('--version') -Pattern '\d+\.\d+(?:\.\d+)+')
        dotnetSdks = (Get-DotNetSdks)
        powerToys = (Get-PowerToys)
    }
    startMenu = @{
        userProgramsKnownFolderResolves = -not [string]::IsNullOrWhiteSpace($programsFolder)
        userProgramsFolderExists = (-not [string]::IsNullOrWhiteSpace($programsFolder) -and (Test-Path -LiteralPath $programsFolder -PathType Container))
    }
}

$result | ConvertTo-Json -Depth 8
