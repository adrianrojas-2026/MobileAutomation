# RecoveryTrac Mobile Automation

End-to-end UI tests for **RecoveryTrac Flex** (collection / disposal monitor) and the **RecoveryTrac Driver App**,
written in C# with NUnit, Selenium and Appium (UiAutomator2). The same test runs in two modes:

| Mode | Flex runs in | Driver App runs in |
|---|---|---|
| **Web** | Chrome on `http://localhost:8100` | Edge on `http://localhost:8200` |
| **Mobile** | Node 1: `emulator-5554` (Android 14)<br>Node 2: `emulator-5558` (Android 15)<br>Node 3: `emulator-5562` (Android 17) | Node 1: `emulator-5556` (Android 12)<br>Node 2: `emulator-5560` (Android 13)<br>Node 3: `emulator-5564` (Android 16) |

Both modes run against the **local Aspire backend**.

## What the end-to-end test covers

`Tests/LoadTicketCollectionTests.cs` → `Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E`, as three test cases that run **in parallel**, one per node pair:

| Test case | Flex | Driver App | Flex account |
|---|---|---|---|
| `..._E2E_Node1_A14_A12` | `Grid_Node1_Flex` (Android 14) | `Grid_Node1_Driver` (Android 12) | `TestSettings:LoginEmail` |
| `..._E2E_Node2_A15_A13` | `Grid_Node2_Flex` (Android 15) | `Grid_Node2_Driver` (Android 13) | `TestSettings:Node2:LoginEmail` |
| `..._E2E_Node3_A17_A16` | `Grid_Node3_Flex` (Android 17) | `Grid_Node3_Driver` (Android 16) | `TestSettings:Node3:LoginEmail` |

Each node logs in with its **own Flex account**. With a shared account, each node would see the others' submitted tickets, and the "exactly one new ticket" check would fail. That's why Node 2 and Node 3 stop at setup with a clear message if their account isn't set. Only Node 1 uses the default account.

Each test case runs these steps:

1. **Flex: log in**, accept the Privacy Policy and Terms of Service, and check that a project is assigned.
2. **Flex: Load - Collection ticket.**
   - Truck: picks the first truck; the form fills in the truck's details.
   - Trailer: answers **No**.
   - Collection Start: adds the debris pile photo.
   - Collection Information: debris type **Vegetative/Woody**.
   - Location Information: captures the GPS location (mocked to Polk City Rd, FL) and waits for the street name.
   - Submits, generates the QR code and saves it, then waits for the ticket to reach **Synced**.
3. **Driver App:** GET STARTED → SKIP, then scans the collection QR. The scanned ticket becomes the **Current Load**.
4. **Flex: Load - Disposal ticket.**
   - Import Collection Data: scans the Driver's QR, then picks a disposal site.
   - Truck Container Load Call: Load Call **50%**, plus a photo.
   - Submits, waits for **Synced**, then generates the disposal QR and saves it.
5. **Driver App:** Scan Receipt Code with the disposal QR. The receipt must match the tickets: receipt number, truck, debris type, capacity and load call.

Generated QR codes are saved under `bin/Debug/net10.0/QrCodes/` (named with the node, e.g. `CollectionLoadTicket_Node2_<timestamp>.png`) and attached to the test result.

## BLE ticket handoff (TC-12 / TC-13)

`Tests/BleTicketHandoffTests.cs` → `BLE_TC12_TC13_CollectionTicket_SentViaBluetooth_IsLoadedOnDriver` (category `BLE`). This is Part B of `docs/testing/test-scenarios-ble-ticket-handoff.md`. The collection ticket goes from Flex to the Driver App **over Bluetooth instead of a QR scan**. The test runs only in Mobile mode; in Web mode it is skipped.

