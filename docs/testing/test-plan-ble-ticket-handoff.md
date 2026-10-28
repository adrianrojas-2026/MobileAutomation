# Test Plan - BLE Ticket Handoff POC (Flex → DriverApp, load ticket at collection)

**Source**: *BLE Enhancements POC: Requirements Summary* (Oct 19, 2026, @Anshdeep) - FR-1…FR-7, demo checks 1–7
**Scope type**: User-provided requirements (no `docs/plans/implementation-plan.md` exists)
**Author**: QA · **Created**: 2026-10-27 · **Status**: Draft, open questions in §10

---

## 1. Scope

**Coverage**: The single POC transfer: the Collection Load Ticket sent from Flex to the DriverApp over Bluetooth Low Energy, Android 12+.

**In scope**
- DriverApp *Receive via Bluetooth* screen, short ID, stop conditions (FR-1)
- Flex *Send via Bluetooth* action on the collection form (FR-2)
- Flex nearby-device list, 15 s scan, signal ordering, Rescan (FR-3)
- Payload parity with the QR code, 4,200-character capacity (FR-4)
- DriverApp integrity check: damaged or incomplete data rejected and never saved (FR-5)
- Hand-off into the existing load flow (FR-6)
- Flex "Sent" status, form not locked, error message on failure (FR-7)
- **Regression**: the existing QR collection → disposal → receipt flow, and Zebra printing (demo check 5)

**Out of scope** (per the requirements): iOS; disposal receipt over BLE; DriverApp → Flex; pairing and encryption; background or screen-off listening; multi-truck broadcast; changes to QR, servers or databases; mapping short ID to truck number; form locking and Retry/Show-QR buttons; production hardening.

---

## 2. Automation strategy: keep QR, add BLE in separate scripts

The QR test must keep running unchanged as the regression baseline (demo check 5). The BLE tests reuse its Flex and Driver setup steps but live in their own fixture.

### 2.1 Reuse approach (recommended): extract shared steps, don't copy-paste

`Test_LoadDisposalCollectionFlow_And_VerifySubmittedSynced_E2E` is a single ~400-line method. Steps 1–15 (log in, open the form, fill Truck / Trailer / Collection Start / Collection Information / Location) are identical for both transports. Only the hand-off (steps 16–22) differs.

| New / changed file | Purpose |
|---|---|
| `Flows/CollectionLoadTicketFlow.cs` *(new)* | Holds steps 1–15 + submit + "new ticket is Synced" as reusable methods, e.g. `LoginAndOpenForms()`, `FillCollectionTicket() → TicketContext {TruckOption, StreetName, …}`, `SubmitAndGetTicketId()`. Lifted verbatim from the current test. |
| `Flows/DriverAppFlow.cs` *(new)* | `StartAndOnboardDriverApp()`: current step 21 (start, permissions, onboarding, switch to WebView). |
| `Tests/LoadTicketCollectionTests.cs` *(refactor only)* | Calls the flows, then keeps its QR-specific steps 16–27 as they are. **Assertions and test names stay unchanged.** |
| `Tests/BleLoadTicketHandoffTests.cs` *(new)* | All BLE test cases in this plan. Uses the same flows, then the BLE hand-off. |
| `Pages/FlexApp/FlexBleSendPage.cs` *(new)* | *Send via Bluetooth* button, device list (ID, signal strength), Rescan, "Sent" status, error text. |
| `Pages/Driver/DriverBleReceivePage.cs` *(new)* | *Receive via Bluetooth*, short ID, Cancel, state (listening / received / stopped). |
| `Utilities/BluetoothHelper.cs` *(new, built on `AdbHelper.Run`)* | `SetBluetooth(udid, on/off)` (`cmd bluetooth_manager enable/disable`), `SetAirplaneMode(udid, on/off)` (`cmd connectivity airplane-mode enable/disable`), `SupportsBleAdvertising(udid)`, `RevokeNearbyPermissions(udid, package)`. |
| `Config/appsettings.json` | Add physical-device entries, e.g. `Grid_Phone_Flex` / `Grid_Phone_Driver` (USB serials), for the demo lane (§7). |

**Gate for the refactor**: run `LoadTicketCollectionTests` on Node1–Node3 **before and after** the extraction. Same 3/3 pass result and same evidence (QR PNGs, ticket IDs). This proves the QR scripts were not changed in behavior.

