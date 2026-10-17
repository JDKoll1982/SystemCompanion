#Requires -Version 5.1
<#
.SYNOPSIS
    Collects a plain-language snapshot of this Windows PC for the System Companion app.

.DESCRIPTION
    Reads hardware and Windows configuration and writes a single JSON file that the
    app displays. Everything here is read-only: no setting is changed.

.PARAMETER OutputPath
    Where to write the JSON snapshot.

.EXAMPLE
    .\discover-system.ps1 -OutputPath ..\Data\current\system-snapshot.json
#>
[CmdletBinding()]
param(
    [string]$OutputPath
)

# Resolved in the body: Windows PowerShell 5.1 leaves $PSScriptRoot empty while it
# binds parameter defaults, so a default here would fail on a bare -File invocation.
$root = Split-Path -Parent $PSScriptRoot
if (-not $OutputPath) { $OutputPath = Join-Path $root 'Data\current\system-snapshot.json' }

$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'

function Get-RegValue {
    param([string]$Path, [string]$Name)
    try { (Get-ItemProperty -Path $Path -Name $Name -ErrorAction Stop).$Name } catch { $null }
}

function Get-CommandVersion {
    param([string]$Exe, [string[]]$Arguments = @('--version'))
    $cmd = Get-Command $Exe -ErrorAction SilentlyContinue
    if (-not $cmd) { return $null }
    try {
        $out = & $Exe @Arguments 2>&1 | Select-Object -First 1
        return ($out -as [string]).Trim()
    } catch { return $null }
}

# ---------------------------------------------------------------- hardware ---
$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$board = Get-CimInstance Win32_BaseBoard | Select-Object -First 1
$bios = Get-CimInstance Win32_BIOS | Select-Object -First 1
$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem
$memModules = Get-CimInstance Win32_PhysicalMemory
$gpus = Get-CimInstance Win32_VideoController
$disks = Get-CimInstance Win32_DiskDrive
$volumes = Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3"
$pageFile = Get-CimInstance Win32_PageFileUsage
$startup = Get-CimInstance Win32_StartupCommand

$memoryTotalGb = [math]::Round((($memModules | Measure-Object -Property Capacity -Sum).Sum) / 1GB, 0)
$memorySpeed = ($memModules | Select-Object -First 1).ConfiguredClockSpeed
$memoryRatedSpeed = ($memModules | Select-Object -First 1).Speed
$moduleCount = @($memModules).Count
$visibleMemoryGb = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)

# ---------------------------------------------------------------- windows ---
$installDate = if ($os.InstallDate) { $os.InstallDate.ToString('yyyy-MM-dd') } else { $null }
$uptime = if ($os.LastBootUpTime) {
    [math]::Round(((Get-Date) - $os.LastBootUpTime).TotalHours, 1)
} else { $null }

$activePlan = (powercfg /getactivescheme) -join ''
if ($activePlan -match 'GUID:\s*([0-9a-fA-F-]+)\s*\(([^)]+)\)') {
    $planGuid = $matches[1]
    $planName = $matches[2]
} else {
    $planGuid = $null
    $planName = $activePlan
}

$plans = @()
foreach ($line in (powercfg /list)) {
    if ($line -match 'GUID:\s*([0-9a-fA-F-]+)\s*\(([^)]+)\)') {
        $plans += [ordered]@{ id = $matches[1]; name = $matches[2]; active = ($matches[1] -eq $planGuid) }
    }
}

$gameMode = Get-RegValue 'HKCU:\Software\Microsoft\GameBar' 'AutoGameModeEnabled'
$gameBarEnabled = Get-RegValue 'HKCU:\Software\Microsoft\GameBar' 'UseNexusForGameBarEnabled'
$gameDvr = Get-RegValue 'HKCU:\System\GameConfigStore' 'GameDVR_Enabled'
$hags = Get-RegValue 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' 'HwSchMode'
$gpuPref = Get-RegValue 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences' 'DirectXUserGlobalSettings'
$memoryIntegrity = Get-RegValue 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity' 'Enabled'
$vbsRunning = Get-RegValue 'HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard' 'EnableVirtualizationBasedSecurity'
$hypervisorLaunch = (bcdedit /enum '{current}' | Select-String -Pattern 'hypervisorlaunchtype') -join ''
$longPaths = Get-RegValue 'HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem' 'LongPathsEnabled'
$defenderExclusions = try { @((Get-MpPreference).ExclusionPath) } catch { @() }
$defenderRealtime = try { (Get-MpPreference).DisableRealtimeMonitoring } catch { $null }

# live GPU numbers: Win32_VideoController.AdapterRAM is a signed 32-bit value and
# reads 4 GB on any card with more, so ask the NVIDIA driver instead.
$nvidiaGpus = @()
$smiLine = & nvidia-smi --query-gpu=name,driver_version,memory.total,memory.used,utilization.gpu,temperature.gpu --format=csv,noheader 2>$null
foreach ($line in @($smiLine)) {
    if (-not $line) { continue }
    $parts = $line -split ','
    if ($parts.Count -ge 6) {
        $nvidiaGpus += [ordered]@{
            name       = $parts[0].Trim()
            driver     = $parts[1].Trim()
            vramMb     = [int]($parts[2] -replace '[^0-9]', '')
            usedMb     = [int]($parts[3] -replace '[^0-9]', '')
            utilization= [int]($parts[4] -replace '[^0-9]', '')
            temperature= [int]($parts[5] -replace '[^0-9]', '')
        }
    }
}

