# Test Scenarios - BLE Ticket Handoff POC

**Source**: *BLE Enhancements POC: Requirements Summary* (FR-1…FR-7, demo checks 1–7) + current code in this repo
**Test plan**: [test-plan-ble-ticket-handoff.md](test-plan-ble-ticket-handoff.md) (TC IDs below match it)
**Created**: 2026-10-27 · **Author**: QA

## How to read this document

The BLE feature is still in development, so the scenarios come in two parts:

| Part | What | Runnable | Category |
|---|---|---|---|
| **A - QR baseline** | Records **today's** QR behavior, which BLE must match: payload, Driver state, second/duplicate ticket, rejection of bad data. Also guards that QR keeps working. | **Now**, on the current builds (Flex 4.10.2, Driver 1.2.0) | `QR` |
| **B - BLE handoff** | The new feature, FR-1…FR-7 and demo checks 1–7. Most B scenarios compare their result to an A baseline. | When the BLE builds land | `BLE`, `BLE-Device` |

Part A is not only regression. It produces the **expected results** for part B. For example, FR-6 says the Driver app must behave "as after a QR scan", and QR-B03 records what that is.

Status values: ✅ automated today · 🟢 ready to automate now · 🟡 automatable now with a small code change · 🔵 waiting for BLE build · ⛔ also blocked by a dev ask (Q-numbers, see test plan §10) · ✋ manual.

---

## Part A implementation status (2026-10-27, branch `feature/qr-baseline-ble-prep`)

**Final result (Node3, Flex A17 / Driver A16): 7 passed, 2 warnings (findings), 0 failed.** HTML report: `bin/Debug/net10.0/Reports/QrBaselineReport_latest.html`, written after every run. It keeps the latest result of each test, so re-running one test still gives a full report.

| Scenario | Result | Recorded baseline (expected result for the BLE test) |
|---|---|---|
| QR-A01 / A02 QR E2E after refactor | ✅ Passed (2 runs) | Refactor gate met: same flow, same assertions |
| QR-B01 + B08 payload | ✅ Passed | Payload **181 chars** (well under 4,200). Format `<LoadId>$<fields…>`. QR reopened from the submitted card carries the **same** payload. |
| QR-B02 + B03 Driver load | ✅ Passed | Current Load = ticket ID without `#` (G6 confirmed). No duplicate alert. Load + load QR unchanged after restart. |
| QR-B04 second ticket, load active | ✅ Passed | Home offers only *Scan Receipt Code* / *Cancel Load*. Scanning ticket 2 there: no alert, **first load kept**. → **Expected result for check 4 / TC-17** |
| QR-B05 same QR twice | ✅ Passed | After Cancel Load, same QR re-scanned: no alert, same load again |
| QR-B06 non-ticket QR | ✅ Passed | Silently ignored: no alert, no load, nothing stored |
| QR-B07 damaged payload | ⚠️ Warning (finding) | **Truncated-half and one-char-altered payloads were both accepted and stored** as the ticket's load. The Driver app's QR handling has no integrity check of its own → **BLE must add one (FR-5, check 3)** |
| QR-B09 airplane mode | ✅ Passed | QR handoff works with both devices in airplane mode. Ticket syncs once back online. |
| QR-B10 readiness | ⚠️ Warning (expected) | Node3 emulators: BLE hardware yes, BT on. Driver 1.2.0 requests no Bluetooth permissions (pre-BLE build). |

**Code added**: `Flows/CollectionLoadTicketFlow.cs`, `Flows/DriverAppFlow.cs`, `Tests/DualAppTestBase.cs`, `Tests/QrBaselineTests.cs`, `Tests/DeviceBluetoothReadinessTests.cs`, `Tests/ReportSetUpFixture.cs`, `Utilities/BluetoothHelper.cs`, `Utilities/BaselineRecorder.cs`, `Utilities/QrBaselineReport.cs`. G1/G2 in `DriverHomePage`. `QrScanFeed.StageText`. `AdbHelper.Read/OnlineDevices/MockLocation`.