*Fallback if a refactor is not allowed right now*: copy the class to `BleLoadTicketHandoffTests.cs` and replace steps 16–22. This is faster to start, but every future form change must then be made twice. Plan to extract later.

### 2.2 Categories, for running each set alone

| Category | Applied to | Run with |
|---|---|---|
| `QR` | existing `LoadTicketCollectionTests` | `dotnet test --filter Category=QR` |
| `BLE` | BLE cases that can run on emulators | `--filter Category=BLE` |
| `BLE-Device` | cases that need physical phones (airplane mode, range, RSSI order, demo timing) | `--filter Category=BLE-Device` |
| `Manual` | documented here, run by hand (Zebra printing, low-cost phone) | n/a |

Test names follow the existing pattern: `Test_<Flow>_<Expectation>_<Node>_A<flex>_A<driver>`.

---

## 3. Requirements traceability

| Req / Check | Description | Test cases |
|---|---|---|
| FR-1 | Receive makes phone discoverable, shows short ID; stops after transfer / Cancel / leaving screen / 120 s | TC-01, TC-02, TC-03, TC-04, TC-05 |
| FR-2 | *Send via Bluetooth* next to QR button on collection form (load ticket) | TC-06, TC-07 |
| FR-3 | ≤15 s search, list by ID + signal, strongest first, Rescan | TC-08, TC-09, TC-10, TC-11 |
| FR-4 | Data sent = QR data; supports 4,200 chars | TC-12, TC-13, TC-14, UT-01…UT-05 |
| FR-5 | Incomplete or damaged data rejected and never saved | TC-15, TC-16, UT-06…UT-09 |
| FR-6 | Received data goes through the existing load flow (screens + storage) | TC-12, TC-17, TC-18 |
| FR-7 | "Sent" on confirm, form not locked; error message with reason on failure | TC-19, TC-20, TC-21, TC-22, TC-23 |
| Constraint | Zebra printing keeps working; one BT connection at a time | TC-27, TC-28 |
| Risk | Phone can't advertise / permissions denied → clear message, QR fallback | TC-24, TC-25 |
| Risk | Several drivers nearby | TC-26 |
| Check 1 | DriverApp in Flex list ≤5 s, 10/10 | TC-08 (repeated ×10) |
| Check 2 | Ticket shows as current load, 9/10 each ≤5 s | TC-12 (repeated ×10) |
| Check 3 | Damaged data rejected, nothing saved, 100% | TC-15, TC-16 |
| Check 4 | Second ticket behaves like a second QR scan | TC-17 |
| Check 5 | QR flows + Zebra unchanged | TC-27, TC-28 |
| Check 6 | BT off mid-transfer → Flex error ≤10 s | TC-20 |
| Check 7 | Unit tests incl. 4,200 chars, ≥85% coverage | UT-01…UT-09 (app repos) |
| Demo env | Both phones airplane mode, BT on, 1–3 m | TC-29 |

---

## 4. Test categories

### Unit tests (owned by the Flex / DriverApp dev teams, in the app repos; QA tracks the gate)
Transfer logic only: framing/chunking, reassembly, integrity check, size limits. Target ≥85% line coverage of that code (check 7).

### Integration tests (device-level, automated in this repo)
Single-app behaviour of the new screens: receive-screen lifecycle (FR-1), scan/list/Rescan (FR-3), permission and adapter-off handling.

### E2E tests (two devices, automated in this repo)
Flex fills and submits the ticket → BLE send → DriverApp current load. Plus parity against the QR path, failure paths, and the full QR regression.

---

## 5. Test scenarios

Priority: **P1** = demo check / must pass, **P2** = requirement coverage, **P3** = hardening / nice to have.
Lane: **E** = emulator grid, **D** = physical phones, **M** = manual.

### 5.1 DriverApp receive screen (FR-1)

