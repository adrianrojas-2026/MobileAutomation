# ==============================================================================
# Frees disk space held by the emulators' rebuildable state. Nothing here breaks an AVD: the emulator recreates
# every file it deletes on the next start.
#
#   .\Setup\Clear-EmulatorCache.ps1 -WhatIf                 # show what would go and how big it is
#   .\Setup\Clear-EmulatorCache.ps1                         # Quick Boot snapshots + temp/crash files, all AVDs
#   .\Setup\Clear-EmulatorCache.ps1 -Avd Pixel_8,Pixel_9    # only these AVDs
#   .\Setup\Clear-EmulatorCache.ps1 -WipeData               # also factory-reset the AVDs (biggest saving)
#
# Snapshots: the next start is a cold boot (about a minute longer). Quick Boot saves a new snapshot
# (2-4 GB per AVD) when the emulator closes, unless you start it with -no-snapshot-save.
# -WipeData: same as "Wipe Data" in Device Manager. Installed apps, app data, granted permissions and the Google
# account sign-in are lost. Reinstall the apps and grant permissions before running the suite again.
# Stop the emulators first (.\Setup\Start-MobileGrid.ps1 -Stop): the files of a running AVD are in use.
# ==============================================================================
[CmdletBinding(SupportsShouldProcess)]
param(
    # AVD names (without .avd); all AVDs when empty
    [string[]]$Avd = @(),
    # Also delete user data and the cache partition (factory reset)
    [switch]$WipeData
)

$avdHome = if ($env:ANDROID_AVD_HOME) { $env:ANDROID_AVD_HOME } else { "$env:USERPROFILE\.android\avd" }
if (-not (Test-Path $avdHome)) {
    Write-Error "AVD folder not found: $avdHome"
    exit 1
}

$running = @(Get-Process -Name 'qemu-system-*', 'emulator' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Error "$($running.Count) emulator process(es) are running. Stop them first (.\Setup\Start-MobileGrid.ps1 -Stop)."
    exit 1
}

function Get-SizeBytes([string]$path) {
    $item = Get-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    if (-not $item) { return 0 }
    if (-not $item.PSIsContainer) { return $item.Length }
    $sum = (Get-ChildItem -LiteralPath $path -Recurse -Force -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    if ($sum) { return [long]$sum } else { return 0 }
}

$script:freed = [long]0
function Remove-Target([string]$path, [string]$what) {
    if (-not (Test-Path -LiteralPath $path)) { return }
    $size = Get-SizeBytes $path
    if ($PSCmdlet.ShouldProcess($path, "Delete $what ({0:N2} GB)" -f ($size / 1GB))) {
        Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction Continue
        if (-not (Test-Path -LiteralPath $path)) {
            $script:freed += $size
            Write-Host (" - {0,8:N2} GB  {1}" -f ($size / 1GB), $path) -ForegroundColor Gray
        }
    }
}

$avdDirs = Get-ChildItem -LiteralPath $avdHome -Directory -Filter '*.avd'
if ($Avd.Count -gt 0) {
    $missing = $Avd | Where-Object { -not (Test-Path (Join-Path $avdHome "$_.avd")) }
    if ($missing) { Write-Warning "Not found: $($missing -join ', ')" }
    $avdDirs = $avdDirs | Where-Object { $Avd -contains $_.BaseName }
}

if ($WipeData -and -not $WhatIfPreference) {
    Write-Warning "-WipeData factory-resets: $($avdDirs.BaseName -join ', '). Installed apps, permissions and Google sign-in will be lost."
}

foreach ($dir in $avdDirs) {
    Write-Host "$($dir.BaseName)" -ForegroundColor Cyan
    # Quick Boot (default_boot) and any saved snapshots
    Get-ChildItem -LiteralPath (Join-Path $dir.FullName 'snapshots') -Directory -Force -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Target $_.FullName 'snapshot' }
    # Leftovers of crashed or killed runs
    Get-ChildItem -LiteralPath $dir.FullName -Force -Filter '*.lock' -ErrorAction SilentlyContinue |
        ForEach-Object { Remove-Target $_.FullName 'lock' }
    Remove-Target (Join-Path $dir.FullName 'tmpAdbCmds') 'adb temp'

    if ($WipeData) {
        # userdata.img (the small seed copied from the system image) stays; the emulator rebuilds the rest from it
        Get-ChildItem -LiteralPath $dir.FullName -Force -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'userdata-qemu.img*' -or $_.Name -like 'cache.img*' } |
            ForEach-Object { Remove-Target $_.FullName 'user data' }
    }
}

# Emulator temp and crash dumps (shared by all AVDs)
Write-Host "Emulator temp / crash files" -ForegroundColor Cyan
Remove-Target (Join-Path $env:TEMP 'AndroidEmulator') 'emulator temp'
Remove-Target "$env:USERPROFILE\.android\breakpad" 'crash dumps'
Get-ChildItem -LiteralPath $env:TEMP -Force -Filter 'emu-crash-*' -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-Target $_.FullName 'crash dump' }

if (-not $WhatIfPreference) {
    Write-Host ("Freed {0:N2} GB." -f ($script:freed / 1GB)) -ForegroundColor Green
}