# AI helpers that live outside PATH
$aiTools = [ordered]@{
    lmStudio      = (Test-Path (Join-Path $env:LOCALAPPDATA 'Programs\LM Studio'))
    ollamaDataDir = (Test-Path (Join-Path $env:USERPROFILE '.ollama\models'))
}

$pageFileInfo = @()
foreach ($pf in $pageFile) {
    $pageFileInfo += [ordered]@{
        name        = $pf.Name
        allocatedMb = $pf.AllocatedBaseSize
        peakMb      = $pf.PeakUsage
    }
}
$pageFileAutomatic = (Get-CimInstance Win32_ComputerSystem).AutomaticManagedPagefile

# ------------------------------------------------------------ dev tooling ---
$tooling = [ordered]@{
    dotnet = Get-CommandVersion 'dotnet'
    git    = Get-CommandVersion 'git'
    node   = Get-CommandVersion 'node'
    npm    = Get-CommandVersion 'npm'
    python = Get-CommandVersion 'python'
    pip    = Get-CommandVersion 'pip'
    docker = Get-CommandVersion 'docker' @('--version')
    code   = Get-CommandVersion 'code' @('--version')
    ollama = Get-CommandVersion 'ollama' @('--version')
    nvidia = Get-CommandVersion 'nvidia-smi' @('--query-gpu=name,driver_version,memory.total', '--format=csv,noheader')
}

$dotnetSdks = @()
try { $dotnetSdks = @(dotnet --list-sdks 2>$null | ForEach-Object { ($_ -split '\[')[0].Trim() }) } catch { }

$wslDistros = @()
try {
    $wslOut = wsl --list --quiet 2>$null
    if ($LASTEXITCODE -eq 0) { $wslDistros = @($wslOut | Where-Object { $_ -and $_ -notmatch '^\s*$' }) }
} catch { }

