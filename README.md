# RecoveryTrac Mobile Automation

End-to-end UI tests for **RecoveryTrac Flex** (collection / disposal monitor) and the **RecoveryTrac Driver App**,
written in C# with NUnit, Selenium and Appium (UiAutomator2). The same test runs in two modes:

| Mode | Flex runs in | Driver App runs in |
|---|---|---|
| **Web** | Chrome on `http://localhost:8100` | Edge on `http://localhost:8200` |
| **Mobile** | Node 1: `emulator-5554` (Android 14)<br>Node 2: `emulator-5558` (Android 15) | Node 1: `emulator-5556` (Android 12)<br>Node 2: `emulator-5560` (Android 13) |

Both modes run against the **local Aspire backend**.

## What the end-to-end test covers

`Tests/LoadTicketCollectionTests.cs` → `Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E`, as two test cases that run **in parallel**, one per node pair:

| Test case | Flex | Driver App | Flex account |
|---|---|---|---|
| `..._E2E_Node1_A14_A12` | `Grid_Node1_Flex` (Android 14) | `Grid_Node1_Driver` (Android 12) | `TestSettings:LoginEmail` |
| `..._E2E_Node2_A15_A13` | `Grid_Node2_Flex` (Android 15) | `Grid_Node2_Driver` (Android 13) | `TestSettings:Node2:LoginEmail` |

Each node logs in with its **own Flex account**. With a shared account, each node would see the other's submitted tickets, and the "exactly one new ticket" check would fail.

Each test case runs these steps:

1. **Flex: log in**, accept the Privacy Policy and Terms of Service, and check that a project is assigned.
2. **Flex: Load - Collection ticket.**
   - Truck: picks the first truck; the form fills in the truck's details.
   - Trailer: answers **No**.
   - Collection Start: adds the debris pile photo.
   - Collection Information: debris type **Vegetative/Woody**.
   - Location Information: captures the location.
   - Submits, generates the QR code and saves it, then waits for the ticket to reach **Synced**.
3. **Driver App:** GET STARTED → SKIP, then scans the collection QR. The scanned ticket becomes the **Current Load**.
4. **Flex: Load - Disposal ticket.**
   - Import Collection Data: scans the Driver's QR, then picks a disposal site.
   - Truck Container Load Call: Load Call **50%**, plus a photo.
   - Submits, waits for **Synced**, then generates the disposal QR and saves it.
5. **Driver App:** Scan Receipt Code with the disposal QR. The receipt must match the tickets: receipt number, truck, debris type, capacity and load call.

Generated QR codes are saved under `bin/Debug/net10.0/QrCodes/` (named with the node, e.g. `CollectionLoadTicket_Node2_<timestamp>.png`) and attached to the test result.

## Project layout

| Path | Contents |
|---|---|
| `Tests/` | Test fixtures (`BaseTest` handles teardown and the HTML report) |
| `Pages/` | Flex page objects (`LoginPage`, `PrivacyPolicyPage`, `HomePage`, `FormsPage`, `CollectionLoadTicketFormPage`, `LoadDisposalFormPage`, ...) |
| `Pages/Driver/` | Driver App page objects (`DriverOnboardingPage`, `DriverHomePage`, `DriverReceiptPage`) |
| `Drivers/DriverFactory.cs` | Starts the browsers (Web) or the Appium sessions (Mobile); forwards backend ports to the emulators |
| `Utilities/FakeCameraFeed.cs` | Web: feeds an image (QR code) to the browser's fake camera |
| `Utilities/QrScanFeed.cs` | Hands a QR code to an app's scanner (Web: fake camera; Mobile: see [How Mobile mode handles the camera](#how-mobile-mode-handles-the-camera)) |
| `Utilities/AdbHelper.cs` | `adb` helpers: port forwarding and adding photos to the gallery |
| `Setup/Build-DebugApk.ps1` | Builds and installs the Flex / Driver debug APKs against the local backend |
| `Config/appsettings.json` | Run mode, URLs, Appium and emulator settings |
| `TestData/` | Test images (e.g. `DebrisPile.png`) |

---

## Prerequisites (both modes)

