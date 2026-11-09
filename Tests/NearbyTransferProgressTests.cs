using MobileAutomation.Tests.Reporting;
using System.Text.RegularExpressions;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium;
using static MobileAutomation.Tests.Pages.NearbyDriverMenuPage;

namespace RecoveryTrac.AutomatedTests
{
    // R1-C2 FEAT-NDT-10 (Present Nearby Transfer Progress and Cancellation) and FEAT-NDT-2-P14, #10082. Once the Flex
    // user picks a Driver App, Flex stops discovery and the same menu turns into a 'Send Data' progress view
    // (Connecting… then Sending data…, with a spinner and Cancel) that ignores taps outside it. A Cancel confirmed before
    // the Driver App commits returns to the unchanged ticket.
    // A transfer between emulators takes about a second, so the menu is recorded from inside the WebView and checked
    // afterwards rather than polled. The POC picker shows 'Send via Bluetooth', a progress bar and no Cancel, so these
    // fail until R1; each failure names its requirement ID.
    // Pending until Flex ships Story 4.8 (NDT-10 progress view and Cancel); until then the send view keeps the earlier
    // 'Send via Bluetooth' title, a progress bar and no Cancel. Move back to "BLE-R1-C2" once 4.8 is in the APK
    [TestFixture]
    [Category("BLE-R1-C2-Pending")]
    [NonParallelizable]
    public class NearbyTransferProgressTests : NearbyTestBase
    {
        private static readonly string[] TransferStates = [ConnectingText, ReconnectingText, SendingDataText];
        private static readonly Regex Canceled = new(@"\bcancel(l)?ed\b", RegexOptions.IgnoreCase);