| ID | Scenario | Expected result | Pri | Lane |
|---|---|---|---|---|
| TC-01 | Driver taps *Receive via Bluetooth* | Receive screen shows a short ID matching `^RTD-[A-Z0-9]{4}$` (format to be confirmed, §10). Flex scan lists that same ID. | P1 | E, D |
| TC-02 | Driver taps **Cancel** while listening | Screen leaves the listening state. A Flex **Rescan** no longer lists the ID (wait at least one scan window). | P2 | E, D |
| TC-03 | Driver leaves the screen (Android back / navigate Home) | Advertising stops. Flex Rescan no longer lists the ID. | P2 | E, D |
| TC-04 | Driver leaves the screen open, no transfer | ID is still listed in Flex at ~110 s. Not listed after ~125 s. DriverApp shows the stopped/timeout state. *(Boundary: 120 s.)* | P2 | E, D |
| TC-05 | Successful transfer (reuses TC-12) | DriverApp stops listening after confirming. Flex Rescan no longer lists the ID. | P2 | E, D |

### 5.2 Flex send action and device list (FR-2, FR-3)

| ID | Scenario | Expected result | Pri | Lane |
|---|---|---|---|---|
| TC-06 | Submit a Collection Load Ticket, reach the QR action | *Send via Bluetooth* is shown next to the existing QR button. Both are enabled. | P1 | E, D |
| TC-07 | Open a **Load - Disposal** form | No *Send via Bluetooth* action (disposal stays QR). | P3 | E |
| TC-08 | DriverApp receiving, Flex taps *Send via Bluetooth* (**×10 iterations**) | DriverApp's short ID appears with a signal-strength value **within 5 s, 10 of 10** (check 1). The stopwatch starts at the tap and stops when the row is visible. All 10 timings go into the report. | P1 | E (indicative), **D (sign-off)** |
| TC-09 | Two or more DriverApps receiving | All listed. Rows sorted by signal strength, strongest first (read displayed values, assert descending). On phones: move one phone farther away, Rescan, and the order changes. | P2 | E (sort only), D |
| TC-10 | No DriverApp receiving | Scan ends by ~15 s with an empty list / "no devices" message. App does not hang. | P2 | E, D |
| TC-11 | DriverApp starts receiving *after* the first scan ends, monitor taps **Rescan** | New scan starts and lists the device. | P2 | E, D |

### 5.3 Transfer, payload and load flow (FR-4, FR-5, FR-6)

| ID | Scenario | Expected result | Pri | Lane |
|---|---|---|---|---|
| TC-12 | **Happy path** (**×10 iterations**): Flex submits ticket (shared flow) → Send via BT → picks driver's ID | DriverApp shows the ticket as the **current load** (`DriverHomePage.WaitForActiveLoad`) **within 5 s in at least 9 of 10** runs (check 2). Truck Number matches the selected truck. | P1 | E (indicative), **D (sign-off)** |
| TC-13 | **Payload parity with QR** (FR-4/FR-6), same ticket through both paths | **A)** BLE-send ticket T. Capture DriverApp state *S_ble*: current load ID, displayed fields, and decoded current-load QR (`GetCurrentLoadQrPng` + `QrScanFeed.Decode`). **B)** Reset the DriverApp (clear app data). Open T's QR in Flex (`OpenSubmittedCardQr`), stage and scan it, capture *S_qr*. **Assert *S_ble* == *S_qr*.** Also decode Flex's QR for T and assert it equals the payload logged by the DriverApp on receipt, if a debug log is available (§10). | P1 | E, D |
| TC-14 | 4,200-character payload via the dev-build **test-ticket button** | Transfer completes. DriverApp payload is identical (length + content hash from the debug log, or field values). Also record the transfer time (risk: BT speed). | P2 | E, D |
| TC-15 | **Damaged data** (dev-build fault injection: flipped bytes / bad checksum) ×N | DriverApp rejects it. **Current load unchanged and nothing new saved** (load list / current load before = after, and still the same after an app restart). Flex shows an error with a reason. **100%** (check 3). | P1 | E, D |
| TC-16 | **Incomplete data** (dev-build fault injection: truncated after first chunk) | Same as TC-15. | P1 | E, D |
| TC-17 | **Second ticket while a load is active** | First run the QR fixture's "second scan" to record today's behavior (accept / replace / prompt). Then BLE-send a second ticket with load 1 active. **Behavior matches the QR baseline** (check 4). | P1 | E, D |
| TC-18 | Received load persists | After TC-12, force-stop and relaunch the DriverApp. Current load is still shown, same as after a QR scan. | P2 | E, D |

### 5.4 Flex result and failures (FR-7)

