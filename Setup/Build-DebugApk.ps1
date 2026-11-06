# ==============================================================================
# DEBUG APKs AGAINST THE LOCAL ASPIRE BACKEND
# Builds RecoveryTrac Flex or Driver as a debug APK that calls the local Aspire services (the same URLs the web
# apps on localhost:8100 / 8200 use), installs it on its emulator and forwards those ports to it.
#
#   .\Build-DebugApk.ps1 -App Flex                  # build + install on emulator-5556 + forward ports
#   .\Build-DebugApk.ps1 -App Driver                # build + install on emulator-5554 + forward ports
#   .\Build-DebugApk.ps1 -App Flex -ForwardOnly     # only re-forward ports (the tests also do this per session)
#   .\Build-DebugApk.ps1 -App Flex -Force           # rebuild even when nothing changed since the last build
#
# The build is skipped (the existing APK is installed) when nothing it is built from changed since the last build:
# same files in the app, libs/ and the repo root (committed or not) and the same settings in this script.
#
# Requires: Aspire running, JDK 21 (the Android Gradle builds do not run on Java 25), and the app's debug network
# security config trusting this machine's ASP.NET dev cert (dotnet dev-certs https -ep <file>.crt --format PEM):
#   Flex:   android/app/src/main/res/xml/network_security_config.xml (debug-overrides) + res/raw/aspnet_dev_cert.crt
#   Driver: android/app/src/debug/res/xml/network_security_config.xml + src/debug/res/raw/aspnetcore_dev_cert.crt
# A release build already on the device has a different signature: uninstall it once before the first install.
# ==============================================================================
param(
    [Parameter(Mandatory)][ValidateSet("Flex", "Driver")][string]$App,
    [string]$UiPath = "C:\Users\adrian.rojas\tetratec\Aspire\RecoveryTrac.UI\apps",
    [string]$Udid,
    [string]$JavaHome = "C:\Program Files\Eclipse Adoptium\jdk-21.0.12.101-hotspot",
    [switch]$ForwardOnly,
    # Build even when the APK is up to date
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$buildScript = $PSCommandPath
$adbPath = "$env:ANDROID_HOME\platform-tools\adb.exe"

# Ports Aspire assigns to each service (check the web bundles on localhost:8100 / 8200 if they ever change)
$flexServices = [ordered]@{
    ActivityService     = 52001
    AttachmentService   = 52002
    AuditService        = 52003
    AuthService         = 52004
    FormService         = 52007
    LookupService       = 52010
    MobileService       = 52011
    NotificationService = 52012
    ProjectService      = 52015
    RoleService         = 52017
    TimeRecordService   = 52019
    TimeService         = 52020
    UserService         = 52021
    WebApi              = 7107
}

if ($App -eq "Flex") {
    $projectPath = Join-Path $UiPath "Flex"
    if (-not $Udid) { $Udid = "emulator-5556" }
    $ports = $flexServices.Values
}
else {
    # The Driver app's environment.ts already points at https://localhost:7013 (DriverService) and :7107 (WebApi)
    $projectPath = Join-Path $UiPath "Driver-App"
    if (-not $Udid) { $Udid = "emulator-5554" }
    $ports = @(7013, 7107)
}

function Set-PortForwarding {
    # The emulator's "localhost" is the emulator itself; adb reverse maps each service port back to this machine
    foreach ($port in $ports) {
        & $adbPath -s $Udid reverse "tcp:$port" "tcp:$port" | Out-Null
    }
    Write-Host "Forwarded $(@($ports).Count) Aspire ports to $Udid." -ForegroundColor Green
}

if ($ForwardOnly) {
    Set-PortForwarding
    return
}

# A hash of everything the APK is built from: the content of the app's files, libs/ and the repo root files, as
# committed, staged, modified or untracked, plus this script (service ports, build commands). Null when the UI
# repo is not a git checkout (always build then).
function Get-BuildFingerprint {
    $repo = git -C $projectPath rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $repo) { return $null }
    $appRel = "apps/" + (Split-Path $projectPath -Leaf)
    $pathspec = @('.') +
        @(Get-ChildItem (Join-Path $repo 'apps') -Directory | Where-Object { "apps/$($_.Name)" -ne $appRel } | ForEach-Object { ":(exclude)apps/$($_.Name)" }) +
        @(':(exclude)docs', ':(exclude)specs', ':(exclude)SPEC', ':(exclude)steering', ':(exclude).claude', ':(exclude).storybook', ':(exclude)*.md') +
        # Rewritten by 'cap sync' on every build
        @(":(exclude)$appRel/android/capacitor-cordova-android-plugins", ":(exclude)$appRel/android/capacitor.settings.gradle", ":(exclude)$appRel/android/app/capacitor.build.gradle")

    $parts = [System.Collections.Generic.List[string]]::new()
    $parts.Add($App)
    $parts.Add((Get-FileHash -LiteralPath $buildScript -Algorithm SHA256).Hash)
    # Index: one blob hash per tracked file
    $parts.Add((git -C $repo ls-files -s -- @pathspec | Out-String))
    # Working tree edits not staged yet
    $parts.Add((git -C $repo -c core.safecrlf=false diff --binary -- @pathspec 2>$null | Out-String))
    # Untracked (not ignored) files: path + content hash
    $untracked = @(git -C $repo ls-files --others --exclude-standard -- @pathspec)
    if ($untracked.Count -gt 0) {
        $hashes = @($untracked | git -C $repo hash-object --stdin-paths)
        for ($i = 0; $i -lt $untracked.Count; $i++) { $parts.Add("$($untracked[$i]) $($hashes[$i])") }
    }

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($parts -join "`n"))).Replace('-', '') }
    finally { $sha.Dispose() }
}