| Fixture | Node pair | Flex account |
|---|---|---|
| `BleTicketHandoffTests("Node2","testuser2")` | `Grid_Node2_Flex` (Android 15) + `Grid_Node2_Driver` (Android 13) | the configured account whose email is `…testuser2@…` |
| `BleTicketHandoffTests("Node3","testuser4")` | `Grid_Node3_Flex` (Android 17) + `Grid_Node3_Driver` (Android 16) | the configured account whose email is `…testuser4@…` |

The two fixtures run **in parallel** (`[Parallelizable(ParallelScope.Self)]`), each with its own Flex account. Neither sees the other's tickets, nor the tickets of Node 1's QR E2E, which uses `testuser1`. The account name is matched against the `LoginEmail` values in user-secrets (`TestSettings:LoginEmail` or any `TestSettings:<name>:LoginEmail`). Its section name doesn't matter, but the account must be set there.

**What each run covers:**

| Check | Covered by |
|---|---|
| **TC-12: happy path** | Flex sends a submitted *Load - Collection* ticket via Bluetooth. The Driver App shows it as its **Current Load**, with a *"Load <number> received."* toast |
| **TC-12: Nearby devices permission** | The Flex permission is reset before the run, so Android must ask *"Allow Flex to find nearby devices?"* the first time Flex uses Bluetooth. The test taps **Allow** and checks the permission is granted |
| **TC-12: truck code** | Flex lists the Driver App by the truck code that Driver App shows (`RTD-XXXX`). The status line must read **Sent to RTD-XXXX** |
| **TC-12: timing (check 2)** | Time from picking the truck to the load showing on the Driver App. Over **5 s** gives a **warning**, not a failure: emulator timings are indicative only, and sign-off happens on the demo phones |
| **TC-13: payload parity** | The Driver App's load QR decodes to exactly the same payload as the same ticket's QR in Flex. Both SHA-256 hashes are recorded |

**Steps:**

1. **Flex:** resets and relaunches Flex (Nearby devices permission revoked), logs in, then fills in and submits a *Load - Collection* ticket. Steps 1–17 use the same flow as the QR tests (`Flows/CollectionLoadTicketFlow.cs`).
2. **Flex:** declines the QR prompt and finds the ticket under **Submitted**, then waits for it to reach Synced.
3. **Driver App:** GET STARTED → SKIP and cancels any load left over from an earlier run. Then it taps **Receive via Bluetooth** (allowing Nearby devices if asked) and reads the truck code it advertises.
4. **Flex:** taps the submitted card's **Bluetooth** icon, allows Nearby devices, and the *Send via Bluetooth* picker opens.
5. **Flex:** waits for the truck code to be listed (pressing **Rescan** up to twice if a search ends without it), picks it, and waits for the final status.
6. **Driver App:** waits for the Current Load and reads the toast and the load's QR. Back in Flex, the test opens the same ticket's QR as the reference payload.

Each run saves its measurements to `bin/Debug/net10.0/Baselines/BLE-TC12_<node>_<timestamp>.json`: ticket, truck code, send result, `sentSeconds` / `loadedSeconds`, toast, payload length and both payload hashes.

**Not covered yet:** the disposal side of the BLE handoff (still in development), so the test ends once the Driver App holds the load. The ×10 iteration run and the "≥ 9 of 10 under 5 s" sign-off from the test plan are also out of scope. Emulator timings are indicative only.

**Shared Bluetooth:** all emulators share one simulated Bluetooth, so each Flex also lists the other node's Driver App. That's why each Flex picks its own Driver App by truck code rather than taking the first one listed.

## Project layout