# --------------------------------------------------------------- assemble ---
$snapshot = [ordered]@{
    generatedAt    = (Get-Date).ToString('o')
    generatedLocal = (Get-Date).ToString('yyyy-MM-dd HH:mm')
    computerName   = $env:COMPUTERNAME
    userName       = $env:USERNAME

    identity   = [ordered]@{
        manufacturer = $cs.Manufacturer
        model        = $cs.Model
        board        = $board.Product
        boardVersion = $board.Version
        boardSerial  = $board.SerialNumber
        biosVendor   = $bios.Manufacturer
        biosVersion  = $bios.SMBIOSBIOSVersion
        biosDate     = if ($bios.ReleaseDate) { $bios.ReleaseDate.ToString('yyyy-MM-dd') } else { $null }
    }

    cpu        = [ordered]@{
        name      = ($cpu.Name -replace '\s+', ' ').Trim()
        cores     = $cpu.NumberOfCores
        threads   = $cpu.NumberOfLogicalProcessors
        baseMhz   = $cpu.MaxClockSpeed
        socket    = $cpu.SocketDesignation
        l3CacheMb = [math]::Round($cpu.L3CacheSize / 1KB, 0)
    }

    memory     = [ordered]@{
        installedGb  = $memoryTotalGb
        visibleGb    = $visibleMemoryGb
        moduleCount  = $moduleCount
        configuredMt = $memorySpeed
        ratedMt      = $memoryRatedSpeed
        modules      = @($memModules | ForEach-Object {
            [ordered]@{
                slot    = $_.DeviceLocator
                sizeGb  = [math]::Round($_.Capacity / 1GB, 0)
                speedMt = $_.ConfiguredClockSpeed
                part    = ($_.PartNumber -replace '\s+', ' ').Trim()
            }
        })
    }

    graphics   = [ordered]@{
        adapters = @($gpus | ForEach-Object {
            $adapterName = $_.Name
            $smi = $nvidiaGpus | Where-Object { $adapterName -like "*$($_.name)*" } | Select-Object -First 1
            [ordered]@{
                name          = $adapterName
                isVirtual     = ($adapterName -match 'Virtual|Remote|Basic Display|Mirror')
                vramGb        = if ($smi) { [math]::Round($smi.vramMb / 1024, 1) }
                                elseif ($_.AdapterRAM) { [math]::Round($_.AdapterRAM / 1GB, 1) }
                                else { $null }
                driverVersion = $_.DriverVersion
                driverDate    = if ($_.DriverDate) { $_.DriverDate.ToString('yyyy-MM-dd') } else { $null }
                currentWidth  = $_.CurrentHorizontalResolution
                currentHeight = $_.CurrentVerticalResolution
                refreshHz     = $_.CurrentRefreshRate
            }
        })
        nvidia  = $nvidiaGpus
    }

    storage    = [ordered]@{
        drives  = @($disks | ForEach-Object {
            [ordered]@{
                model     = $_.Model
                sizeGb    = [math]::Round($_.Size / 1GB, 0)
                interface = $_.InterfaceType
                mediaType = $_.MediaType
            }
        })
        volumes = @($volumes | ForEach-Object {
            [ordered]@{
                letter     = $_.DeviceID
                label      = $_.VolumeName
                sizeGb     = [math]::Round($_.Size / 1GB, 0)
                freeGb     = [math]::Round($_.FreeSpace / 1GB, 0)
                fileSystem = $_.FileSystem
            }
        })
    }

    windows    = [ordered]@{
        edition        = $os.Caption
        version        = $os.Version
        build          = $os.BuildNumber
        architecture   = $os.OSArchitecture
        displayVersion = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name DisplayVersion).DisplayVersion
        installDate    = $installDate
        lastBootHours  = $uptime
        systemDrive    = $env:SystemDrive
        longPaths      = [bool]$longPaths
        pageFile       = [ordered]@{
            automatic = [bool]$pageFileAutomatic
            files     = $pageFileInfo
        }
        startupItems   = @($startup | ForEach-Object {
            [ordered]@{ name = $_.Name; command = $_.Command; location = $_.Location }
        })
    }

    performance = [ordered]@{
        powerPlan             = $planName
        powerPlanGuid         = $planGuid
        powerPlans            = $plans
        gameMode              = if ($null -eq $gameMode) { $null } else { [bool]$gameMode }
        gameDvrEnabled        = if ($null -eq $gameDvr) { $null } else { [bool]$gameDvr }
        hardwareScheduling    = if ($hags -eq 2) { $true } elseif ($null -eq $hags) { $null } else { $false }
        hardwareSchedulingRaw = $hags
        memoryIntegrity       = if ($memoryIntegrity -eq 1) { $true } elseif ($null -eq $memoryIntegrity) { $null } else { $false }
        vbsConfigured         = if ($vbsRunning -eq 1) { $true } elseif ($null -eq $vbsRunning) { $null } else { $false }
        hypervisorLaunch      = $hypervisorLaunch
        defender              = [ordered]@{
            realtimeDisabled = $defenderRealtime
            exclusionCount   = @($defenderExclusions).Count
            exclusions       = @($defenderExclusions | Where-Object { $_ })
        }
    }

    tooling    = $tooling
    aiTools    = $aiTools
    dotnetSdks = $dotnetSdks
    wslDistros = $wslDistros

    environment = [ordered]@{
        tempPath       = $env:TEMP
        processorCount = $env:NUMBER_OF_PROCESSORS
    }
}
$directory = Split-Path -Parent $OutputPath
if (-not (Test-Path $directory)) { New-Item -ItemType Directory -Path $directory -Force | Out-Null }
$snapshot | ConvertTo-Json -Depth 8 | Set-Content -Path $OutputPath -Encoding UTF8

# Keep a dated copy of every reading so past ones can be compared later.
$historyDir = Join-Path $root 'Data\history'
try {
    $generatedLocal = $null
    try {
        $generatedLocal = (Get-Content -Path $OutputPath -Raw -ErrorAction Stop | ConvertFrom-Json).generatedLocal
    } catch { }

    $stamp = Get-Date
    if ($generatedLocal) {
        $parsed = [datetime]::MinValue
        $parsedOk = [datetime]::TryParseExact(
            [string]$generatedLocal,
            'yyyy-MM-dd HH:mm',
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::None,
            [ref]$parsed)
        if ($parsedOk) { $stamp = $parsed }
    }

    if (-not (Test-Path -Path $historyDir)) {
        New-Item -ItemType Directory -Path $historyDir -Force -ErrorAction Stop | Out-Null
    }

    $baseName = 'scan-' + $stamp.ToString('yyyy-MM-dd-HHmm')
    $historyPath = Join-Path $historyDir ($baseName + '.json')
    $counter = 2
    while (Test-Path -Path $historyPath) {
        $historyPath = Join-Path $historyDir ($baseName + '-' + $counter + '.json')
        $counter++
    }
    Copy-Item -Path $OutputPath -Destination $historyPath -ErrorAction Stop
    Write-Output "History $historyPath"
} catch {
    Write-Warning ("Could not keep a history copy of {0}: {1}" -f $OutputPath, $_.Exception.Message)
}

Write-Output "Wrote $OutputPath"
Write-Output ("CPU {0} | {1} cores / {2} threads" -f $snapshot.cpu.name, $snapshot.cpu.cores, $snapshot.cpu.threads)
Write-Output ("RAM {0} GB in {1} modules @ {2} MT/s" -f $snapshot.memory.installedGb, $snapshot.memory.moduleCount, $snapshot.memory.configuredMt)
Write-Output ("GPU {0}" -f (($gpus | Select-Object -First 1).Name))
Write-Output ("Windows {0} build {1}" -f $os.Caption, $os.BuildNumber)
Write-Output ("Power plan {0}" -f $planName)