| ID | Scenario | Expected result | Pri | Lane |
|---|---|---|---|---|
| TC-19 | After a confirmed transfer | Flex shows **"Sent"**. Form is **not locked**: QR button still works (QR renders and decodes), Send via BT can be tapped again. | P1 | E, D |
| TC-20 | **Bluetooth off mid-transfer**: send the 4,200-char test ticket (longer window), then `BluetoothHelper.SetBluetooth(driverUdid, off)` right after selecting the device | Flex shows an error with a reason **within 10 s** (check 6). DriverApp saved nothing. *(Repeat with BT off on the Flex side.)* | P1 | E, D |
| TC-21 | Driver cancels receive between scan and send (stale list entry) | Flex shows an error with a reason. No hang. | P2 | E, D |
| TC-22 | Recovery after failure | After TC-20/21, re-enable BT. A resend via BLE succeeds **and** the QR button fallback still works for the same ticket. | P2 | E, D |
| TC-23 | Flex Bluetooth off when tapping *Send via Bluetooth* | Clear message (turn on Bluetooth). No crash. | P3 | E, D |

### 5.5 Risks, multiple drivers, environment

| ID | Scenario | Expected result | Pri | Lane |
|---|---|---|---|---|
| TC-24 | DriverApp missing *Nearby devices* permission (`pm revoke … BLUETOOTH_ADVERTISE / BLUETOOTH_CONNECT`; note `AutoGrantPermissions=true` must be overridden for this test) | Permission prompt or clear message. QR scan still available. | P2 | E, D |
| TC-25 | Phone that can't advertise over BLE | DriverApp shows a clear message and falls back to QR. **Day 1: run `SupportsBleAdvertising` on the demo phones.** | P2 | M / D |
| TC-26 | **Several drivers nearby**: 2 DriverApps receiving (e.g. Node1_Driver + Node2_Driver on shared virtual BT), monitor picks driver B's ID | Only driver B gets the load. Driver A's current load is unchanged and it keeps listening. | P2 | E, D |
| TC-27 | **QR regression**: existing `LoadTicketCollectionTests` (Node1–3) against the BLE builds | All 3 pass: collection QR, disposal QR import, receipt (check 5). | P1 | E |
| TC-28 | **Zebra printing**: print a ticket → BLE send → print again (one BT connection at a time) | Both prints succeed. BLE send not blocked by the printer link, and printer not broken by BLE. | P1 | M |
| TC-29 | **Demo conditions**: both phones in airplane mode, BT on, 1–3 m apart; run TC-08, TC-12, TC-15, TC-20 | Same results as without airplane mode. (Log in to Flex before enabling airplane mode; see §7.) | P1 | D |

---

## 6. Unit tests (app repos, check 7)

Run by the dev teams. QA collects the result + coverage report as evidence.

| ID | Area | Case |
|---|---|---|
| UT-01 | Round-trip | Typical load-ticket QR string → frame → reassemble → identical |
| UT-02 | Boundary | **Exactly 4,200 chars** round-trips identically |
| UT-03 | Boundary | 0 chars / 1 char; 4,201 chars → documented behavior (reject or accept, §10) |
| UT-04 | Encoding | Non-ASCII / multi-byte UTF-8 (accents, emoji) in notes or street name keeps the char count |
| UT-05 | Chunking | Payload exactly a multiple of chunk size, and chunk size ± 1 |
| UT-06 | Integrity | Single flipped byte → rejected |
| UT-07 | Integrity | Truncated (missing last chunk) → rejected |
| UT-08 | Integrity | Duplicated / out-of-order chunk → rejected or correctly reassembled (per design) |
| UT-09 | Integrity | Length header mismatch → rejected; nothing passed to the load flow |

**Gate**: 100% pass, **≥85% coverage of the transfer-logic module**.

---

## 7. Environments

| Lane | Setup | Used for |
|---|---|---|
| **E - Emulator grid** (existing Node1–3 pairs) | Requires the Android Emulator's virtual Bluetooth (netsim), so emulators on the same host can see each other. **Not yet verified here, Day-1 spike.** Virtual BT is expected to need newer system images, so **Node1_Driver (Android 12) may not support it**. If it does not, run BLE on Node2/Node3 and keep Node1 for QR only. | Functional coverage and CI regression. Timing results are **indicative only**. |
| **D - Physical phones** | 2× Android 12+ phones (the demo phones), USB-connected to the Appium host (adb over USB still works in airplane mode; Wi-Fi adb does not), 1–3 m apart, airplane mode on, Bluetooth on. | **Demo sign-off**: checks 1, 2, 3, 6 and TC-29. |
| **M - Manual** | Flex phone + Zebra printer; one low-cost phone. | TC-25, TC-28 |