| Path | Contents |
|---|---|
| `Tests/` | Test fixtures (`BaseTest` handles teardown and the HTML report) |
| `Pages/` | Flex page objects (`LoginPage`, `PrivacyPolicyPage`, `HomePage`, `FormsPage`, `CollectionLoadTicketFormPage`, `LoadDisposalFormPage`, ...) |
| `Pages/BleTruckPickerPage.cs` | Flex *Send via Bluetooth* picker: lists Driver Apps by truck code, Rescan, final send status |
| `Pages/Driver/` | Driver App page objects (`DriverOnboardingPage`, `DriverHomePage`, `DriverReceiptPage`). `DriverHomePage` also covers Receive via Bluetooth, the truck code and toasts |
| `Flows/` | Steps shared by tests: `CollectionLoadTicketFlow` (Flex login → submitted collection ticket), `DriverAppFlow` (Driver App onto Home, restart) |
| `Utilities/NearbyDevicesPermission.cs` | Resets, answers and checks Android's *Nearby devices* (Bluetooth) permission |
| `Utilities/BaselineRecorder.cs` | Saves a run's measurements as JSON under `Baselines/` |
| `Drivers/DriverFactory.cs` | Starts the browsers (Web) or the Appium sessions (Mobile); forwards backend ports to the emulators |
| `Utilities/FakeCameraFeed.cs` | Web: feeds an image (QR code) to the browser's fake camera |
| `Utilities/QrScanFeed.cs` | Hands a QR code to an app's scanner (Web: fake camera; Mobile: see [How Mobile mode handles the camera](#how-mobile-mode-handles-the-camera)) |
| `Utilities/AdbHelper.cs` | `adb` helpers: port forwarding and adding photos to the gallery |
| `Setup/Start-MobileGrid.ps1` | Starts the six node emulators on their fixed ports, then checks each one's Android version and installed APK |
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

  # Node 3's own Flex account
  dotnet user-secrets set TestSettings:Node3:LoginEmail "<third flex test user email>"
  dotnet user-secrets set TestSettings:Node3:LoginPassword "<third flex test user password>"
  ```

  A node uses `TestSettings:<node>:LoginEmail` / `LoginPassword` when that email is set. Node 1 falls back to the default account; Node 2 and Node 3 must have their own. Each account needs the same setup: a project and task assignment, and access to the *Load - Collection* and *Load - Disposal* forms.

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
| `MobileConfigurations:Grid_Node3_Flex` | Node 3 Flex emulator: `Udid` `emulator-5562`, Android 17 |
| `MobileConfigurations:Grid_Node3_Driver` | Node 3 Driver emulator: `Udid` `emulator-5564`, Android 16 |

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
| 3 | Flex | `emulator-5562` | 17 (API 37) | `Pixel_10_Pro` |
| 3 | Driver App | `emulator-5564` | 16 (API 36) | `Pixel_9_A16` |

Nodes 1 and 2 have passed full parallel runs in both Web and Mobile mode. Node 3 hasn't completed a run yet.

- **Start the grid with the script.** It starts each emulator on its own port, so the serial always matches `appsettings.json`. (Started by hand, the serial depends on start order: the first emulator gets 5554.) It stops if a port is taken by the wrong AVD. Once the emulators have booted, it reports each serial's Android version and whether its APK is installed:

  ```powershell
  .\Setup\Start-MobileGrid.ps1                # all 3 nodes, windows visible
  .\Setup\Start-MobileGrid.ps1 -Nodes 1,2     # only some nodes (saves RAM)
  .\Setup\Start-MobileGrid.ps1 -Headless      # no windows
  .\Setup\Start-MobileGrid.ps1 -Stop          # shut the grid down
  .\Setup\Start-MobileGrid.ps1 -MemoryMB 3072 # more RAM per emulator
  .\Setup\Start-MobileGrid.ps1 -Arrange       # lay out the running emulators' windows side by side again
  ```

  The script lays the emulator windows out side by side across your monitors, preferring monitors tall enough for a whole device. Each AVD otherwise opens where it was last, which can be stacked or off the top of a short screen.

  Every emulator starts with 2 GB of RAM and 2 CPU cores (`-MemoryMB`, `-Cores`), whatever its AVD says. That keeps all six within a 32 GB laptop. The script warns before starting an emulator when there isn't enough free memory for it.

  The AVD-to-port table is at the top of the script; keep it in sync with `appsettings.json`. To check a serial by hand:

  ```powershell
  adb devices
  adb -s emulator-5558 emu avd name
  adb -s emulator-5558 shell pm list packages | findstr tetratech
  ```

- **Memory:** four emulators already use most of a 32 GB machine, so six is tight. Close Android Studio and browsers before a parallel run, and start only the nodes you run (`-Nodes`). Frozen emulators or Appium session timeouts usually mean the host is out of RAM.

- **Back camera:** the Collection Start and Load Call photo fields need a live photo, so the Flex emulator must have a working camera. Pixel_8 uses `hw.camera.back=virtualscene`; a real webcam (`webcam0`) also works.
- **GPU:** `hw.gpu.mode=host` is set on the emulators (`config.ini` in `%USERPROFILE%\.android\avd\<name>.avd`).
- **Android 17 (`Pixel_10_Pro`)** uses a 16 KB page-size system image (`ps16k`). If Flex won't install or crashes on start there, a native library in the APK isn't 16 KB-aligned. That's a problem with the app build, not the tests.

#### 3. Debug APKs that talk to the local backend

The tests need **debug builds** of Flex and the Driver App that call the local Aspire services. Store or release builds won't work. Build and install them with:

```powershell
.\Setup\Build-DebugApk.ps1 -App Flex   -Udid emulator-5554   # builds and installs on Node 1 Flex
.\Setup\Build-DebugApk.ps1 -App Driver -Udid emulator-5556   # builds and installs on Node 1 Driver
```

The script's default serials (Flex `5556`, Driver `5554`) are from the older single-pair layout, so pass `-Udid`. To put the same build on Node 2 and Node 3, install the APK directly:

```powershell
$ui = "C:\Users\adrian.rojas\tetratec\Aspire\RecoveryTrac.UI\apps"
adb -s emulator-5558 install -r "$ui\Flex\android\app\build\outputs\apk\debug\app-debug.apk"
adb -s emulator-5560 install -r "$ui\Driver-App\android\app\build\outputs\apk\debug\app-debug.apk"
adb -s emulator-5562 install -r "$ui\Flex\android\app\build\outputs\apk\debug\app-debug.apk"
adb -s emulator-5564 install -r "$ui\Driver-App\android\app\build\outputs\apk\debug\app-debug.apk"
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
2. **The node emulators** are running on the serials in `appsettings.json`, with the debug APKs installed (Flex on 5554/5558/5562, Driver on 5556/5560/5564). `.\Setup\Start-MobileGrid.ps1` shows `OK` for each one when this is true.
3. **Appium** is running, in its own terminal:

   ```powershell
   appium --address 127.0.0.1 --port 4723 --allow-insecure chromedriver_autodownload
   ```

   The `chromedriver_autodownload` flag lets Appium download the chromedriver that matches each emulator's WebView. Newer Appium versions require the driver prefix: `--allow-insecure uiautomator2:chromedriver_autodownload`.