**Stability fixes to existing code found while running**:
- `DriverFactory`: one test per node pair at a time (a NonParallelizable fixture was scheduled next to the QR E2E on the same emulators, and each new Appium session killed the other's).
- `CollectionLoadTicketFormPage.MockGpsLocation`: 1 m mock fix through Appium Settings when `mobile: setGeolocation` is missing (UiAutomator2 2.45.1). The ~16 ft fallback fix was rejected by the location field.
- `FormsPage.GetSubmittedCardValues`: re-reads a card that goes stale when its sync status re-renders.
- `DriverAppFlow`: retries the Driver WebView switch (an adb timeout made it fail silently).

**Environment lessons**: emulators keep the DNS of the network they were launched on. After switching Wi-Fi → cable, restart them with `Start-MobileGrid.ps1 -Stop` and then start (`adb reboot` is not enough). The Flex address lookup calls ArcGIS online with a 15 s timeout, so it needs working DNS. Restart Appium after interrupted runs leave stale sessions.

**Findings for the app teams**: Flex sometimes logs itself out mid-flow (seen twice while waiting for *Synced*). Flex `:remote` process crashes on Android 17 (`MainActivity cannot be cast to BroadcastReceiver`). Driver app cold start always opens on Welcome.

Run Part A: `dotnet test --filter Category=QR-Baseline` · readiness only: `--filter Category=BLE-Readiness`

---

## 1. Reusable steps (from the current code)

Scenarios refer to these step blocks instead of repeating them. **R** = reuses existing code as-is, **N** = new code needed.

| Step block | Code | R/N |
|---|---|---|
| **S1 Flex: log in to forms** | `LoginPage.LoginIfNeeded` → `PermissionHelper.HandleSystemPermissions` → `PrivacyPolicyPage.ClearAllPendingAgreements` → `HomePage.CompleteProjectAssignment` → `DriverFactory.SwitchToWebViewContext(flex)` | R |
| **S2 Flex: fill + submit collection ticket** | `FormsPage.OpenFormsMenu` / `OpenForm("Load - Collection")` / `GetSubmittedTicketIds` (before) / `ClickNew` + `ClockInPage.ClockInIfPrompted` → `CollectionLoadTicketFormPage`: `SelectFirstTruck`, Trailer "No", `AddPhoto(DebrisPile.png)`, `SelectChoiceOption("Truck Debris Type", "Vegetative/Woody")`, `MockGpsLocation` + `CaptureLocation`, `EnterNotes`, `Submit` → returns `TicketContext {TicketId, Truck, StreetName}` | R (steps 1–18 of `LoadTicketCollectionTests`), to be extracted into `Flows/CollectionLoadTicketFlow` |
| **S3 Driver: start to Home** | `DriverFactory.StartDriverApp` → `HandleSystemPermissions` → `DriverOnboardingPage.CompleteOnboardingIfPresent` / `IsHomeDisplayed` → `SwitchToWebViewContext(driver)` → `new DriverHomePage` | R (step 21) |
| **S4 QR handoff** | `ProceedWithQrGeneration` → `GetQrCodePng` → `QrScanFeed.Stage(driverApp, png, …)` → `DriverHomePage.ScanQrCode` → `WaitForActiveLoad` | R (steps 18–22) |
| **S5 Driver snapshot** | `{ CurrentLoad, Decode(GetCurrentLoadQrPng()) }`, used to compare two Driver states | R (combines existing methods) |
| **S6 QR payload** | `QrScanFeed.Decode(GetQrCodePng(), rasterizer: flex)` → raw text + length + SHA-256 | R |
| **S7 Device control** | `BluetoothHelper`: BT on/off, airplane on/off, `pm has-feature android.hardware.bluetooth_le`, `pm revoke` (built on `AdbHelper.Run`) | N |
| **S8 BLE receive** | `DriverBleReceivePage`: `StartReceiving()`, `ShortId`, `Cancel()`, `State` | N (⛔ Q1 locators) |
| **S9 BLE send** | `FlexBleSendPage`: `TapSendViaBluetooth()`, `WaitForDevice(shortId)` → row {Id, Rssi}, `Rows`, `Rescan()`, `Select(shortId)`, `WaitForSent()` / `ErrorText` | N (⛔ Q1, Q6, Q7) |
| **S10 Bad-QR factory** | `QrCodeFactory.Png(text)`: encode any text to a QR PNG (ZXing.Net `BarcodeWriter` already referenced) | N |

### Gaps in the current code that some scenarios depend on

| # | Where | Gap | Affects |
|---|---|---|---|
| G1 | `DriverHomePage.ScanQrCode()` | Always calls `CancelStaleLoadIfPresent()`, so a second ticket can never be scanned **while a load is active**. With a load active, Home shows *Scan Receipt Code* / *Cancel Load*, not *Scan QR Code*. | QR-B04, TC-17 (check 4). Add `ScanQrCode(cancelActiveLoad: false)` and find out where a second collection QR can be scanned today. |
| G2 | `DriverHomePage.WaitForActiveLoad()` | Silently clicks OK on *Possible Duplicate Load*, so the alert is never asserted. | QR-B05, TC-12 re-send. Add `bool DuplicateAlertShown` (or an `acknowledgeDuplicate` parameter) so tests can see it. |
| G3 | `WaitForActiveLoad(timeoutSeconds = 60)` | 60 s polling. The demo SLA is 5 s, and it needs a measured duration, not just a pass. | TC-08, TC-12. Wrap the call in a `Stopwatch` and assert the elapsed time separately. |
| G4 | `TestSettings:RunMode = Web` | Web mode runs both apps in browsers, which have no BLE. | All `BLE` tests: `Assume.That(RunMode == "Mobile")`. |
| G5 | `LoadTicketCollectionTests` | One 400-line method, so it can't be reused without copying. | Extract S2/S3 (test plan §2.1), then re-run QR-A01 before and after. |
| G6 | Comparing Current Load with the ticket ID | Not asserted today (only receipt/disposal prove the link). | QR-B02, TC-12: confirm whether `CurrentLoad == TicketId.TrimStart('#')`. |

---

## Part A - QR baseline (current functionality, runnable now)

| ID | Scenario | Steps | Expected / captured | Feeds | Status |
|---|---|---|---|---|---|
| **QR-A01** | Full QR E2E: collection → Driver → disposal → receipt, Node1–3 | Existing `Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E_*` | Ticket Synced. Driver current load. Disposal linked to collection. Receipt matches Truck / Debris / Capacity / Load Call 50. | Check 5, TC-27 | ✅ |
| **QR-A02** | QR still passes after the refactor (G5) | Run QR-A01 before and after extracting S2/S3 | Same 3/3 result, same evidence files | Test plan §2.1 gate | 🟢 |
| **QR-B01** | **Collection QR payload fingerprint** | S1 → S2 → `ProceedWithQrGeneration` → S6 | Save the payload text, character length and SHA-256 as an attachment. Assert it isn't empty. **Record the length (how far below 4,200 chars a real ticket is).** | FR-4, TC-13, TC-14 sizing | 🟢 |
| **QR-B02** | **Driver state after a QR scan** (FR-6 reference) | S1 → S2 → S4 → S5 | Snapshot `S_qr` {current load, decoded current-load QR} saved. `CurrentLoad` relates to the ticket ID (G6). | FR-6, TC-13 parity | 🟢 |
| **QR-B03** | Load persists after app restart | QR-B02 → `TerminateApp` / `ActivateApp` driver → S3 → S5 | Snapshot equals `S_qr` (storage, not just screen) | FR-6, TC-18 | 🟢 |
| **QR-B04** | **Second collection ticket while a load is active** | S1 → S2 (ticket 1) → S4 → S2 (ticket 2) → scan ticket 2's QR **without cancelling** | Record today's behavior: replaced / rejected / prompt / not possible from Home. This **is** the expected result for check 4. | Check 4, TC-17 | 🟡 G1 |
| **QR-B05** | Same collection QR scanned twice | QR-B02 → Cancel Load → re-stage same QR → scan | Record whether *Possible Duplicate Load* is shown and what is saved | TC-12 re-send, TC-22 | 🟡 G2 |
| **QR-B06** | Driver rejects a **non-ticket QR** | S3 → `QrScanFeed.Stage(QrCodeFactory.Png("hello"))` → `ScanQrCode` | Error/ignored. Current Load stays **NO ACTIVE LOAD**, and stays that way after restart. | FR-5, check 3 (reference) | 🟡 S10 |
| **QR-B07** | Driver rejects a **truncated / altered ticket QR** | QR-B01 payload → cut to 50% / change 1 char → S10 → scan | Same as QR-B06. Records how today's decoder treats damaged ticket data. *(Note: the requirements list 2 known QR decoding bugs in the Driver app, out of scope. Log them, don't fail on them.)* | FR-5, check 3 | 🟡 S10 |
| **QR-B08** | Flex form state after QR generation | S1 → S2 → `ProceedWithQrGeneration` → `CloseQrModal` | Ticket under Submitted and Synced. QR can be reopened from the card (`OpenSubmittedCardQr`) and decodes to the QR-B01 payload. | FR-7 "not locked", TC-19, TC-22 fallback | 🟢 |
| **QR-B09** | QR flow with **both devices in airplane mode** (after login) | S1 → S7 airplane on (both) → S2 → S4 → S7 airplane off | Driver gets the load offline. Ticket under Submitted, **Synced only after airplane mode is off**. | Demo env, TC-29 | 🟡 S7 |
| **QR-B10** | **Device Bluetooth readiness probe** (Day-1 spike) | S7 on each grid device / demo phone | Log Android version, `bluetooth_le` feature, BT on/off, Flex/Driver BT permissions. *(2026-10-27: Node3 A16/A17 emulators report `bluetooth_le=true`, BT on. Driver 1.2.0 requests no BT permissions; Flex 4.10.2 requests SCAN/CONNECT, not granted on Node3 Flex.)* | Risk 1, lane E/D | 🟢 |
| **QR-B11** | Zebra print of a collection ticket | Manual: print from Flex | Prints. Record the steps as the baseline for TC-28. | Check 5 | ✋ |

