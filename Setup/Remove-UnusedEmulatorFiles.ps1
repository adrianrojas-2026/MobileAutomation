# ==============================================================================
# Removes emulator files that nothing uses:
#   - broken AVD folders (no config.ini, so the emulator can't list or start them)
#   - Android system images that no AVD is built on (Android Studio's SDK Manager can download them again)
#
#   .\Setup\Remove-UnusedEmulatorFiles.ps1 -WhatIf          # list what would go and how big it is
#   .\Setup\Remove-UnusedEmulatorFiles.ps1                  # delete it, asking for each item
#   .\Setup\Remove-UnusedEmulatorFiles.ps1 -Confirm:$false  # delete it without asking
#   .\Setup\Remove-UnusedEmulatorFiles.ps1 -RemoveAvd Old_Phone   # also delete these working AVDs
#
# Working AVDs are deleted only when you name them in -RemoveAvd. A system image becomes unused (and is removed)
# once the last AVD built on it is gone.
# ==============================================================================
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    # Working AVDs to delete as well (names without .avd)
    [string[]]$RemoveAvd = @()
)

$sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } elseif ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { "$env:LOCALAPPDATA\Android\Sdk" }
$avdHome = if ($env:ANDROID_AVD_HOME) { $env:ANDROID_AVD_HOME } else { "$env:USERPROFILE\.android\avd" }
$imagesRoot = Join-Path $sdk 'system-images'
if (-not (Test-Path $avdHome) -or -not (Test-Path $imagesRoot)) {
    Write-Error "AVD folder ($avdHome) or system images ($imagesRoot) not found. Set ANDROID_HOME to the Android SDK."
    exit 1
}

$running = @(Get-Process -Name 'qemu-system-*', 'emulator' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Error "$($running.Count) emulator process(es) are running. Stop them first (.\Setup\Start-MobileGrid.ps1 -Stop)."
    exit 1
}

function Get-SizeBytes([string]$path) {
    $sum = (Get-ChildItem -LiteralPath $path -Recurse -Force -File -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
    if ($sum) { return [long]$sum } else { return 0 }
}

$script:freed = [long]0
function Remove-Target([string]$path, [string]$what) {
    $size = Get-SizeBytes $path
    if ($PSCmdlet.ShouldProcess($path, "Delete $what ({0:N2} GB)" -f ($size / 1GB))) {
        Remove-Item -LiteralPath $path -Recurse -Force -ErrorAction Continue
        if (-not (Test-Path -LiteralPath $path)) {
            $script:freed += $size
            Write-Host (" - {0,8:N2} GB  {1}" -f ($size / 1GB), $path) -ForegroundColor Gray
            return $true
        }
    }
    return $false
}

# An AVD is "<name>.ini" (points at the folder) + "<name>.avd\config.ini"
function Remove-Avd([System.IO.DirectoryInfo]$dir, [string]$why) {
    Write-Host "$($dir.BaseName): $why" -ForegroundColor Cyan
    if (Remove-Target $dir.FullName 'AVD') {
        $ini = Join-Path $avdHome "$($dir.BaseName).ini"
        if (Test-Path $ini) { Remove-Item -LiteralPath $ini -Force }
    }
}

# 1. AVDs: broken ones, plus the ones named in -RemoveAvd
$missing = $RemoveAvd | Where-Object { -not (Test-Path (Join-Path $avdHome "$_.avd")) }
if ($missing) { Write-Warning "Not found: $($missing -join ', ')" }

foreach ($dir in Get-ChildItem -LiteralPath $avdHome -Directory -Filter '*.avd') {
    if (-not (Test-Path (Join-Path $dir.FullName 'config.ini'))) {
        Remove-Avd $dir 'broken (no config.ini)'
    } elseif ($RemoveAvd -contains $dir.BaseName) {
        Remove-Avd $dir 'named in -RemoveAvd'
    }
}

# 2. System images that no remaining AVD uses (image.sysdir.1 = system-images\android-34\google_apis_playstore\x86_64\)
$used = @{}
foreach ($config in Get-ChildItem -Path "$avdHome\*.avd\config.ini" -ErrorAction SilentlyContinue) {
    # Under -WhatIf the AVDs above still exist, so leave out the ones that would have been deleted
    if ($RemoveAvd -contains $config.Directory.BaseName) { continue }
    foreach ($line in Select-String -LiteralPath $config.FullName -Pattern '^image\.sysdir\.\d+\s*=\s*(.+)$') {
        $relative = $line.Matches[0].Groups[1].Value.Trim().TrimEnd('\', '/') -replace '/', '\'
        $used[(Join-Path $sdk $relative).ToLowerInvariant()] = $true
    }
}

Write-Host "System images" -ForegroundColor Cyan
# Layout: system-images\<api>\<tag>\<abi>
foreach ($abi in Get-ChildItem -LiteralPath $imagesRoot -Directory | Get-ChildItem -Directory | Get-ChildItem -Directory) {
    if ($used.ContainsKey($abi.FullName.ToLowerInvariant())) { continue }
    [void](Remove-Target $abi.FullName 'unused system image')
}
# Drop the api / tag folders the deletions left empty
if (-not $WhatIfPreference) {
    Get-ChildItem -LiteralPath $imagesRoot -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending |
        Where-Object { -not (Get-ChildItem -LiteralPath $_.FullName -Force) } |
        ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
}

if (-not $WhatIfPreference) {
    Write-Host ("Freed {0:N2} GB." -f ($script:freed / 1GB)) -ForegroundColor Green
}
