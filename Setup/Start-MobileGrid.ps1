# ==============================================================================
# RUNTIME EMULATOR GRID BOOTSTRAPPER (WITH LOCAL UI / WINDOW SWITCH)
# ==============================================================================
$emulatorPath = "$env:ANDROID_HOME\emulator\emulator.exe"
$adbPath = "$env:ANDROID_HOME\platform-tools\adb.exe"

# 💡 TOGGLE THIS VALUE: true for pipeline headless mode | false to see the UI Windows locally!
$headlessMode = false

# Define the exact emulators created by your setup script and map them to sequential ports
$emulatorsToBoot = @(
    @("Android_12",   "5554"),
    @("Android_14",   "5556"),
    @("Android_8",    "5558"),
    @("Pixel_4a",     "5560"),
    @("Pixel_6",      "5562"),
    @("Small_Tablet", "5564")
)

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "   LAUNCHING ANDROID EMULATOR TESTING GRID (UI:(-not headlessMode))   " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

foreach ($emu in $emulatorsToBoot) {
    $name = $emu[0]
    $port = $emu[1]
    $targetUdid = "emulator-$port"

    Write-Host "Checking if \(name (\)targetUdid) is already running..." -ForegroundColor Yellow
    
    $deviceStatus = & $adbPath devices | Select-String -SimpleMatch $targetUdid
    
    if ($deviceStatus) {
        Write-Host " -> name (targetUdid) is already online. Skipping boot." -ForegroundColor Green
    } else {
        # Dynamic Arguments based on your switch
        if ($headlessMode) {
            Write-Host " -> Booting $name headlessly for pipeline execution..." -ForegroundColor Cyan
            $arguments = "-avd `"$name`" -port $port -no-window -no-audio -no-boot-anim -gpu swiftshader_indirect"
        } else {
            Write-Host " -> Booting $name with FULL UI WINDOW for local debugging..." -ForegroundColor Green
            $arguments = "-avd `"$name`" -port $port -no-audio -gpu host"
        }
        
        Start-Process -FilePath $emulatorPath -ArgumentList $arguments -WindowStyle Hidden
    }
}

Write-Host "`nWaiting for all 6 emulators to complete boot loops safely..." -ForegroundColor Cyan

$timeoutSeconds = 180
$elapsedSeconds = 0
$checkInterval = 10

while ($elapsedSeconds -lt $timeoutSeconds) {
    $allReady = $true
    
    foreach ($emu in $emulatorsToBoot) {
        $port = $emu[1]
        $targetUdid = "emulator-$port"
        
        $state = & $adbPath -s $targetUdid get-state 2>$null
        $bootAnim = & $adbPath -s $targetUdid shell getprop sys.boot_completed 2>$null
        
        if ($state -eq "device" -and $bootAnim -and $bootAnim.Trim() -eq "1") {
            # This emulator is ready
        } else {
            $allReady = $false
        }
    }

    if ($allReady) {
        Write-Host "`n[SUCCESS] All 6 emulators are fully booted and responsive!" -ForegroundColor Green
        break
    }

    Start-Sleep -Seconds $checkInterval
    $elapsedSeconds += $checkInterval
    Write-Host " -> Grid status: Booting in progress... (${elapsedSeconds}/${timeoutSeconds}s elapsed)" -ForegroundColor Gray
}

if (-not $allReady) {
    Write-Error "Grid timeout reached! One or more emulators failed to initialize."
    exit 1
}

Write-Host "`nActive Emulator Grid Status Breakdown:" -ForegroundColor Cyan
& $adbPath devices


# Since those 6 emulators are running silently in your system memory (-no-window), 
# don't forget to run your PowerShell terminal teardown command when you are completely finished coding for the day: 
# adb devices | ForEach-Object { if ($_ -match "emulator-\d+") { adb -s $matches[0] shell reboot -p } }