---

## Part B - BLE handoff (ready when the feature lands)

All part-B scenarios: `RunMode=Mobile` (G4), Android 12+, both apps on BLE builds. Rows marked **D** need the physical demo phones for sign-off; emulator results there are indicative only.

### B.1 DriverApp receive screen (FR-1)

| ID | Scenario | Steps | Expected | Req | Status |
|---|---|---|---|---|---|
| TC-01 | Receive shows a short ID and advertises | S3 → S8 `StartReceiving` → S1 → S2 → S9 `WaitForDevice(ShortId)` | `ShortId` matches `^RTD-[A-Z0-9]{4}$` (⛔ Q4). The same ID is listed in Flex. | FR-1 | 🔵⛔ |
| TC-02 | Cancel stops advertising | TC-01 → S8 `Cancel` → S9 `Rescan` | ID gone after one 15 s scan. Driver back to Home, Current Load unchanged. | FR-1 | 🔵⛔ |
| TC-03 | Leaving the screen stops advertising | TC-01 → Android back → S9 `Rescan` | ID gone | FR-1 | 🔵⛔ |
| TC-04 | 120 s timeout | TC-01 → wait → `Rescan` at ~110 s and ~125 s | Listed at 110 s, gone at 125 s. Driver shows stopped state. | FR-1 | 🔵⛔ |
| TC-05 | Stops after a transfer | TC-12 → S9 `Rescan` | ID gone. Driver shows the load (Home). | FR-1 | 🔵⛔ |

