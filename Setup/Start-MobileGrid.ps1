# ==============================================================================
# Starts the Mobile node grid: one Flex + one Driver App emulator per node, each on the fixed port that
# Config/appsettings.json (MobileConfigurations:Grid_Node<N>_*:Udid) expects.
#
#   .\Setup\Start-MobileGrid.ps1                  # all 3 nodes, emulator windows visible
#   .\Setup\Start-MobileGrid.ps1 -Nodes 1,2       # only nodes 1 and 2
#   .\Setup\Start-MobileGrid.ps1 -Headless        # no windows (pipelines)
#   .\Setup\Start-MobileGrid.ps1 -Stop            # shut the grid's emulators down
#   .\Setup\Start-MobileGrid.ps1 -Arrange         # just lay out the running emulators' windows side by side
#   .\Setup\Start-MobileGrid.ps1 -MemoryMB 3072   # more RAM per emulator (default 2048, overrides the AVD's)
# ==============================================================================
param(
    [int[]]$Nodes = @(1, 2, 3),
    [switch]$Headless,
    [switch]$Stop,
    # Only lay out the windows of emulators that are already running (nothing is started or stopped)
    [switch]$Arrange,
    [int]$BootTimeoutSeconds = 300,
    # Six emulators at their AVD defaults (up to 4 GB / 4 cores each) don't fit a 32 GB / 20-thread laptop
    # next to Aspire, Appium and the IDE. These override each AVD's RAM and cores for this launch only.
    [int]$MemoryMB = 2048,
    [int]$Cores = 2
)

$emulatorPath = "$env:ANDROID_HOME\emulator\emulator.exe"
$adbPath = "$env:ANDROID_HOME\platform-tools\adb.exe"

# Keep in sync with Config/appsettings.json and the README's emulator table
$grid = @(
    [pscustomobject]@{ Node = 1; Role = "Flex";   Avd = "Pixel_8";            Port = 5554; Android = "14"; Package = "com.tetratech.recoverytracflex" }
    [pscustomobject]@{ Node = 1; Role = "Driver"; Avd = "Driver_A12_Play";    Port = 5556; Android = "12"; Package = "com.tetratech.recoverytracdriver" }
    [pscustomobject]@{ Node = 2; Role = "Flex";   Avd = "Pixel_9";            Port = 5558; Android = "15"; Package = "com.tetratech.recoverytracflex" }
    [pscustomobject]@{ Node = 2; Role = "Driver"; Avd = "Pixel_7_Android_13"; Port = 5560; Android = "13"; Package = "com.tetratech.recoverytracdriver" }
    [pscustomobject]@{ Node = 3; Role = "Flex";   Avd = "Pixel_10_Pro";       Port = 5562; Android = "17"; Package = "com.tetratech.recoverytracflex" }
    [pscustomobject]@{ Node = 3; Role = "Driver"; Avd = "Pixel_9_A16";        Port = 5564; Android = "16"; Package = "com.tetratech.recoverytracdriver" }
) | Where-Object { $Nodes -contains $_.Node }

if (-not (Test-Path $emulatorPath) -or -not (Test-Path $adbPath)) {
    Write-Error "emulator.exe / adb.exe not found. Set ANDROID_HOME to the Android SDK (e.g. $env:LOCALAPPDATA\Android\Sdk)."
    exit 1
}

function Get-RunningAvd([string]$serial) {
    $state = & $adbPath -s $serial get-state 2>$null
    if ($state -ne "device") { return $null }
    # First line of 'emu avd name' is the AVD name, the second is "OK". The emulator console can answer
    # empty on a busy host, so fall back to the name Android reports (ro.kernel.* on older images).
    $name = & $adbPath -s $serial emu avd name 2>$null | Select-Object -First 1
    if (-not $name -or -not $name.Trim()) {
        $name = & $adbPath -s $serial shell getprop ro.boot.qemu.avd_name 2>$null
    }
    if (-not $name -or -not $name.Trim()) {
        $name = & $adbPath -s $serial shell getprop ro.kernel.qemu.avd_name 2>$null
    }
    if ($name) { return $name.Trim() } else { return "" }
}