        // 10082-NDT-2-P14, N04 and NDT-10-P01, P02, P03, P04, P06, P07, N01: the Flex user picks the Driver App and the
        // ticket is sent
        [Test]
        [Requirements("NDT-2-P14", "NDT-2-N04", "NDT-10-P01", "NDT-10-P02", "NDT-10-P03", "NDT-10-P04", "NDT-10-P06", "NDT-10-P07", "NDT-10-N01")]
        public void NDT10_SelectDriverApp_StopsDiscovery_ShowsSendDataProgress()
        {
            var (flow, ticketId, flexUdid) = StartFlexOnSubmittedTicket("Nearby transfer progress");
            PrepareGridDriverApps();
            var menu = OpenNearby(flow, ticketId);
            var own = FindOwnDriverApp(menu);
            string? menuBefore = menu.Snapshot().Overlay;

            menu.StartRecording();
            menu.SelectCard(own.Key);
            bool? scanningAfterSelect = IsFlexScanning(flexUdid);
            string? load = null;
            try
            {
                load = own.Home.WaitForActiveLoad(timeoutSeconds: 60);
            }
            catch (WebDriverTimeoutException ex)
            {
                Console.WriteLine($"⚠️ {ex.Message}");
            }
            menu.WaitFor(s => !s.Open || s.Progress == null, 15);
            var recording = menu.StopRecording();

            long tSelect = recording.MarkTime("select")!.Value;
            var afterSelect = recording.Log.Where(s => s.T >= tSelect).ToList();
            var inProgress = afterSelect.Where(s => s.Open && s.Progress != null).ToList();
            var transferring = inProgress.Where(s => TransferStates.Contains(s.Progress!.Text)).ToList();
            var cancelChecked = transferring.Count > 0 ? transferring : inProgress;
            var states = inProgress.Select(s => $"{s.Progress!.Text}{(s.Progress.Spinner ? " (spinner)" : string.Empty)}").Distinct().ToList();
            Console.WriteLine($"📡 After selecting '{own.Key}': [{string.Join(" > ", states)}], titles [{string.Join(", ", inProgress.Select(s => s.Title).Distinct())}]; " +
                              $"Flex scanning: {scanningAfterSelect}; Driver App load: '{load}'.");

            BaselineRecorder.Save("NDT-10-P01", Node, new
            {
                ticketId, own.Key, states, titles = inProgress.Select(s => s.Title).Distinct(), scanningAfterSelect, load,
                final = afterSelect.LastOrDefault()?.ToString()
            });

            if (load == null)
            {
                Assert.Warn("The Driver App did not receive the ticket within 60 s, so the transfer did not reach its result (later features).");
            }
            Assert.Multiple(() =>
            {
                Assert.That(inProgress.Any(s => s.Searching != null) || afterSelect.LastOrDefault()?.Searching != null, Is.False,
                    "10082-NDT-2-P14 not met: Flex kept showing the active search after the Driver App was selected.");
                Assert.That(scanningAfterSelect, Is.Not.True,
                    "10082-NDT-2-P14 / N04 not met: Flex is still scanning after the Driver App was selected.");
                Assert.That(inProgress, Is.Not.Empty,
                    $"10082-NDT-10-P01 not met: no progress was shown after the Driver App was selected (states after selecting: {string.Join(" / ", afterSelect.Select(s => s.ToString()).Distinct().Take(5))}).");
                Assert.That(afterSelect.TakeWhile(s => s.Progress == null).All(s => s.Open) && inProgress.All(s => s.Overlay == menuBefore), Is.True,
                    "10082-NDT-10-P01 not met: the discovery menu closed or was replaced by another overlay instead of turning into the progress view.");
                Assert.That(inProgress.All(s => s.NoResults == null && s.Cards.All(c => c.Key == own.Key)), Is.True,
                    "10082-NDT-10-P01 not met: the progress view still shows discovery content (other results or the no-results state).");
                Assert.That(inProgress.Count > 0 && inProgress.All(s => s.Title == SendDataTitle), Is.True,
                    $"10082-NDT-10-P02 not met: the progress menu's title is not '{SendDataTitle}' (titles: [{string.Join(", ", inProgress.Select(s => s.Title).Distinct())}]).");
                Assert.That(inProgress.All(s => s.Card(own.Key) != null || s.Lines.Any(l => l.Contains(own.Key))), Is.True,
                    $"10082-NDT-10-P03 not met: the selected Driver App '{own.Key}' is not shown throughout the progress view.");
                Assert.That(inProgress.Any(s => DriverCard.TextShowsDistance(s.Lines)), Is.False,
                    "10082-NDT-10-P03 not met: the progress view still shows the discovery distance.");
                Assert.That(inProgress.Any(s => s.Progress!.Text == ConnectingText && s.Progress.Spinner), Is.True,
                    $"10082-NDT-10-P04 not met: '{ConnectingText}' with a spinner was not shown while connecting (states: [{string.Join(" > ", states)}]).");
                Assert.That(inProgress.Any(s => s.Progress!.Text == SendingDataText && s.Progress.Spinner), Is.True,
                    $"10082-NDT-10-P06 not met: '{SendingDataText}' with a spinner was not shown while sending (states: [{string.Join(" > ", states)}]).");
                Assert.That(cancelChecked.Count > 0 && cancelChecked.All(s => s.HasButton(Cancel)), Is.True,
                    $"10082-NDT-10-P07 not met: '{Cancel}' is not offered throughout the in-progress states " +
                    $"(first without it: {cancelChecked.FirstOrDefault(s => !s.HasButton(Cancel))}).");
                Assert.That(afterSelect.Any(s => s.Determinate), Is.False,
                    $"10082-NDT-10-N01 not met: Flex showed determinate progress (a progress bar, percentage or byte/packet count) ({afterSelect.FirstOrDefault(s => s.Determinate)}).");
            });
        }