### B.2 Flex send and device list (FR-2, FR-3)

| ID | Scenario | Steps | Expected | Req | Status |
|---|---|---|---|---|---|
| TC-06 | Send via Bluetooth beside the QR button | S1 → S2 → (QR prompt / modal or card, ⛔ Q6) | *Send via Bluetooth* visible and enabled next to the QR action. QR still works. | FR-2 | 🔵⛔ |
| TC-07 | Not offered on Load - Disposal | S1 → `OpenAllForms` → `OpenForm("Load - Disposal")` → submit flow | No BLE action | FR-2 scope | 🔵 |
| TC-08 | **Discovery ≤5 s, 10 of 10** (check 1) | S3 → S8 → S1 → S2 → loop ×10 { stopwatch → S9 `TapSendViaBluetooth` → `WaitForDevice` → stop; back out }, G3 | 10/10 under 5 s. All 10 timings attached. | FR-3, check 1 | 🔵⛔ D |
| TC-09 | Strongest first | 2 Drivers receiving (Node2_Driver + Node3_Driver, or 2 phones) → S9 `Rows` | RSSI values descending. On phones: moving one phone away changes the order after `Rescan`. | FR-3 | 🔵⛔ D |
| TC-10 | Nothing nearby | S1 → S2 → S9 with no Driver receiving | Scan ends by about 15 s with an empty list / message. No hang. | FR-3 | 🔵⛔ |
| TC-11 | Rescan finds a late Driver | TC-10 → S8 `StartReceiving` → S9 `Rescan` | Driver listed | FR-3 | 🔵⛔ |

### B.3 Transfer, payload, load flow (FR-4, FR-5, FR-6)

