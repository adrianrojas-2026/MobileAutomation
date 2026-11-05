using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using static MobileAutomation.Tests.Pages.NearbyDriverMenuPage;

namespace RecoveryTrac.AutomatedTests
{
    // R1-C2 FEAT-NDT-2 (Flex Nearby Driver Discovery Menu) and FEAT-NDT-3 (Driver App Discovery Identity Cards), #10082.
    // Flex opens discovery from a submitted ticket's Nearby action, searches at once, gives up after 10 s with no
    // Driver App, keeps searching while one is listed, and shows each Driver App as one identity card. The POC picker
    // searches for 15 s, uses other texts and lists RTD-XXXX codes, so these fail until R1; each failure names its
    // requirement ID.
    [TestFixture]
    [Category("BLE-R1-C2")]
    [NonParallelizable]
    public class NearbyDiscoveryMenuTests : NearbyTestBase
    {
        // 10082-NDT-2-P01, P02, E01, E02, E03, E05, P16, N04: no Driver App nearby. Searching at once, the no-results
        // state after 10 s, Try Again for a new 10-s attempt, then dismissing the menu mid-search
        [Test]
        public void NDT2_NoDriverApp_NoResultsAfter10s_TryAgain_DismissStopsDiscovery()
        {
            var (flow, ticketId, flexUdid) = StartFlexOnSubmittedTicket("Nearby discovery menu");
            PrepareGridDriverApps();
            var ticketsBefore = flow.Forms.GetSubmittedTicketIds();
            var cardBefore = flow.Forms.GetSubmittedCardValues(ticketId);

            // First attempt, straight from the Nearby action
            var menu = OpenNearby(flow, ticketId);
            bool? scanningWhileSearching = menu.WaitFor(s => s.Searching != null, 5) != null ? IsFlexScanning(flexUdid) : null;
            var firstEnd = menu.WaitFor(s => s.Open && s.Searching == null, 30);
            bool? scanningAfterFirst = IsFlexScanning(flexUdid);
            var first = menu.Recording();
            Assume.That(first.Log.All(s => s.Cards.Count == 0), Is.True,
                $"A device was listed ({string.Join(", ", first.Log.SelectMany(s => s.Cards).Select(c => c.Key).Distinct())}), so the no-results state cannot be checked.");

            long tAction = first.MarkTime("nearby-action")!.Value;
            var firstSearching = first.Log.FirstOrDefault(s => s.Searching != null);
            var firstStopped = firstSearching == null ? null : first.Log.FirstOrDefault(s => s.T > firstSearching.T && s.Open && s.Searching == null);
            double? searchingAfter = firstSearching == null ? null : Seconds(tAction, firstSearching.T);
            double? searchedFor = firstStopped == null ? null : Seconds(firstSearching!.T, firstStopped.T);
            Console.WriteLine($"📡 First attempt: searching after {searchingAfter} s ('{firstSearching?.Searching?.Text}'), stopped after {searchedFor} s: {firstEnd}.");

            // Try Again: a new attempt with its own 10-s interval
            bool tryAgainOffered = firstEnd?.HasButton(TryAgain) == true;
            MenuSnapshot? retrySearching = null;
            double? retrySearchedFor = null;
            if (tryAgainOffered)
            {
                menu.StartRecording();
                menu.ClickButton(TryAgain);
                menu.WaitFor(s => s.Searching != null, 5);
                menu.WaitFor(s => s.Open && s.Searching == null, 30);
                var retry = menu.Recording();
                long tClick = retry.MarkTime($"click {TryAgain}")!.Value;
                retrySearching = retry.Log.FirstOrDefault(s => s.T >= tClick && s.Searching != null);
                var retryStopped = retrySearching == null ? null : retry.Log.FirstOrDefault(s => s.T > retrySearching.T && s.Open && s.Searching == null);
                retrySearchedFor = retryStopped == null ? null : Seconds(retrySearching!.T, retryStopped.T);
                Console.WriteLine($"📡 Try Again: searching '{retrySearching?.Searching?.Text}', stopped after {retrySearchedFor} s.");
            }

            // Dismiss the menu while a new attempt is searching: tap outside it, else Android Back
            menu.RestartSearch();
            string dismissedBy = string.Empty;
            bool? scanningBeforeDismiss = null, scanningAfterDismiss = null;
            IReadOnlyList<string>? ticketsAfter = null;
            Dictionary<string, string>? cardAfter = null;
            if (menu.WaitFor(s => s.Searching != null, 5) != null)
            {
                scanningBeforeDismiss = IsFlexScanning(flexUdid);
                if (menu.TapOutside() && menu.WaitFor(s => !s.Open, 2) != null)
                {
                    dismissedBy = "tap outside";
                }
                else
                {
                    AdbHelper.Run(flexUdid, "shell input keyevent KEYCODE_BACK");
                    if (menu.WaitFor(s => !s.Open, 3) != null) dismissedBy = "Android Back";
                }
            }
            if (dismissedBy.Length > 0)
            {
                scanningAfterDismiss = IsFlexScanning(flexUdid);
                try
                {
                    ticketsAfter = flow.Forms.GetSubmittedTicketIds();
                    cardAfter = flow.Forms.GetSubmittedCardValues(ticketId);
                }
                catch (WebDriverTimeoutException ex)
                {
                    Console.WriteLine($"⚠️ Flex is no longer on the Submitted list after the menu was dismissed: {ex.Message}");
                }
            }
            Console.WriteLine($"📡 Dismissed by: '{dismissedBy}' (menu: {menu.Snapshot()}); Flex scanning before {scanningBeforeDismiss}, after {scanningAfterDismiss}.");

            BaselineRecorder.Save("NDT-2-E01", Node, new
            {
                searchingAfter, searchingText = firstSearching?.Searching?.Text, searchedFor, noResults = firstEnd?.ToString(),
                retrySearchedFor, dismissedBy, scanningWhileSearching, scanningAfterFirst, scanningBeforeDismiss, scanningAfterDismiss
            });

            if (scanningWhileSearching != true)
            {
                Assert.Warn($"Android did not show Flex scanning while it showed Searching (dumpsys: {scanningWhileSearching?.ToString() ?? "no scanner map"}), " +
                            "so 'Flex stops discovery' (E01, N04, P16) rests on the menu alone. A transport scanning through Google Play services (D2) is not counted under Flex.");
            }
            Assert.Multiple(() =>
            {
                Assert.That(searchingAfter, Is.Not.Null.And.LessThanOrEqualTo(3.0),
                    $"10082-NDT-2-P01 not met: Flex did not start searching within 3 s of the Nearby action, without a separate Search action (first state: {first.Log.FirstOrDefault(s => s.Open)}).");
                Assert.That(firstSearching?.Searching?.Text, Is.EqualTo(SearchingText),
                    "10082-NDT-2-P02 not met: the active-search message is not the required one.");
                Assert.That(firstSearching?.Searching?.Spinner, Is.True,
                    "10082-NDT-2-P02 not met: no indeterminate spinner beside the searching message.");
                Assert.That(firstEnd is { Searching: null, Spinner: false }, Is.True,
                    $"10082-NDT-2-P02 not met: the searching message or spinner is still shown once discovery stopped ({firstEnd}).");
                Assert.That(searchedFor, Is.Not.Null.And.InRange(9.5, 12.0),
                    $"10082-NDT-2-E01 not met: with no Driver App Flex searched for {searchedFor?.ToString() ?? "no measurable time"} s; it must stop after 10 s.");
                Assert.That(firstEnd?.NoResults, Is.EqualTo(NoResultsText),
                    $"10082-NDT-2-E01 not met: the no-results state does not show '{NoResultsText}' ({firstEnd}).");
                Assert.That(scanningAfterFirst, Is.Not.True,
                    "10082-NDT-2-N04 not met: Flex is still scanning after the 10-second no-results interval.");
                Assert.That(firstEnd?.HasLine(NoResultsHint), Is.True,
                    $"10082-NDT-2-E02 not met: the no-results state does not show '{NoResultsHint}'.");
                Assert.That(tryAgainOffered, Is.True,
                    $"10082-NDT-2-E03 not met: the no-results state offers no '{TryAgain}' action ({firstEnd}).");
                if (tryAgainOffered)
                {
                    Assert.That(retrySearching is { NoResults: null, Searching.Spinner: true } && retrySearching.Searching.Text == SearchingText, Is.True,
                        $"10082-NDT-2-E05 not met: after Try Again Flex did not clear the no-results state and show '{SearchingText}' with a spinner ({retrySearching}).");
                    Assert.That(retrySearchedFor, Is.Not.Null.And.InRange(9.5, 12.0),
                        $"10082-NDT-2-E05 not met: the attempt started by Try Again searched for {retrySearchedFor?.ToString() ?? "no measurable time"} s instead of a new 10-s interval.");
                }
                Assert.That(dismissedBy, Is.Not.Empty,
                    $"10082-NDT-2-P16 not met: neither tapping outside the menu nor Android Back dismissed it while searching ({menu.Snapshot()}).");
                if (dismissedBy.Length > 0)
                {
                    Assert.That(scanningAfterDismiss, Is.Not.True,
                        $"10082-NDT-2-P16 / N04 not met: Flex is still scanning after the menu was dismissed ({dismissedBy}).");
                    Assert.That(ticketsAfter, Is.EqualTo(ticketsBefore),
                        "10082-NDT-2-P16 not met: the Submitted list is not the one the Nearby action was started from.");
                    Assert.That(cardAfter, Is.EqualTo(cardBefore),
                        $"10082-NDT-2-P16 not met: ticket {ticketId} changed after the menu was dismissed.");
                }
            });
        }