$apk = Join-Path $projectPath "android\app\build\outputs\apk\debug\app-debug.apk"
# Inside build\, so 'gradlew clean' drops it together with the APK
$fingerprintFile = "$apk.fingerprint"
# Taken before building: a file edited while the build runs then counts as a change next time
$fingerprint = Get-BuildFingerprint
$upToDate = -not $Force -and $fingerprint -and (Test-Path $apk) -and (Test-Path $fingerprintFile) -and
    ((Get-Content -LiteralPath $fingerprintFile -Raw).Trim() -eq $fingerprint)

if ($upToDate) {
    Write-Host "$App APK is up to date (nothing changed since $((Get-Item $apk).LastWriteTime)); skipping the build. -Force rebuilds it." -ForegroundColor Green
}
else {
    Remove-Item -LiteralPath $fingerprintFile -ErrorAction SilentlyContinue
    Write-Host "Building $App web bundle against the local Aspire services..." -ForegroundColor Cyan
    Push-Location $projectPath
    try {
        if ($App -eq "Flex") {
            foreach ($service in $flexServices.Keys) {
                Set-Item "env:NG_APP_$service" "https://localhost:$($flexServices[$service])"
            }
            $env:NG_APP_Authorization__Issuance__Issuer = "urn:recoverytrac:issuance:driver-session"
            $env:NG_APP_Authorization__Issuance__Audience = "RecoveryTrac.API"
            pnpm run web-build-dev
        }
        else {
            pnpm run build
        }
        if ($LASTEXITCODE -ne 0) { throw "$App web build failed." }

        npx cap sync android
        if ($LASTEXITCODE -ne 0) { throw "Capacitor sync failed." }

        Write-Host "Building debug APK..." -ForegroundColor Cyan
        $env:JAVA_HOME = $JavaHome
        Push-Location android
        try {
            # Always clean: an incremental build after cap sync once produced an APK missing MainActivity
            .\gradlew.bat clean assembleDebug --console=plain
            if ($LASTEXITCODE -ne 0) { throw "Gradle assembleDebug failed." }
        }
        finally {
            Pop-Location
        }
    }
    finally {
        Pop-Location
    }
    if ($fingerprint) { Set-Content -LiteralPath $fingerprintFile -Value $fingerprint -NoNewline }
}

Write-Host "Installing $apk on $Udid..." -ForegroundColor Cyan
# -r keeps app data
& $adbPath -s $Udid install -r $apk
if ($LASTEXITCODE -ne 0) {
    $package = if ($App -eq "Flex") { "com.tetratech.recoverytracflex" } else { "com.tetratech.recoverytracdriver" }
    throw "APK install failed. If the installed app is a release build, run: adb -s $Udid uninstall $package"
}

Set-PortForwarding
Write-Host "Done: $apk" -ForegroundColor Green