| ID | Scenario | Steps | Expected | Req | Status |
|---|---|---|---|---|---|
| TC-12 | **Happy path ≤5 s, ≥9 of 10** (check 2) | Loop ×10 { S2 → S8 → S9 `Select(ShortId)` → stopwatch → `WaitForActiveLoad` (G3) → S5 → Cancel Load } | ≥9/10 under 5 s. Load = ticket (G6). Truck Number = selected truck. Timings attached. | FR-4, FR-6, check 2 | 🔵⛔ D |
| TC-13 | **BLE = QR parity** | S2 (ticket T) → BLE send → S5 = `S_ble` → clear Driver data → S3 → `OpenSubmittedCardQr(T)` → S4 → S5 = `S_qr` | `S_ble == S_qr`. If the Driver logs the received payload (⛔ Q3): its hash = QR-B01 hash for T. | FR-4, FR-6 | 🔵 |
| TC-14 | 4,200-character payload | Dev test-ticket button (4,200 chars) → BLE send | Received intact (length + hash, ⛔ Q3). Transfer time recorded. | FR-4, check 7 (E2E side) | 🔵⛔ |
| TC-15 | **Damaged data rejected** (check 3) | S5 before → dev fault injection "corrupt" (⛔ Q2) → BLE send → S5 after → restart Driver → S5 | before = after = after-restart. Flex shows an error with a reason. 100% over N runs. Result compared to QR-B07. | FR-5, check 3 | 🔵⛔ |
| TC-16 | **Incomplete data rejected** | As TC-15 with "truncate" | As TC-15 | FR-5, check 3 | 🔵⛔ |
| TC-17 | **Second ticket while a load is active** (check 4) | S2 (T1) → BLE → S2 (T2) → BLE send without cancelling | **Same outcome as QR-B04** | FR-6, check 4 | 🔵 (needs QR-B04) |
| TC-18 | BLE load persists after restart | TC-12 → restart Driver → S5 | Same as QR-B03 | FR-6 | 🔵 |

### B.4 Flex result and failures (FR-7)

| ID | Scenario | Steps | Expected | Req | Status |
|---|---|---|---|---|---|
| TC-19 | "Sent" and form not locked | TC-12 → Flex | **"Sent"** shown. QR action still opens a QR that decodes to the QR-B01 payload. *Send via Bluetooth* still enabled. | FR-7 | 🔵 |
| TC-20 | **BT off mid-transfer ≤10 s** (check 6) | 4,200-char test ticket → `Select` → S7 BT off on Driver → stopwatch until `ErrorText` | Error with reason within 10 s. Driver saved nothing (S5 unchanged). Repeat with BT off on Flex. | FR-7, check 6 | 🔵⛔ |
| TC-21 | Driver cancels after being listed | S8 → S9 list → S8 `Cancel` → S9 `Select` | Error with reason. No hang. | FR-7 | 🔵⛔ |
| TC-22 | Recover after failure | TC-20 → S7 BT on → resend; then QR action for the same ticket | Resend succeeds. QR fallback works. If the ticket was already received, behavior matches QR-B05. | FR-7 | 🔵 |
| TC-23 | Flex BT off before Send | S7 BT off (Flex) → `TapSendViaBluetooth` | Clear "turn on Bluetooth" message. No crash. | FR-7 | 🔵⛔ |

### B.5 Risks, environment, regression

| ID | Scenario | Steps | Expected | Req | Status |
|---|---|---|---|---|---|
| TC-24 | Nearby-devices permission denied on Driver | S7 `pm revoke … BLUETOOTH_ADVERTISE/CONNECT` (override `AutoGrantPermissions`) → S8 | Prompt or clear message. *Scan QR Code* still works (S4). | Risk | 🔵 |
| TC-25 | Phone can't advertise | Manual, phone failing QR-B10 | Clear message, QR fallback | Risk 1 | ✋ |
| TC-26 | Several drivers: only the picked one receives | 2 Drivers in S8 → S9 `Select(B)` | B has the load. A unchanged and still listening. | Risk 4 | 🔵 |
| TC-27 | QR regression on BLE builds | QR-A01 + QR-B01…B08 on BLE builds | Identical to the pre-BLE baseline run | Check 5 | 🟢 now, 🔵 rerun |
| TC-28 | Zebra print → BLE send → Zebra print | Manual (QR-B11 steps) | Both prints OK. One BT connection at a time. | Check 5, constraint | ✋ |
| TC-29 | Demo conditions | Phones, both airplane on (after S1), BT on, 1–3 m: TC-08, TC-12, TC-15, TC-20 | Same targets as without airplane mode | Demo | 🔵 D |

---

## 2. Coverage check