4. **Run mode:** `"RunMode": "Mobile"` in `Config/appsettings.json`.

### Run

```powershell
# All three nodes in parallel
dotnet test --filter "Name~LoadDisposalCollectionFlow" --logger "console;verbosity=detailed"

# Some nodes only
dotnet test --filter "Name~E2E_Node1|Name~E2E_Node2" --logger "console;verbosity=detailed"
dotnet test --filter "Name~E2E_Node3" --logger "console;verbosity=detailed"
```

The cases run at the same time: `[Parallelizable(ParallelScope.Children)]`, up to `LevelOfParallelism(3)` in `Properties/AssemblyInfo.cs`, which is exactly three nodes. Raise it if you add a fourth node. Each case uses the node pair it names, not the shared node pool. A run takes about **4–5 minutes** per node, and the parallel run takes about as long as the slower node. Leave the emulators alone while it runs.

**BLE handoff (TC-12 / TC-13)**, Node 2 and Node 3 in parallel. Only those two node pairs need to be running (`.\Setup\Start-MobileGrid.ps1 -Nodes 2,3`):

```powershell
dotnet test --filter "Category=BLE" --logger "console;verbosity=normal"
```

A run takes about **4 minutes** per node. On a pass, the console shows the truck code, the send status and the timing, for example:

