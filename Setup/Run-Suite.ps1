# ==============================================================================
# RUN THE MOBILE SUITE END TO END
# One entry point for a local run and for the Azure DevOps pipeline: backend, Appium, emulator grid, apps, tests,
# then the results (TRX, HTML report, baselines, failure screenshots) gathered into one folder.
#
#   .\Setup\Run-Suite.ps1                                         # Local Aspire, nodes 2,3, Category=BLE, apps as installed
#   .\Setup\Run-Suite.ps1 -RebuildApks                            # also build the debug APKs and install them on every node
#   .\Setup\Run-Suite.ps1 -Filter "Category=BLE-R1-C1" -Nodes 2
#   .\Setup\Run-Suite.ps1 -Target QA -FlexApk <flex.apk> -DriverApk <driver.apk>   # deployed QA services, no Aspire
#   .\Setup\Run-Suite.ps1 -Headless -Teardown                     # pipelines: no windows, stop what was started
#   .\Setup\Run-Suite.ps1 -Stop                                   # only stop the grid, Appium and Aspire started by a run
#
# Target Local starts the Aspire AppHost (Docker must be running) unless its services already answer, and the debug
# APKs call it through adb reverse. Target QA installs APKs built against the QA services (e.g. the QA pipelines'
# artifacts) and never starts Aspire. Login secrets come from user secrets locally, or from TestSettings__<key>
# environment variables (pipeline secret variables).
#
# Exit code: the 'dotnet test' exit code (non-zero when a test failed), or 1 when the setup failed.
# ==============================================================================
param(
    [ValidateSet("Local", "QA")][string]$Target = "Local",
    [string]$AspirePath = "C:\Users\adrian.rojas\tetratec\Aspire",
    # The UI repo's 'apps' folder; defaults to the Aspire submodule
    [string]$UiPath,
    [int[]]$Nodes = @(2, 3),
    [string]$Filter = "Category=BLE",
    [ValidateSet("Debug", "Release")][string]$Configuration = "Release",
    [string]$ResultsDirectory = (Join-Path $PSScriptRoot "..\TestResults\run"),
    # Local: build both debug APKs from the UI repo and install them on every node
    [switch]$RebuildApks,
    # QA (or a Local run with prebuilt APKs): installed on every node, replacing what is there
    [string]$FlexApk,
    [string]$DriverApk,
    [string]$JavaHome,
    [switch]$Headless,
    # Stop the grid, and Appium / Aspire when this run started them, once the tests are done
    [switch]$Teardown,
    [switch]$Stop,
    [int]$AspireTimeoutMinutes = 15,
    # Forwarded to the emulators but not served by the local AppHost: the Driver app's environment.ts calls
    # DriverService on 7013, while Aspire runs DriverService on 52006
    [int[]]$OptionalPorts = @(7013)
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$adbPath = "$env:ANDROID_HOME\platform-tools\adb.exe"
$appiumUrl = "http://127.0.0.1:4723"
if (-not $UiPath) { $UiPath = Join-Path $AspirePath "RecoveryTrac.UI\apps" }
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
# PIDs of what this run started, so -Stop (e.g. a pipeline's always() step) can stop them after a failed run
$stateFile = Join-Path $ResultsDirectory "run-suite.state.json"

$settings = Get-Content (Join-Path $repoRoot "Config\appsettings.json") -Raw | ConvertFrom-Json
$aspirePorts = @($settings.TestSettings.AspireServicePorts | Where-Object { $OptionalPorts -notcontains $_ })

function Write-Step([string]$text) { Write-Host "`n=== $text ===" -ForegroundColor Cyan }

function Test-Port([int]$port) {
    $client = [Net.Sockets.TcpClient]::new()
    # localhost, not 127.0.0.1: a service may listen on IPv6 only
    try { return $client.ConnectAsync("localhost", $port).Wait(1000) -and $client.Connected }
    catch { return $false }
    finally { $client.Dispose() }
}

function Test-Appium {
    try { return (Invoke-RestMethod "$appiumUrl/status" -TimeoutSec 3).value.ready -ne $false }
    catch { return $false }
}

# Serial of a node's Flex or Driver emulator, from the same appsettings.json the tests read
function Get-NodeUdid([int]$node, [string]$role) {
    $section = $settings.MobileConfigurations."Grid_Node$($node)_$role"
    if (-not $section) { throw "appsettings.json has no MobileConfigurations:Grid_Node$($node)_$role." }
    return $section.Udid
}

function Save-State([hashtable]$state) {
    New-Item -ItemType Directory -Force $ResultsDirectory | Out-Null
    $state | ConvertTo-Json | Set-Content $stateFile
}

function Stop-Started {
    if (Test-Path $stateFile) {
        $state = Get-Content $stateFile -Raw | ConvertFrom-Json
        foreach ($name in "AppiumPid", "AspirePid") {
            $id = $state.$name
            if ($id -and (Get-Process -Id $id -ErrorAction SilentlyContinue)) {
                Write-Host "Stopping $($name -replace 'Pid$') (PID $id)..." -ForegroundColor Yellow
                # The whole tree: the AppHost runs the services and DCP, which removes Aspire's containers
                & taskkill /PID $id /T /F | Out-Null
            }
        }
        Remove-Item $stateFile -Force
    }
    & (Join-Path $PSScriptRoot "Start-MobileGrid.ps1") -Nodes $Nodes -Stop
}

if ($Stop) {
    Stop-Started
    exit 0
}

if ($Target -eq "QA" -and -not ($FlexApk -and $DriverApk)) {
    throw "Target QA needs -FlexApk and -DriverApk (APKs built against the QA services)."
}
if ($RebuildApks -and ($FlexApk -or $DriverApk)) {
    throw "Use either -RebuildApks or -FlexApk/-DriverApk, not both."
}

New-Item -ItemType Directory -Force $ResultsDirectory | Out-Null
$state = @{}
$started = Get-Date
$testExitCode = 1

try {
    # --------------------------------------------------------------------------
    # 1. Backend
    # --------------------------------------------------------------------------
    if ($Target -eq "Local") {
        Write-Step "Aspire backend"
        $answering = @($aspirePorts | Where-Object { Test-Port $_ })
        $aspire = $null
        if ($answering.Count -eq $aspirePorts.Count) {
            Write-Host "All $($aspirePorts.Count) Aspire service ports already answer; using the running backend." -ForegroundColor Green
        }
        elseif ($answering.Count -gt 0) {
            # Some services answer: an AppHost is already running (or still starting). A second one would fight it
            # for the same ports and containers, so wait for it instead
            Write-Host "$($answering.Count) of $($aspirePorts.Count) Aspire service ports answer; waiting for the running AppHost." -ForegroundColor Yellow
        }
        else {
            & docker info *> $null
            if ($LASTEXITCODE -ne 0) { throw "Docker is not running. Aspire runs SQL Server, Redis and Liquibase as containers; start Docker and run this again." }

            $appHost = Join-Path $AspirePath "RecoveryTrac.AppHost"
            if (-not (Test-Path $appHost)) { throw "No RecoveryTrac.AppHost under '$AspirePath' (set -AspirePath)." }
            $aspireLog = Join-Path $ResultsDirectory "aspire.log"
            Write-Host "Starting the AppHost (log: $aspireLog)..."
            # Run from the AppHost folder, as its README says, so it picks up its own launch settings
            $aspire = Start-Process dotnet -ArgumentList "run --launch-profile https" -WorkingDirectory $appHost -PassThru -WindowStyle Hidden `
                -RedirectStandardOutput $aspireLog -RedirectStandardError (Join-Path $ResultsDirectory "aspire.err.log")
            $state.AspirePid = $aspire.Id
            Save-State $state
        }

        if ($answering.Count -lt $aspirePorts.Count) {
            $deadline = (Get-Date).AddMinutes($AspireTimeoutMinutes)
            do {
                if ($aspire -and $aspire.HasExited) { throw "The AppHost exited (code $($aspire.ExitCode)); see $ResultsDirectory\aspire.log." }
                Start-Sleep -Seconds 10
                $waiting = @($aspirePorts | Where-Object { -not (Test-Port $_) })
                Write-Host " ... waiting for $($waiting.Count) of $($aspirePorts.Count) service ports ($($waiting -join ', '))" -ForegroundColor Gray
            } while ($waiting.Count -gt 0 -and (Get-Date) -lt $deadline)
            if ($waiting.Count -gt 0) {
                throw "Aspire services not up after $AspireTimeoutMinutes min (ports $($waiting -join ', ')). Check the Aspire dashboard$(if ($aspire) { " or $ResultsDirectory\aspire.log" })."
            }
            Write-Host "Aspire is up." -ForegroundColor Green
        }
    }

    # --------------------------------------------------------------------------
    # 2. Appium
    # --------------------------------------------------------------------------
    Write-Step "Appium"
    if (Test-Appium) {
        Write-Host "Appium already answers on $appiumUrl." -ForegroundColor Green
    }
    else {
        # npm installs appium.ps1 and appium.cmd; Start-Process can only run the .cmd
        $appium = (Get-Command appium.cmd -ErrorAction Stop).Source
        # Appium 3 only accepts insecure features scoped to a driver
        $major = [int](((& $appium --version) | Select-Object -Last 1).Trim().Split('.')[0])
        $insecure = if ($major -ge 3) { "uiautomator2:chromedriver_autodownload" } else { "chromedriver_autodownload" }
        $appiumLog = Join-Path $ResultsDirectory "appium.log"
        Write-Host "Starting Appium $major.x (log: $appiumLog)..."
        $appiumProcess = Start-Process $appium -ArgumentList "--address 127.0.0.1 --port 4723 --allow-insecure $insecure" -PassThru -WindowStyle Hidden `
            -RedirectStandardOutput $appiumLog -RedirectStandardError (Join-Path $ResultsDirectory "appium.err.log")
        $state.AppiumPid = $appiumProcess.Id
        Save-State $state

        $deadline = (Get-Date).AddSeconds(60)
        while (-not (Test-Appium)) {
            if ($appiumProcess.HasExited -or (Get-Date) -gt $deadline) { throw "Appium did not start on $appiumUrl; see $appiumLog." }
            Start-Sleep -Seconds 2
        }
        Write-Host "Appium is up." -ForegroundColor Green
    }

    # --------------------------------------------------------------------------
    # 3. Emulator grid
    # --------------------------------------------------------------------------
    Write-Step "Emulator grid (nodes $($Nodes -join ', '))"
    & (Join-Path $PSScriptRoot "Start-MobileGrid.ps1") -Nodes $Nodes -Headless:$Headless
    if ($LASTEXITCODE -ne 0) { throw "The emulator grid did not start." }

    # --------------------------------------------------------------------------
    # 4. Apps
    # --------------------------------------------------------------------------
    $flexUdids = @($Nodes | ForEach-Object { Get-NodeUdid $_ "Flex" })
    $driverUdids = @($Nodes | ForEach-Object { Get-NodeUdid $_ "Driver" })

    if ($RebuildApks) {
        Write-Step "Debug APKs from $UiPath"
        $uiRoot = Split-Path $UiPath -Parent
        if (-not (Test-Path (Join-Path $uiRoot "node_modules"))) {
            Write-Host "Installing the UI repo's packages (fresh checkout)..."
            Push-Location $uiRoot
            try { pnpm install --frozen-lockfile; if ($LASTEXITCODE -ne 0) { throw "pnpm install failed." } }
            finally { Pop-Location }
        }
        $buildArgs = @{ UiPath = $UiPath }
        if ($JavaHome) { $buildArgs.JavaHome = $JavaHome }
        # Builds and installs on the first node; the others get the same APK below
        & (Join-Path $PSScriptRoot "Build-DebugApk.ps1") -App Flex -Udid $flexUdids[0] @buildArgs
        & (Join-Path $PSScriptRoot "Build-DebugApk.ps1") -App Driver -Udid $driverUdids[0] @buildArgs
        $FlexApk = Join-Path $UiPath "Flex\android\app\build\outputs\apk\debug\app-debug.apk"
        $DriverApk = Join-Path $UiPath "Driver-App\android\app\build\outputs\apk\debug\app-debug.apk"
        $flexUdids = @($flexUdids | Select-Object -Skip 1)
        $driverUdids = @($driverUdids | Select-Object -Skip 1)
    }

    if ($FlexApk -and $DriverApk) {
        Write-Step "Installing the apps"
        $installs = @($flexUdids | ForEach-Object { @{ Udid = $_; Apk = $FlexApk; Package = "com.tetratech.recoverytracflex" } }) +
                    @($driverUdids | ForEach-Object { @{ Udid = $_; Apk = $DriverApk; Package = "com.tetratech.recoverytracdriver" } })
        foreach ($install in $installs) {
            if (-not (Test-Path $install.Apk)) { throw "APK not found: $($install.Apk)" }
            # A build with another signature (debug vs. QA/release) can't be installed over the old one, so uninstall
            # first, except for a rebuilt debug APK, which keeps the app data like Build-DebugApk does
            if (-not $RebuildApks) { & $adbPath -s $install.Udid uninstall $install.Package *> $null }
            Write-Host "$($install.Udid): installing $($install.Apk)..."
            & $adbPath -s $install.Udid install -r $install.Apk
            if ($LASTEXITCODE -ne 0) { throw "Install failed on $($install.Udid)." }
        }
    }
    else {
        Write-Host "`nUsing the apps already installed on the emulators (pass -RebuildApks or -FlexApk/-DriverApk to replace them)." -ForegroundColor Gray
    }

    # --------------------------------------------------------------------------
    # 5. Tests
    # --------------------------------------------------------------------------
    Write-Step "Tests ($Filter)"
    & dotnet test (Join-Path $repoRoot "MobileAutomation.Tests.csproj") `
        --configuration $Configuration `
        --filter $Filter `
        --settings (Join-Path $PSScriptRoot "mobile.runsettings") `
        --results-directory $ResultsDirectory `
        --logger "trx;LogFileName=mobile-tests.trx" `
        --logger "console;verbosity=normal"
    $testExitCode = $LASTEXITCODE
}
catch {
    Write-Host "`n$($_.Exception.Message)" -ForegroundColor Red
    $testExitCode = 1
}
finally {
    # The tests write the HTML report, baselines and failure screenshots next to the test binaries; copy this run's
    # into the results folder so a pipeline publishes one place
    $output = Join-Path $repoRoot "bin\$Configuration\net10.0"
    if (Test-Path $output) {
        foreach ($folder in "Reports", "Baselines") {
            $files = @(Get-ChildItem (Join-Path $output $folder) -File -Recurse -ErrorAction SilentlyContinue | Where-Object LastWriteTime -ge $started)
            if ($files.Count -gt 0) {
                New-Item -ItemType Directory -Force (Join-Path $ResultsDirectory $folder) | Out-Null
                $files | Copy-Item -Destination (Join-Path $ResultsDirectory $folder) -Force
            }
        }
        Get-ChildItem $output -Filter "Failure_*.png" -File | Where-Object LastWriteTime -ge $started |
            Copy-Item -Destination $ResultsDirectory -Force
    }
    Write-Host "`nResults: $ResultsDirectory" -ForegroundColor Cyan

    if ($Teardown) {
        Write-Step "Teardown"
        Stop-Started
    }
}

exit $testExitCode