**Offline caveat**: today the debug APKs reach the backend through `adb reverse` (Aspire ports), and Flex login needs it. For lane D: log in and clear onboarding **before** enabling airplane mode. In BLE fixtures, assert the ticket appears under *Submitted* but **don't** require *Synced* while in airplane mode. Sync can be checked after airplane mode is turned off.

**Builds**: dev builds of Flex and DriverApp from the POC branch (they include the test-ticket button and, ideally, fault injection).

---

## 8. Test data

| Data set | Source | Used in |
|---|---|---|
| Real collection ticket | Shared flow (first truck, "Vegetative/Woody", mock GPS, `DebrisPile.png`) | TC-12, 13, 17, 18, 19 |
| Test ticket (normal size) | Dev-build test-ticket button | TC-08, 10, 11, iterations |
| 4,200-char ticket | Test-ticket button (size option, or long notes) | TC-14, TC-20, UT-02 |
| Damaged / truncated payload | **Dev-build fault-injection hook (needed, §10)** | TC-15, TC-16 |
| Two consecutive tickets | Shared flow ×2 | TC-17 |
| Accounts | Existing per-node Flex accounts (`TestSettings:Node<N>`) | all |

---

## 9. Quality gates

- All **P1** cases pass on lane D (demo checks 1–7 met at their stated targets)
- Check 1: 10/10 ≤5 s · Check 2: ≥9/10 ≤5 s · Check 3: 100% · Check 6: ≤10 s
- Unit tests: 100% pass, ≥85% coverage of transfer logic (check 7)
- **QR regression (TC-27) 3/3 green, before and after the test refactor and on the BLE builds**
- Zebra printing (TC-28) passes
- No open Critical/High defects against FR-1…FR-7
- Every run attaches evidence: per-iteration timings, screenshots on failure (existing `CaptureFailureEvidence`), decoded payloads for parity

---

## 10. Open questions and dev-team asks (blocking automation)

| # | Question / ask | Blocks |
|---|---|---|
| Q1 | **Locators**: stable IDs (`data-testid` / Android `resource-id`) for: Send via BT, device rows (ID + signal), Rescan, Sent status, error text, Receive via BT, short ID, Cancel. Are the receive screen and device list WebView or native? | All page objects |
| Q2 | **Fault injection** in dev builds to send damaged and truncated payloads (or a way to push a raw payload) | TC-15, TC-16 (check 3) |
| Q3 | DriverApp **debug log** (logcat tag) of the received payload / hash, for exact byte parity | TC-13, TC-14 |
| Q4 | Exact short-ID format, and is a new ID generated on every *Receive* tap? | TC-01 |
| Q5 | Behavior above 4,200 chars (reject with message?) | UT-03 |
| Q6 | Exact placement of *Send via Bluetooth*: in the post-submit QR modal, on the submitted card, or both? | TC-06, flow design |
| Q7 | Error message texts per failure reason (BT off, out of range, rejected) | TC-20…TC-23 |
| Q8 | Is the emulator virtual Bluetooth approved for CI, and are the demo phones available for lane D? | Lane E / D |
| Q9 | Approval to refactor `LoadTicketCollectionTests` into shared flows (§2.1) vs. a full copy | Implementation order |

---

## 11. Schedule (aligned with the POC's 6–8.5 dev days)

| Phase | When | Output |
|---|---|---|
| Test planning | Day 0 (2026-10-27) | This document |
| Day-1 spike | Build day 1 | Emulator virtual BT check on Node1–3; `SupportsBleAdvertising` on demo phones; Q1–Q9 answered |
| Refactor + QR gate | Build days 1–2 | Shared flows; `LoadTicketCollectionTests` 3/3 green before/after |
| BLE scripts | As DriverApp receive screen (dev days 3–5) and Flex send (day 5–6) land | `BleLoadTicketHandoffTests`, page objects, `BluetoothHelper` |
| Execution | Dev days 6–8 | Lane E full run; lane D demo checks; manual TC-25/TC-28 |
| Reporting | Before demo | Validation report (`aire-qa-validate`) |