```
🚚 Driver app is waiting for the monitor as RTD-JB03.
📡 Flex: 'Sent to RTD-JB03' after 3.1 s.
🚚 Driver app: current load 9T4F-LC-1007 after 3.4 s (toast: 'Load 9T4F-LC-1007 received.').
```

A run whose load took longer than 5 s ends with a **warning** outcome. The `dotnet test` summary doesn't count that as passed, but all of its checks held. The recorded JSON shows its numbers.

All nodes pick the **first truck**. If the backend ever allows only one active load per truck, every node but the first will fail at the Driver scan step.

### How Mobile mode handles the camera

| Step | Web | Mobile |
|---|---|---|
| Photos | Attached to the file input | Taken with the emulator camera (both fields block gallery uploads) |
| QR scans (Driver × 2, Flex × 1) | The browser's fake camera shows the QR | **Scanner hand-off** (below) |
| Location | Mocked browser GPS | GPS fix sent through the emulator console (see [Location](#location)) |

**Scanner hand-off (`Utilities/QrScanFeed.cs`):**
- **Why:** the emulator cameras can't show a chosen image on Android 12. The virtual scene camera only shows a black screen on Android 12 images, and a host virtual webcam such as OBS isn't visible to the emulator on Windows.
- **How:** the test reads the QR image with ZXing.Net. Inside the app's WebView it then answers the ML Kit `BarcodeScanner` plugin: `startScan` reports the QR text once through the normal `barcodesScanned` event, instead of opening the camera.
- **Coverage:** everything after the scan runs exactly as on a real device (validation, the load import, navigation). Only the camera reading the code is bypassed.
- **QR images are read from the page, not screenshotted.** The Driver App draws its load QR as a PNG data URL, and the test takes those exact bytes. On the Android 12 emulator, an element screenshot came out cropped by a few pixels: it clipped a corner square, and the code couldn't be decoded.

#### Location

`CollectionLoadTicketFormPage.CaptureLocation`:
- Appium's `mobile: setGeolocation` mock does **not** reach the app on these emulators. They keep reporting the default fix (Google HQ, 37.4220, -122.0840).
- So the test sends the fix through the emulator console instead: `adb -s <serial> emu geo fix <lon> <lat>`, right before tapping the GPS button and every 2 seconds while waiting. A console fix arrives as a real GPS reading.
- The location field only accepts a fix within 15 ft. The emulator's console fix sometimes reports 16 ft; the app then keeps the old location and offers **Use this location**. The test taps it rather than falling back to the map. The map opens on the centre of the US (Kansas), where the address lookup finds nothing.
- If Street Name stays empty (the address lookup failed), the test sends the fix again and taps GPS once more. If that also fails, it stops with a clear message instead of timing out.
- The address lookup for the mock point sometimes returns just *"Road"* as the street name. That passes, because the test compares the ticket with whatever the form filled in.

#### Other emulator-specific handling

- **Login:** on an emulator, the LOG IN tap sometimes only focuses the button. The test checks that the form went away and taps once more if needed. If it's still on the form, it fails and lists the error messages the app shows.
- **Driver App launch after a cold boot:** on the first session after an emulator boots, Appium's `am start` can hang and leave the launcher in front. `DriverAppFlow.StartOnHome` then opens the Driver App itself (*"Driver app not in front after the session started"*).
- **Driver App toasts:** a toast such as *"Load … received."* closes after a couple of seconds, and with two node pairs running it could be gone before the test read it. The BLE test therefore records toasts as the app shows them (`DriverHomePage.StartRecordingToasts`), starting just before it sends.
- **Driver App tutorial (Android 13+):** SKIP is drawn under the status bar and is missing from the native screen layout. The test recognises the tutorial by its slides and taps SKIP inside the app's WebView.
- **Assignment with a filled-in Position:** some accounts show *Position* instead of *Labor Category*. The test re-selects the Position so the assignment passes validation and the modal closes.

---

## Failure evidence and reports

- **Failure files:** when a test fails, the screen, page text and page source of each app are saved to `bin/Debug/net10.0/` as `Failure_<test>_<timestamp>_Flex.*` and `..._DriverApp.*`. `<test>` ends in the node name (e.g. `..._E2E_Node2_A15_A13`). They're also attached to the test result. The `.html` file is the most useful one for fixing locators.
- **Passwords are masked:** before the files are saved, the text of every password field, and the login password wherever it appears, is replaced with `********`. Failure files saved before this change may contain the password in plain text. Delete them and don't share them.
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
| Emulators freeze or sessions time out only in parallel runs | The host is out of RAM. Close other apps, start fewer nodes (`Start-MobileGrid.ps1 -Nodes 1,2`), and filter the run to those nodes |
| An emulator window is missing from the screen | It opened off-screen or behind another window. Run `.\Setup\Start-MobileGrid.ps1 -Arrange` |
| `Start-MobileGrid.ps1`: *... is not running at all: it failed to start* | Usually not enough free RAM for another emulator. Run fewer nodes (`-Nodes 1,2`), or close Android Studio, WSL and browsers |
| `Node3 has no Flex account of its own` | Set `TestSettings:Node3:LoginEmail` and `LoginPassword` in user-secrets (same for Node2) |
| `Start-MobileGrid.ps1`: *emulator-55xx is running 'X', not 'Y'* | The emulators were started on the wrong ports. Run `.\Setup\Start-MobileGrid.ps1 -Stop`, then start the grid again |
| `Login as '...' did not leave the Log In screen` | Wrong password for that node's account, or the backend can't be reached. The message lists the error text the app showed |
| *Street Name was not filled in ... (reverse geocoding failed twice)* | The emulator can't reach the address lookup (ArcGIS), or the GPS fix never arrived. Check the emulator's network, or run `adb -s <serial> emu geo fix -81.69 28.16` while the form is waiting |
| `No QR code could be read from the staged image` | The QR image couldn't be decoded. Check the `QrCodes/*.png` attachment for a cropped or blank image |
| Driver App stuck on the tutorial (Android 13+) | The WebView SKIP tap failed. Look for the SKIP button in the Driver failure `.html` |
| `Grid_Node2_Driver (emulator-5560) is not responding to adb` | That emulator isn't running. Check with `adb devices` and `adb -s <serial> emu avd name`. Emulators started by hand take the first free port, so an AVD can end up on another node's port. Fix it with `.\Setup\Start-MobileGrid.ps1 -Stop`, then start the grid with the script |
| `No configured Flex account matches 'testuser4'` | A BLE fixture names an account that isn't in user-secrets. Set it under any `TestSettings:<name>:LoginEmail` / `LoginPassword` |
| BLE: *Flex did not find the Driver app 'RTD-XXXX' after 3 Bluetooth searches* | The Driver App stopped advertising (it advertises for 2 minutes after Receive via Bluetooth), or the emulator's Bluetooth is off. The message lists the truck codes Flex did find |
| BLE: `sendResult` is *"Waiting for confirmation…"* or a failure message | Flex sent the ticket but the Driver App didn't confirm it. Check that the Driver emulator is responsive and run again |
| BLE: a fixture shows neither *Passed* nor *Failed* | The run passed every check but took over 5 s, so it ended with a warning. See `Baselines/BLE-TC12_<node>_*.json` |