        // 10082-NDT-2-P03, E06, P04, N02, N01 and NDT-3-P01, P03, P21, P12: one Driver App nearby, with no Driver Session.
        // Its card shows under the still-running search, stays past the 10-s mark while Flex keeps searching, and goes
        // away by itself once the Driver App closes
        [Test]
        public void NDT2_NDT3_DriverAppFound_ListedWhileDiscoveryContinues()
        {
            var (flow, ticketId, flexUdid) = StartFlexOnSubmittedTicket("Nearby discovery menu");
            PrepareGridDriverApps();
            var menu = OpenNearby(flow, ticketId);
            var own = FindOwnDriverApp(menu);
            var foundCard = own.WhenFound.Card(own.Key)!;

            // Watch past the 10-second no-results interval (and the POC's 15-second search)
            const double watchSeconds = 16;
            Thread.Sleep(TimeSpan.FromSeconds(Math.Max(0, watchSeconds - own.SinceAttempt.Elapsed.TotalSeconds)));
            bool? scanningAfterWatch = IsFlexScanning(flexUdid);
            var watched = menu.Recording().Log.Where(s => s.T >= own.WhenFound.T).ToList();
            var atEnd = menu.Snapshot();
            var card = atEnd.Card(own.Key) ?? foundCard;
            int[] ownCardCounts = watched.Select(s => s.Cards.Count(c => c.Key == own.Key)).Distinct().ToArray();
            Console.WriteLine($"📡 After {own.SinceAttempt.Elapsed.TotalSeconds:N1} s: {atEnd}; Flex scanning: {scanningAfterWatch}.");

            // The Driver App closes; Flex must drop its card without a manual refresh
            string driverUdid = AdbHelper.GetUdid((AndroidDriver)own.App)!;
            AdbHelper.Run(driverUdid, $"shell am force-stop {DriverPackage}");
            Console.WriteLine("🛑 Driver app closed.");
            var removed = menu.WaitFor(s => s.Card(own.Key) == null, 30);
            Console.WriteLine($"📡 {(removed != null ? "Card removed" : "Card still listed 30 s after the Driver app closed")}: {removed ?? menu.Snapshot()}.");

            BaselineRecorder.Save("NDT-2-P04", Node, new
            {
                own.Key, own.FoundAfter, card.Lines, card.Primary, card.PrimaryWeight, card.Truncated, scanningAfterWatch,
                listedAfterSeconds = Math.Round(own.SinceAttempt.Elapsed.TotalSeconds, 1), ownCardCounts, removedWithoutRefresh = removed != null
            });

            Assert.Multiple(() =>
            {
                Assert.That(own.FoundAfter, Is.LessThanOrEqualTo(10.0),
                    $"Precondition: Flex listed the Driver App after {own.FoundAfter:N1} s, past the 10-s interval, so E06 cannot hold.");
                Assert.That(own.WhenFound.Searching is { Spinner: true } && own.WhenFound.Searching.Top < foundCard.Top, Is.True,
                    $"10082-NDT-2-P03 not met: when the Driver App card appeared, the searching status and spinner were not shown above the result list ({own.WhenFound}).");
                Assert.That(watched.Any(s => s.NoResults != null), Is.False,
                    "10082-NDT-2-E06 / N02 not met: Flex showed the no-results state while the Driver App was listed.");
                Assert.That(watched.All(s => s.Open && s.Searching != null), Is.True,
                    $"10082-NDT-2-P04 not met: Flex stopped searching while the Driver App was listed (first stopped state: {watched.FirstOrDefault(s => s.Searching == null)}).");
                Assert.That(scanningAfterWatch, Is.Not.False,
                    $"10082-NDT-2-P04 not met: {watchSeconds:0} s into the attempt Flex is no longer scanning while the Driver App is listed.");
                Assert.That(ownCardCounts, Is.EqualTo(new[] { 1 }),
                    $"10082-NDT-3-P01 not met: Flex did not show exactly one card for the Driver App throughout (counts seen: [{string.Join(", ", ownCardCounts)}]).");
                Assert.That(card.Primary == NoSessionPrimary && card.PrimaryWeight >= 600, Is.True,
                    $"10082-NDT-3-P21 not met: a Driver App with no Driver Session must show '{NoSessionPrimary}' in bold as its primary identity " +
                    $"(card: '{card.Primary}', weight {card.PrimaryWeight}).");
                Assert.That(card.IdentityCode, Is.Not.Null.And.Not.Empty,
                    $"10082-NDT-3-P03 / P21 not met: the card shows no 'Identity Code: <identity code>' line ([{string.Join(" | ", card.Lines)}]).");
                Assert.That(card.ShowsDistance, Is.True,
                    $"10082-NDT-3-P21 not met: the card shows no estimated distance (nor 'Distance unavailable') ([{string.Join(" | ", card.Lines)}]).");
                Assert.That(card.Truncated, Is.False,
                    $"10082-NDT-3-P12 not met: an identity value on the card is cut off with an ellipsis ([{string.Join(" | ", card.Lines)}]).");
                Assert.That(removed, Is.Not.Null,
                    "10082-NDT-2-N01 not met: 30 s after the Driver App closed its card is still listed; the result list did not stay current without a manual refresh.");
            });
        }
    }
}