| Req / check | Baseline (Part A) | BLE (Part B) |
|---|---|---|
| FR-1 | - | TC-01…05 |
| FR-2 | - | TC-06, TC-07 |
| FR-3 | - | TC-08…11 |
| FR-4 | QR-B01 | TC-12, 13, 14 |
| FR-5 | QR-B06, QR-B07 | TC-15, 16 |
| FR-6 | QR-B02, QR-B03, QR-B04 | TC-12, 13, 17, 18 |
| FR-7 | QR-B08 | TC-19…23 |
| Check 1 / 2 | - | TC-08 / TC-12 |
| Check 3 | QR-B06, B07 | TC-15, 16 |
| Check 4 | QR-B04 | TC-17 |
| Check 5 | QR-A01, A02, B11 | TC-27, 28 |
| Check 6 | - | TC-20 |
| Check 7 | - | Unit tests in the app repos (test plan §6) + TC-14 |
| Demo env | QR-B09, QR-B10 | TC-29 |

Every FR and check has at least one scenario.

## 3. Suggested order of work

1. **Now**: QR-B10 (device probe), QR-A02 (refactor gate) → QR-B01, B02, B03, B08 (🟢).
2. **Now, small code changes**: G1/G2 + S10 + S7 → QR-B04…B07, QR-B09. These lock in the expected results for checks 3 and 4 before BLE arrives.
3. **When the dev team shares locators (Q1)**: write `DriverBleReceivePage` / `FlexBleSendPage` and the B.1/B.2 scenarios against the first BLE build.
4. **BLE build + fault injection (Q2)**: B.3/B.4. Then the physical-phone run for checks 1, 2, 6 and TC-29.

---

## Part C - Feature #10082 R1-C1 (Foundation & BLE spike)

From *BT - Requirements Plan - feature-10082*, priority group **R1-C1**. Automated in `Tests/NearbyDiscoverabilityTests.cs` (category `BLE-R1-C1`). The tests check the **R1 requirement**, not the POC, so they fail until R1 lands. Each failure names the requirement ID.

| Requirement | What it asks | Test | Status on the POC (2026-10-30) |
|---|---|---|---|
| R1-INFRA-SPIKE | BLE / Nearby transport spike, go/no-go Nov 3 | No test: it is a decision. Evidence: QR-B10 (`DeviceBluetoothReadinessTests`) and TC-12/13 (Android-to-Android BLE with the Driver App as peripheral) | ✅ evidence available |
| R1-INFRA-FLAG | One flag gates every #10082 entry point. Flag off leaves the QR flows identical | Blocked: neither app has a #10082 flag yet (only `driverSession` and Flex's dev-only `bleHandoffDevTools`, both build-time). Once it exists: flag-off build shows no Bluetooth icon in Flex and no *Receive via Bluetooth* in the Driver App, plus `QrBaselineTests` on that build | ⛔ blocked |
| 10082-NDT-1-P01, N02 | Discoverable whenever open in the foreground, with no receive mode | `NDT1_P01_N02_DriverOnHome_IsDiscoverable_WithoutReceiveMode` | ❌ not listed (POC needs *Receive via Bluetooth*) |
| 10082-NDT-1-P02, N01 | Discoverable on any page, without going to Home first | `NDT1_P02_N01_DriverOnAnotherPage_IsDiscoverable` (Receipts tab) | ❌ not listed |
| 10082-NDT-1-N01 | Not even Home needs to have been reached | `NDT1_N01_DriverBeforeHome_IsDiscoverable` (Welcome screen) | ❌ not listed |
| 10082-NDT-1-P03 | Discoverable again after returning from the background, with nothing re-enabled | `NDT1_P03_DriverBackFromBackground_IsDiscoverableAgain`. Whether it is listed while in the background is only recorded (`Baselines/NDT-1-P03_*.json`) | ❌ fails at its P01 precondition |
| (control) | Proves the search below can find an advertising Driver App | `NDT1_Control_DriverInReceiveMode_IsFoundByDifference` | ✅ found in 1.1 s |

**How a test finds its own Driver App.** Without receive mode the Driver App shows no code to look for, so each test works by difference:
1. It stops the Driver App on every grid Driver emulator, since all emulators share one simulated Bluetooth.
2. It opens Flex's *Send via Bluetooth* picker on any submitted *Load - Collection* ticket and notes what one search lists.
3. It starts this node's Driver App in the state under test and searches again (Rescan, 2 searches). A new entry is this node's Driver App.

The fixture is `NonParallelizable` and runs on Node 2 (`testuser2`).

**When R1 lands:** the picker becomes the standard Flex Action Menu (FEAT-NDT-2), so `BleTruckPickerPage`'s locators will need updating. The test logic stays the same.