- **.NET 10 SDK**
- **The RecoveryTrac Aspire solution running locally** (`RecoveryTrac.AppHost`). It starts the backend services, Flex on `localhost:8100` and the Driver App on `localhost:8200`. Check that both pages open in a browser before running the tests.
- **An ASP.NET Core HTTPS dev certificate** that's trusted (`dotnet dev-certs https --trust`). The local services use it.
- **Test user credentials** in user-secrets (run these in the test project folder):

  ```powershell
  dotnet user-secrets set TestSettings:LoginEmail "<flex test user email>"
  dotnet user-secrets set TestSettings:LoginPassword "<flex test user password>"

  # Node 2's own Flex account (e.g. Flex.testuser2@tetratech.com)
  dotnet user-secrets set TestSettings:Node2:LoginEmail "<second flex test user email>"
  dotnet user-secrets set TestSettings:Node2:LoginPassword "<second flex test user password>"
  ```

  A node uses `TestSettings:<node>:LoginEmail` / `LoginPassword` when that email is set; otherwise it uses the default account. Each account needs the same setup: a project and task assignment, and access to the *Load - Collection* and *Load - Disposal* forms.

## Configuration (`Config/appsettings.json`)

| Setting | Purpose |
|---|---|
| `TestSettings:RunMode` | `"Web"` or `"Mobile"` |
| `TestSettings:WebBaseUrl` / `DriverWebBaseUrl` | Flex and Driver App URLs for Web mode (`http://localhost:8100` / `8200`) |
| `TestSettings:AspireServicePorts` | Local service ports forwarded to the emulators in Mobile mode (see [Backend ports](#backend-ports)) |
| `Appium:ServerUrl` | Appium server (`http://127.0.0.1:4723`) |
| `MobileConfigurations:Grid_Node1_Flex` | Node 1 Flex emulator: `Udid` `emulator-5554`, Android 14 |
| `MobileConfigurations:Grid_Node1_Driver` | Node 1 Driver emulator: `Udid` `emulator-5556`, Android 12 |
| `MobileConfigurations:Grid_Node2_Flex` | Node 2 Flex emulator: `Udid` `emulator-5558`, Android 15 |
| `MobileConfigurations:Grid_Node2_Driver` | Node 2 Driver emulator: `Udid` `emulator-5560`, Android 13 |

Every `Grid_Node*` entry has its own `SystemPort` (820x), `mjpegServerPort` (830x) and `ChromedriverPort` (840x), so parallel sessions on the one Appium server don't share a port.

---

## Running in Web mode

### Requirements

- Google Chrome and Microsoft Edge installed. Selenium Manager downloads the matching drivers automatically.
- Aspire running (Flex on `8100`, Driver App on `8200`).

### Run

1. Set `"RunMode": "Web"` in `Config/appsettings.json`.
2. Run the test. Web mode doesn't use the node emulators, so run one case. Running both opens two Chrome and two Edge windows at once:

   ```powershell
   dotnet test --filter "Name~E2E_Node1" --logger "console;verbosity=detailed"
   ```

   Or run it from the IDE's Test Explorer.

Chrome (Flex) opens first. Edge (Driver App) opens once the collection QR code exists. A run takes about **2–3 minutes**.

**How Web mode handles the camera:** both browsers start with a fake camera that plays a file. Before each scan, the test writes the QR code to scan into that file. Photos are attached directly to the page's file input, and the location comes from a mocked browser GPS position.

---

## Running in Mobile mode

### One-time setup

#### 1. Tools

| Tool | Notes |
|---|---|
| Android Studio + Android SDK | `ANDROID_HOME` must point to the SDK (e.g. `%LOCALAPPDATA%\Android\Sdk`) |
| Appium 2.x + UiAutomator2 driver | `npm i -g appium` and `appium driver install uiautomator2` |
| **JDK 21** | For the Android Gradle builds. Android Studio's bundled JDK (Java 25) is **too new** and fails with *"Unsupported class file major version 69"* |
| Node.js 20 + pnpm | To build the apps' web bundles |

#### 2. Emulators

| Node | Role | Serial | Android | AVD |
|---|---|---|---|---|
| 1 | Flex | `emulator-5554` | 14 (API 34) | `Pixel_8` |
| 1 | Driver App | `emulator-5556` | 12 (API 31) | `Driver_A12_Play` |
| 2 | Flex | `emulator-5558` | 15 (API 35) | `Pixel_9` |
| 2 | Driver App | `emulator-5560` | 13 (API 33) | `Pixel_7_Android_13` |

Node 1 is the pair that has passed full runs. Node 2 hasn't completed a run yet.

- **Start each emulator on its port** so the serial always matches `appsettings.json`. Otherwise the serial depends on start order (the first emulator gets 5554):

  ```powershell
  $emu = "$env:ANDROID_HOME\emulator\emulator.exe"
  & $emu -avd Pixel_8            -port 5554 -no-audio -gpu host
  & $emu -avd Driver_A12_Play    -port 5556 -no-audio -gpu host
  & $emu -avd Pixel_9            -port 5558 -no-audio -gpu host
  & $emu -avd Pixel_7_Android_13 -port 5560 -no-audio -gpu host
  ```

  To check which AVD and app are on which serial:

  ```powershell
  adb devices
  adb -s emulator-5558 emu avd name
  adb -s emulator-5558 shell pm list packages | findstr tetratech
  ```

- **Memory:** four emulators use most of a 32 GB machine. Close Android Studio and browsers before a parallel run. Frozen emulators or Appium session timeouts usually mean the host is out of RAM.

- **Back camera:** the Collection Start and Load Call photo fields need a live photo, so the Flex emulator must have a working camera. Pixel_8 uses `hw.camera.back=virtualscene`; a real webcam (`webcam0`) also works.
- **GPU:** `hw.gpu.mode=host` is set on the emulators (`config.ini` in `%USERPROFILE%\.android\avd\<name>.avd`).

#### 3. Debug APKs that talk to the local backend

The tests need **debug builds** of Flex and the Driver App that call the local Aspire services. Store or release builds won't work. Build and install them with:

```powershell
.\Setup\Build-DebugApk.ps1 -App Flex   -Udid emulator-5554   # builds and installs on Node 1 Flex
.\Setup\Build-DebugApk.ps1 -App Driver -Udid emulator-5556   # builds and installs on Node 1 Driver
```

The script's default serials (Flex `5556`, Driver `5554`) are from the older single-pair layout, so pass `-Udid`. To put the same build on Node 2, install the APK directly:

```powershell
$ui = "C:\Users\adrian.rojas\tetratec\Aspire\RecoveryTrac.UI\apps"
adb -s emulator-5558 install -r "$ui\Flex\android\app\build\outputs\apk\debug\app-debug.apk"
adb -s emulator-5560 install -r "$ui\Driver-App\android\app\build\outputs\apk\debug\app-debug.apk"
```

The script expects the RecoveryTrac UI repo at `C:\Users\adrian.rojas\tetratec\Aspire\RecoveryTrac.UI` (override with `-UiPath`) and JDK 21 at the Eclipse Adoptium path (override with `-JavaHome`). It always does a **clean** Gradle build, because an incremental build once produced an APK that crashed on start (*ClassNotFoundException: MainActivity*).

What the script does:
- **Flex:** builds the web bundle with every service URL set to its local Aspire port (`NG_APP_*` variables, the same ones Aspire passes to web Flex), then `cap sync android`, `gradlew clean assembleDebug` and install.
- **Driver App:** its `environment.ts` already points at `https://localhost:7013` and `https://localhost:7107`, so it builds as is.

**Dev certificate (one-time, per machine):** the debug APKs must trust this machine's ASP.NET dev certificate to call `https://localhost:<port>`. Export it without the private key:

```powershell
dotnet dev-certs https -ep aspnet-dev.crt --format PEM --no-password   # then delete the generated .key file
```

Copy the certificate into the UI repo. **Never commit it**: it's specific to your machine.

| App | Certificate file | Trusted by |
|---|---|---|
| Flex | `apps/Flex/android/app/src/main/res/raw/aspnet_dev_cert.crt` | `res/xml/network_security_config.xml` (`debug-overrides`); the manifest points to it via `android:networkSecurityConfig` |
| Driver App | `apps/Driver-App/android/app/src/debug/res/raw/aspnetcore_dev_cert.crt` | The Driver App's existing debug source set (`src/debug/res/xml/network_security_config.xml`) |

If the installed app is a release build, uninstall it once before the first debug install (`adb -s <serial> uninstall <package>`), because the signatures differ.

#### Backend ports

Each emulator reaches the local services as `https://localhost:<port>` through `adb reverse`. **The test sets this up itself** for every port in `TestSettings:AspireServicePorts`, right after each emulator session starts.

| Port | Service |
|---|---|
| 52001 / 52002 / 52003 / 52004 | Activity / Attachment / Audit / Auth |
| 52007 / 52010 / 52011 / 52012 | Form / Lookup / Mobile / Notification |
| 52015 / 52017 / 52019 / 52020 / 52021 | Project / Role / TimeRecord / Time / User |
| 7013 | Driver service (Driver App) |
| 7107 | RecoveryTrac Web API |

If Aspire ever assigns different ports, update this list and the port table in `Setup/Build-DebugApk.ps1`. The real values are in the web bundle on `localhost:8100` (search it for `localhost:52`). If a port is missing, the app shows *"Unable to reach server"* at login.

### Before each Mobile run

1. **Aspire** is running.
2. **All four emulators** are running on the serials in `appsettings.json`, with the debug APKs installed (Flex on 5554/5558, Driver on 5556/5560).
3. **Appium** is running, in its own terminal:

   ```powershell
   appium --address 127.0.0.1 --port 4723 --allow-insecure chromedriver_autodownload
   ```

   The `chromedriver_autodownload` flag lets Appium download the chromedriver that matches each emulator's WebView. Newer Appium versions require the driver prefix: `--allow-insecure uiautomator2:chromedriver_autodownload`.
4. **Run mode:** `"RunMode": "Mobile"` in `Config/appsettings.json`.

### Run

```powershell
# Both nodes in parallel
dotnet test --filter "Name~LoadDisposalCollectionFlow" --logger "console;verbosity=detailed"

# One node only
dotnet test --filter "Name~E2E_Node2" --logger "console;verbosity=detailed"
```

The two cases run at the same time (`[Parallelizable(ParallelScope.Children)]`, up to `LevelOfParallelism(3)` in `Properties/AssemblyInfo.cs`). Each case uses the node pair it names, not the shared node pool. A run takes about **4–5 minutes** per node, and the parallel run takes about as long as the slower node. Leave the emulators alone while it runs.

Both nodes pick the **first truck**. If the backend allows only one active load per truck, one node fails at the Driver scan step.

### How Mobile mode handles the camera

| Step | Web | Mobile |
|---|---|---|
| Photos | Attached to the file input | Taken with the emulator camera (both fields block gallery uploads) |
| QR scans (Driver × 2, Flex × 1) | The browser's fake camera shows the QR | **Scanner hand-off** (below) |
| Location | Mocked browser GPS | GPS mock; if the app rejects the fix (it needs ≤ 15 ft accuracy), the location is picked on the map |

**Scanner hand-off (`Utilities/QrScanFeed.cs`):**
- **Why:** the emulator cameras can't show a chosen image on Android 12. The virtual scene camera only shows a black screen on Android 12 images, and a host virtual webcam such as OBS isn't visible to the emulator on Windows.
- **How:** the test reads the QR image with ZXing.Net. Inside the app's WebView it then answers the ML Kit `BarcodeScanner` plugin: `startScan` reports the QR text once through the normal `barcodesScanned` event, instead of opening the camera.
- **Coverage:** everything after the scan runs exactly as on a real device (validation, the load import, navigation). Only the camera reading the code is bypassed.

---

## Failure evidence and reports

- **Failure files:** when a test fails, the screen, page text and page source of each app are saved to `bin/Debug/net10.0/` as `Failure_<test>_<timestamp>_Flex.*` and `..._DriverApp.*`. `<test>` ends in the node name (e.g. `..._E2E_Node2_A15_A13`). They're also attached to the test result. The `.html` file is the most useful one for fixing locators.
- **HTML report:** generated after each run by `HtmlReportGenerator`.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `Activity name '.com.tetratech.recoverytracflex.MainActivity' ... doesn't exist` | The emulator serials don't match the installed apps. Check `adb devices` and the `Udid` values in `appsettings.json` |
| `No connection could be made ... 127.0.0.1:4723` | Appium isn't running |
| `UiAutomator2 Server cannot start because the local port #8201 is busy` | A stopped or aborted run left its Appium session behind. Restart Appium and wait about a minute |
| Login shows *"Unable to reach server"* | Aspire isn't running, or a service port is missing from `AspireServicePorts` |
| Driver App crashes on start (`ClassNotFoundException`) | Rebuild with `Setup/Build-DebugApk.ps1 -App Driver` (clean build) |
| Gradle: *"Unsupported class file major version 69"* | Use JDK 21 (`-JavaHome`), not Android Studio's bundled JDK |
| Build: *"file is being used by another process"* | A debug session is holding the test DLL. Stop it (Shift+F5) |
| Driver App still has a load from an earlier run | The test cancels it automatically (it waits out the CONFIRM countdown) |
| `LoginPassword setting is missing ... TestSettings:Node2:LoginPassword` | Node 2's email is set but its password isn't. Set it with `dotnet user-secrets set TestSettings:Node2:LoginPassword <value>` |
| Node 2 fails at login or finds no project | The Node 2 account isn't set up like the Node 1 account (project/task assignment, form access) |
| Emulators freeze or sessions time out only in parallel runs | The host is out of RAM. Close other apps, or run one node at a time |