        // 10082-NDT-10-P08, N02, P09, P10, N03, E01: right after the Driver App is picked, a tap outside the menu and
        // Escape (which must not dismiss or cancel it), then Cancel. A confirmed Cancel returns to the unchanged ticket and
        // the Driver App gets no load; if the Driver App committed first, success must win
        [Test]
        [Requirements("NDT-10-P08", "NDT-10-N02", "NDT-10-P09", "NDT-10-P10", "NDT-10-N03", "NDT-10-E01")]
        public void NDT10_CancelDuringTransfer_ReturnsToUnchangedTicket()
        {
            var (flow, ticketId, _) = StartFlexOnSubmittedTicket("Nearby transfer cancel");
            PrepareGridDriverApps();
            var ticketsBefore = flow.Forms.GetSubmittedTicketIds();
            var cardBefore = flow.Forms.GetSubmittedCardValues(ticketId);
            var menu = OpenNearby(flow, ticketId);
            var own = FindOwnDriverApp(menu);

            menu.StartRecording();
            Assert.That(menu.SelectThenDismissAndCancel(own.Key, dismissAfterMs: 150, cancelAfterMs: 500), Is.True,
                $"The Driver App card '{own.Key}' was gone before it could be selected.");
            Thread.Sleep(1000);
            bool closed = menu.WaitFor(s => !s.Open, 5) != null;
            // Long enough for a commit the Cancel did not stop to show on the Driver App
            Thread.Sleep(5000);
            bool driverHasLoad = own.Home.HasActiveLoad;
            var recording = menu.StopRecording();
            var final = recording.Log.LastOrDefault();

            long? tDismiss = recording.MarkTime("tap-outside");
            long? tCancel = recording.MarkTime("cancel");
            long? tCancelMissing = recording.MarkTime("cancel-missing");
            var atDismiss = tDismiss is { } d ? recording.At(d) : null;
            bool dismissedWhileInProgress = atDismiss?.Progress != null;
            long passiveWindowEnd = tCancel ?? tCancelMissing ?? (tDismiss ?? 0) + 1000;
            bool closedByPassive = dismissedWhileInProgress && recording.Between(tDismiss!.Value, passiveWindowEnd).Any(s => !s.Open);
            var atCancel = tCancel is { } c ? recording.At(c) : tCancelMissing is { } m ? recording.At(m) : null;
            bool flexCanceled = tCancel is { } tc && recording.Log.Any(s => s.T > tc && s.T <= tc + 3000 && !s.Open);
            Console.WriteLine($"📡 At the tap outside / Escape: {atDismiss}. At Cancel ({(tCancel != null ? "tapped" : "not offered")}): {atCancel}. " +
                              $"Menu closed: {closed}; Flex canceled: {flexCanceled}; Driver App has a load: {driverHasLoad}.");

            IReadOnlyList<string>? ticketsAfter = null;
            Dictionary<string, string>? cardAfter = null;
            if (flexCanceled)
            {
                try
                {
                    ticketsAfter = flow.Forms.GetSubmittedTicketIds();
                    cardAfter = flow.Forms.GetSubmittedCardValues(ticketId);
                }
                catch (WebDriverTimeoutException ex)
                {
                    Console.WriteLine($"⚠️ Flex is no longer on the Submitted list after the cancel: {ex.Message}");
                }
            }

            BaselineRecorder.Save("NDT-10-P10", Node, new
            {
                ticketId, own.Key, dismissedWhileInProgress, closedByPassive, cancelTapped = tCancel != null, atCancel = atCancel?.ToString(),
                flexCanceled, driverHasLoad, final = final?.ToString()
            });

            if (!dismissedWhileInProgress)
            {
                Assert.Warn($"10082-NDT-10-P08 / N02 not checked: the tap outside and Escape (150 ms after selecting) did not land in an in-progress state ({atDismiss}).");
            }
            if (tCancel == null && atCancel?.Progress == null)
            {
                Assert.Warn($"10082-NDT-10-P09 / P10 not checked: 500 ms after selecting there was no in-progress transfer to cancel ({atCancel}).");
            }
            if (tCancel != null && !flexCanceled && driverHasLoad)
            {
                Assert.Warn("10082-NDT-10-P10 / N03 not checked: the Driver App committed the transfer before Cancel took effect (E01 checked instead).");
            }
            Assert.Multiple(() =>
            {
                if (dismissedWhileInProgress)
                {
                    Assert.That(closedByPassive, Is.False,
                        "10082-NDT-10-P08 / N02 not met: a tap outside the menu or Escape dismissed it during an in-progress transfer.");
                }
                if (tCancel == null)
                {
                    Assert.That(atCancel?.Progress, Is.Null,
                        $"10082-NDT-10-P07 / P09 not met: no '{Cancel}' was offered during the in-progress transfer ({atCancel}).");
                }
                else if (flexCanceled)
                {
                    Assert.That(driverHasLoad, Is.False,
                        "10082-NDT-10-N03 not met: Flex canceled the transfer, yet the Driver App holds a new load from it.");
                    Assert.That(ticketsAfter, Is.EqualTo(ticketsBefore),
                        "10082-NDT-10-P10 not met: after the cancel Flex is not back on the Submitted list the Nearby action started from.");
                    Assert.That(cardAfter, Is.EqualTo(cardBefore),
                        $"10082-NDT-10-P10 not met: ticket {ticketId} changed after the canceled transfer.");
                }
                else if (driverHasLoad)
                {
                    Assert.That(final?.Lines.Any(l => Canceled.IsMatch(l)), Is.Not.True,
                        $"10082-NDT-10-E01 not met: the Driver App committed the transfer, yet Flex reports it as canceled ({final}).");
                }
                else
                {
                    Assert.Fail($"10082-NDT-10-P09 / P10 not met: Cancel was tapped during the transfer ({atCancel}), but Flex did not return to the ticket " +
                                $"within 3 s and the Driver App got no load ({final}).");
                }
            });
        }
    }
}