# Every AVD remembers its own window position, so the windows land wherever they were last (often stacked, or
# with tall skins such as Pixel_10_Pro above the top of a short display). Lay them out side by side instead:
# left to right in grid order, filling each monitor (left to right, tallest-enough first) before the next.
function Set-EmulatorWindowLayout {
    if ($Headless) { return }
    Add-Type -AssemblyName System.Windows.Forms
    if (-not ('MobileGrid.Win32' -as [type])) {
        Add-Type -Namespace MobileGrid -Name Win32 -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
'@
    }

    $windows = foreach ($emu in $grid) {
        $process = Get-Process qemu-system-x86_64 -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowTitle -like "*$($emu.Avd):$($emu.Port)" } | Select-Object -First 1
        if ($process -and $process.MainWindowHandle -ne [IntPtr]::Zero) {
            $rect = New-Object MobileGrid.Win32+RECT
            [void][MobileGrid.Win32]::GetWindowRect($process.MainWindowHandle, [ref]$rect)
            [pscustomobject]@{ Emu = $emu; Handle = $process.MainWindowHandle; Width = $rect.Right - $rect.Left; Height = $rect.Bottom - $rect.Top }
        }
    }
    if (-not $windows) { return }

    # Prefer monitors tall enough for a whole device window; use the rest only if those run out of room
    $tallest = ($windows | Measure-Object Height -Maximum).Maximum
    $screens = @([System.Windows.Forms.Screen]::AllScreens | Sort-Object { $_.WorkingArea.Height -lt $tallest }, { $_.WorkingArea.Left })

    $screenIndex = 0
    $x = $screens[0].WorkingArea.Left
    foreach ($window in $windows) {
        # Move to the next monitor when this window doesn't fit on the current one; on the last monitor, overlap
        while ($screenIndex -lt $screens.Count - 1 -and $x + $window.Width -gt $screens[$screenIndex].WorkingArea.Right) {
            $screenIndex++
            $x = $screens[$screenIndex].WorkingArea.Left
        }
        $area = $screens[$screenIndex].WorkingArea
        [void][MobileGrid.Win32]::ShowWindow($window.Handle, 9) # SW_RESTORE, in case it was minimised
        # SWP_NOSIZE | SWP_NOZORDER
        [void][MobileGrid.Win32]::SetWindowPos($window.Handle, [IntPtr]::Zero, $x, $area.Top, 0, 0, 0x0001 -bor 0x0004)
        Write-Host (" - emulator-{0} ({1}) window -> {2} at x={3}" -f $window.Emu.Port, $window.Emu.Avd, $screens[$screenIndex].DeviceName.TrimStart('\', '.'), $x) -ForegroundColor Gray
        $x += $window.Width
    }
}

if ($Arrange) {
    Set-EmulatorWindowLayout
    exit 0
}

if ($Stop) {
    foreach ($emu in $grid) {
        $serial = "emulator-$($emu.Port)"
        # Stop whatever is on the port, even when its AVD name can't be read ("" is still running)
        if ($null -ne (Get-RunningAvd $serial)) {
            Write-Host "Stopping $serial ($($emu.Avd))..." -ForegroundColor Yellow
            & $adbPath -s $serial emu kill | Out-Null
        }
    }
    exit 0
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Mobile grid: nodes $($Nodes -join ', ')  ($(if ($Headless) { 'headless' } else { 'windows visible' }))" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

$existingAvds = & $emulatorPath -list-avds
$failed = $false

foreach ($emu in $grid) {
    $serial = "emulator-$($emu.Port)"
    $label = "Node $($emu.Node) $($emu.Role) ($($emu.Avd), Android $($emu.Android))"

    if ($existingAvds -notcontains $emu.Avd) {
        Write-Host " x $label : AVD '$($emu.Avd)' does not exist. Create it in Android Studio's Device Manager." -ForegroundColor Red
        $failed = $true
        continue
    }

    $running = Get-RunningAvd $serial
    if ($null -ne $running) {
        if ($running -eq $emu.Avd) {
            Write-Host " - $label is already running on $serial." -ForegroundColor Green
        } elseif ($running -eq "") {
            # Name unreadable: keep it; the Android version check at the end still catches a wrong AVD
            Write-Host " - $serial is running, but its AVD name can't be read; assuming $($emu.Avd)." -ForegroundColor Yellow
        } else {
            # Wrong emulator on this port: the tests would drive the wrong device/app
            Write-Host " x $serial is running '$running', not '$($emu.Avd)'. Stop it (.\Setup\Start-MobileGrid.ps1 -Stop) and run this again." -ForegroundColor Red
            $failed = $true
        }
        continue
    }

    # Free commit space (RAM + page file). An emulator started without room for its RAM boots very slowly
    # or hangs on "Starting up", so say so instead of failing later with a boot timeout.
    $freeMB = [int]((Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory / 1KB)
    if ($freeMB -lt $MemoryMB + 1024) {
        Write-Host " ! Only $freeMB MB of memory free before starting $($emu.Avd) (needs about $($MemoryMB + 1024) MB). It may boot slowly or hang; close other apps." -ForegroundColor Yellow
    }

    $common = "-avd `"$($emu.Avd)`" -port $($emu.Port) -memory $MemoryMB -cores $Cores -no-audio -no-boot-anim"
    $arguments = if ($Headless) {
        "$common -no-window -gpu swiftshader_indirect"
    } else {
        "$common -gpu host"
    }
    Write-Host " > Starting $label on $serial ($MemoryMB MB, $Cores cores)..." -ForegroundColor Cyan
    Start-Process -FilePath $emulatorPath -ArgumentList $arguments -WindowStyle Hidden
    # Starting several emulators at the same instant can fail on a busy host; stagger them a little
    Start-Sleep -Seconds 5
}

if ($failed) {
    Write-Error "The grid is not in the layout appsettings.json expects. Fix the items marked x above."
    exit 1
}

Write-Host "`nWaiting for $($grid.Count) emulators to finish booting (up to $BootTimeoutSeconds s)..." -ForegroundColor Cyan

$elapsed = 0
$interval = 10
do {
    $pending = @($grid | Where-Object {
        $boot = & $adbPath -s "emulator-$($_.Port)" shell getprop sys.boot_completed 2>$null
        -not ($boot -and $boot.Trim() -eq "1")
    })
    if ($pending.Count -eq 0) { break }
    Start-Sleep -Seconds $interval
    $elapsed += $interval
    Write-Host " ... still booting: $(($pending | ForEach-Object { "emulator-$($_.Port)" }) -join ', ') (${elapsed}s)" -ForegroundColor Gray
} while ($elapsed -lt $BootTimeoutSeconds)


Write-Host "`nArranging the emulator windows..." -ForegroundColor Cyan
Set-EmulatorWindowLayout

if ($pending.Count -gt 0) {
    # An emulator whose process is gone never got its RAM (most often) or hit an AVD error; say which
    foreach ($emu in $pending) {
        $alive = Get-Process qemu-system-x86_64 -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like "*$($emu.Avd):$($emu.Port)" }
        if (-not $alive) {
            Write-Host " x $($emu.Avd) (emulator-$($emu.Port)) is not running at all: it failed to start. Usually that's not enough free RAM; run fewer nodes (-Nodes) or close other apps, then run this again." -ForegroundColor Red
        }
    }
    Write-Error "Boot timeout: $(($pending | ForEach-Object { "emulator-$($_.Port)" }) -join ', ') did not finish booting."
    exit 1
}

# Final check: right Android version and the debug APK installed on every serial
Write-Host "`nGrid status:" -ForegroundColor Cyan
$problems = 0
foreach ($emu in $grid) {
    $serial = "emulator-$($emu.Port)"
    $release = (& $adbPath -s $serial shell getprop ro.build.version.release).Trim()
    $installed = [bool](& $adbPath -s $serial shell pm list packages $emu.Package | Select-String -SimpleMatch "package:$($emu.Package)")
    $notes = @()
    if ($release -ne $emu.Android) { $notes += "Android $release (expected $($emu.Android))" }
    if (-not $installed) { $notes += "$($emu.Package) NOT installed" }

    $line = " Node $($emu.Node) {0,-6} {1}  {2,-19} Android {3,-3}" -f $emu.Role, $serial, $emu.Avd, $release
    if ($notes.Count -eq 0) {
        Write-Host "$line OK" -ForegroundColor Green
    } else {
        Write-Host "$line $($notes -join '; ')" -ForegroundColor Yellow
        $problems++
    }
}

if ($problems -gt 0) {
    Write-Host "`nInstall the missing APKs before running (see the README, 'Debug APKs'):" -ForegroundColor Yellow
    Write-Host '  adb -s <serial> install -r "<UI repo>\apps\Flex\android\app\build\outputs\apk\debug\app-debug.apk"' -ForegroundColor Yellow
    Write-Host '  adb -s <serial> install -r "<UI repo>\apps\Driver-App\android\app\build\outputs\apk\debug\app-debug.apk"' -ForegroundColor Yellow
} else {
    Write-Host "`n[SUCCESS] Grid is ready." -ForegroundColor Green
}
