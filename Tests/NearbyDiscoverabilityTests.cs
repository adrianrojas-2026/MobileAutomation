using MobileAutomation.Tests.Reporting;
using System.Diagnostics;
using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Flows;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Utilities;
using MobileAutomation.Tests.Pages.Driver;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace RecoveryTrac.AutomatedTests
{
    // R1-C1 FEAT-NDT-1 (Driver App Nearby Discoverability, #10082): a Driver app open in the foreground can be found
    // by Flex without the driver doing anything for it - no receive mode, on any page, and again after coming back
    // from the background.
    //
    // The Driver app shows no code to look for, so each test finds its own Driver app by difference: Flex's Nearby menu
    // is searched with every grid Driver app stopped, then the new card that shows up once this node's Driver app runs
    // is the one under test. That is why the fixture runs on its own (all emulators share one simulated Bluetooth) and
    // stops the Driver app on the other grid emulators. Each check opens the Nearby menu again, so it is a new
    // discovery attempt rather than a card still listed from an earlier one.
    [TestFixture]
    [Category("BLE-R1-C1")]
    [NonParallelizable]
    public class NearbyDiscoverabilityTests : NearbyTestBase
    {
        // Flex on a submitted ticket, and the cards its Nearby menu lists with no grid Driver app running
        private sealed record NearbySearch(CollectionLoadTicketFlow Flow, string TicketId, IReadOnlySet<string> Others);

        // 10082-NDT-1-P01 / N02: Driver app open on Home, never put in receive mode, and nothing for the driver to
        // approve on its screen while Flex finds it
        [Test]
        [Requirements("NDT-1-P01", "NDT-1-N02")]
        public void NDT1_P01_N02_DriverOnHome_IsDiscoverable_WithoutReceiveMode()
        {
            var search = OpenNearbyWithOnlyOtherDevices();

            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();
            Assert.That(driverHome.IsReceiving, Is.False, "The Driver app opened in receive mode; this test must not use it.");

            string? found = FindOwnDriverApp(search, "10082-NDT-1-P01");
            var prompts = DriverPromptsShown((AndroidDriver)driverApp);
            Assert.Multiple(() =>
            {
                Assert.That(found, Is.Not.Null,
                    "10082-NDT-1-P01 / N02 not met: Flex did not list the Driver app open in the foreground on Home, without receive mode.");
                Assert.That(prompts, Is.Empty,
                    "10082-NDT-1-N02 not met: the Driver app asks the driver to approve or pair while Flex searches: " +
                    $"[{string.Join("; ", prompts)}].");
            });
        }

        // 10082-NDT-1-P02: the Driver app stays discoverable as the driver moves between its pages, Home > Receipts > Home
        [Test]
        [Requirements("NDT-1-P02")]
        public void NDT1_P02_DriverOnEveryPage_StaysDiscoverable()
        {
            var search = OpenNearbyWithOnlyOtherDevices();

            var (_, driverHome) = DriverAppFlow.StartOnHome();
            string? onHome = FindOwnDriverApp(search, "10082-NDT-1-P02 (Home)");

            driverHome.OpenTab("receipts");
            Console.WriteLine("🚚 Driver app is on Receipts.");
            string? onReceipts = FindOwnDriverApp(search, "10082-NDT-1-P02 (Receipts)");

            driverHome.OpenTab("home");
            Console.WriteLine("🚚 Driver app is back on Home.");
            string? backOnHome = FindOwnDriverApp(search, "10082-NDT-1-P02 (back on Home)");

            // The requirement is about being listed, not about keeping the same name, so a changed name is only logged
            var names = new[] { onHome, onReceipts, backOnHome }.OfType<string>().Distinct().ToList();
            if (names.Count > 1) Console.WriteLine($"📡 The Driver app was listed under different names: [{string.Join(", ", names)}].");

            Assert.Multiple(() =>
            {
                Assert.That(onHome, Is.Not.Null, "10082-NDT-1-P02 not met: Flex did not list the Driver app open on Home.");
                Assert.That(onReceipts, Is.Not.Null, "10082-NDT-1-P02 not met: Flex did not list the Driver app open on Receipts.");
                Assert.That(backOnHome, Is.Not.Null,
                    "10082-NDT-1-P02 not met: Flex did not list the Driver app after the driver moved back to Home from Receipts.");
            });
        }

        // 10082-NDT-1-N01: Driver app on Welcome and then the Tutorial, before it ever reached Home
        [Test]
        [Requirements("NDT-1-N01")]
        public void NDT1_N01_DriverBeforeHome_IsDiscoverable()
        {
            var search = OpenNearbyWithOnlyOtherDevices();

            var driverApp = DriverAppFlow.StartOnWelcome();
            Assume.That(driverApp, Is.Not.Null, "The Driver app did not open on its Welcome screen, so this case cannot be set up.");
            Console.WriteLine("🚚 Driver app is on Welcome (GET STARTED not tapped).");
            string? onWelcome = FindOwnDriverApp(search, "10082-NDT-1-N01 (Welcome)");

            // With noReset an emulator that has seen the Tutorial goes from GET STARTED straight to Home, so the Tutorial
            // is only checked when it shows; the Welcome result is reported either way
            var onboarding = new DriverOnboardingPage(driverApp!);
            onboarding.ClickGetStarted();
            bool tutorialShown = onboarding.IsTutorialDisplayed(30);
            string? onTutorial = null;
            string tutorialSkipped = string.Empty;
            if (tutorialShown)
            {
                Console.WriteLine("🚚 Driver app is on the Tutorial (SKIP not tapped).");
                onTutorial = FindOwnDriverApp(search, "10082-NDT-1-N01 (Tutorial)");
            }
            else
            {
                tutorialSkipped = onboarding.IsHomeDisplayed(5)
                    ? "it went from GET STARTED straight to Home (Tutorial already seen on this emulator)"
                    : $"it did not show the Tutorial after GET STARTED ({onboarding.DescribeLocation()})";
                Console.WriteLine($"🚚 Driver app: Tutorial not checked, {tutorialSkipped}.");
            }

            Assert.Multiple(() =>
            {
                Assert.That(onWelcome, Is.Not.Null,
                    "10082-NDT-1-N01 not met: Flex did not list the Driver app open in the foreground on its Welcome screen, " +
                    "before it reached Home.");
                if (tutorialShown)
                {
                    Assert.That(onTutorial, Is.Not.Null,
                        "10082-NDT-1-N01 not met: Flex did not list the Driver app open in the foreground on its Tutorial, " +
                        "before it reached Home.");
                }
                else
                {
                    Assert.Warn($"10082-NDT-1-N01 on the Tutorial was not checked: {tutorialSkipped}.");
                }
            });
        }

        // 10082-NDT-1-P03: back from the background, discoverable again without the driver re-enabling anything
        [Test]
        [Requirements("NDT-1-P03")]
        public void NDT1_P03_DriverBackFromBackground_IsDiscoverableAgain()
        {
            var search = OpenNearbyWithOnlyOtherDevices();

            var (driverApp, _) = DriverAppFlow.StartOnHome();
            var driverAndroid = (AndroidDriver)driverApp;
            string driverUdid = AdbHelper.GetUdid(driverAndroid)!;

            string? before = FindOwnDriverApp(search, "10082-NDT-1-P03 (before backgrounding)");
            Assert.That(before, Is.Not.Null,
                "10082-NDT-1-P03 precondition (P01) not met: Flex did not list the Driver app open on Home, so returning " +
                "from the background cannot be checked.");

            // Home key, as a driver switching to another app would
            AdbHelper.Run(driverUdid, "shell input keyevent KEYCODE_HOME");
            Console.WriteLine("🚚 Driver app sent to the background.");
            // The requirement only covers the foreground; whether it keeps advertising in the background is recorded
            string? whileBackgrounded = FindOwnDriverApp(search, "10082-NDT-1-P03 (in the background)", timeoutSeconds: 12);

            driverAndroid.ActivateApp(DriverAppFlow.DriverPackage(driverAndroid));
            Console.WriteLine("🚚 Driver app back in the foreground; nothing tapped.");
            string? after = FindOwnDriverApp(search, "10082-NDT-1-P03 (after returning)");

            BaselineRecorder.Save("NDT-1-P03", Node, new { before, listedWhileBackgrounded = whileBackgrounded != null, after });
            Assert.That(after, Is.Not.Null,
                "10082-NDT-1-P03 not met: after returning from the background, Flex did not list the Driver app again " +
                "without the driver re-enabling discoverability.");
        }

        // Control for the checks above: the same search by difference, with the Driver app started the way the transfer
        // tests start it (the POC's receive mode where a build still has it). A failure here means the search itself is
        // broken (Flex's scan, the Driver emulator's Bluetooth), not a missing NDT-1 behavior
        [Test]
        [Requirements()]
        public void NDT1_Control_DriverStarted_IsFoundByDifference()
        {
            var search = OpenNearbyWithOnlyOtherDevices();

            StartDiscoverableDriverApp();
            string? found = FindOwnDriverApp(search, "control");
            Assert.That(found, Is.Not.Null,
                "The search by difference did not find the started Driver app, so the NDT-1 results above can't be trusted.");
        }

        // Stops every grid Driver app (granting it Nearby access), logs Flex in on a submitted ticket and runs one Nearby
        // search with no grid Driver app running; what it lists is other devices (phones nearby)
        private NearbySearch OpenNearbyWithOnlyOtherDevices()
        {
            if (DriverFactory.DriverFlex is not AndroidDriver)
            {
                Assert.Ignore("Nearby discoverability needs devices (RunMode=Mobile).");
                return default!;
            }

            PrepareGridDriverApps();
            var (flow, ticketId, _) = StartFlexOnSubmittedTicket("Nearby discoverability");

            var menu = OpenNearby(flow, ticketId);
            Assert.That(menu.IsOpen(), Is.True, $"The Nearby menu did not open for ticket {ticketId}.");
            // A search that finds nothing ends after 10 s; with other devices listed it keeps going, so cap the wait
            var snapshot = menu.WaitFor(s => s.Open && s.Searching == null, 15) ?? menu.Snapshot();
            var others = snapshot.Cards.Select(c => c.Key).ToHashSet();
            Console.WriteLine($"📡 With no grid Driver app running Flex lists: [{string.Join(", ", others)}].");
            CloseMenu(menu);
            return new NearbySearch(flow, ticketId, others);
        }

        // Opens the Nearby menu again (a new discovery attempt) and waits for a card that was not listed before this
        // node's Driver app ran; Try Again whenever an attempt ends without one. Returns its key, or null
        private static string? FindOwnDriverApp(NearbySearch search, string check, int timeoutSeconds = 25)
        {
            var menu = OpenNearby(search.Flow, search.TicketId);
            Assert.That(menu.IsOpen(), Is.True, $"{check}: the Nearby menu did not open for ticket {search.TicketId}.");
            var timer = Stopwatch.StartNew();
            var found = menu.WaitFor(s =>
            {
                if (s.Cards.Any(c => !search.Others.Contains(c.Key))) return true;
                if (s.Searching == null && (s.NoResults != null || s.HasButton(NearbyDriverMenuPage.TryAgain))) menu.RestartSearch();
                return false;
            }, timeoutSeconds);
            var card = found?.Cards.First(c => !search.Others.Contains(c.Key));
            Console.WriteLine(card != null
                ? $"📡 {check}: Flex lists the Driver app as '{card.Key}' ({string.Join(" / ", card.Lines)}) after {timer.Elapsed.TotalSeconds:N1} s."
                : $"📡 {check}: Flex did not list the Driver app within {timeoutSeconds} s ({menu.Snapshot()}).");
            CloseMenu(menu);
            return card?.Key;
        }

        private static void CloseMenu(NearbyDriverMenuPage menu)
        {
            menu.StopRecording();
            menu.ClickButton("Close");
            Assert.That(menu.WaitFor(s => !s.Open, 10), Is.Not.Null, $"The Nearby menu did not close ({menu.Snapshot()}).");
        }

        // Anything on the Driver device waiting for the driver: a window of another app over the Driver app (a Bluetooth
        // pairing request, a permission dialog) or an Ionic alert inside it. Each entry names where it came from
        private static IReadOnlyList<string> DriverPromptsShown(AndroidDriver driverApp)
        {
            var prompts = new List<string>();
            string context = driverApp.Context;
            if (context.StartsWith("WEBVIEW", StringComparison.Ordinal))
            {
                prompts.AddRange(driverApp.FindElements(By.CssSelector("ion-alert")).Where(e => e.Displayed)
                    .Select(e => $"Driver app alert '{e.Text.Trim()}'"));
            }

            driverApp.Context = "NATIVE_APP";
            try
            {
                string package = DriverAppFlow.DriverPackage(driverApp);
                prompts.AddRange(driverApp.FindElements(By.XPath($"//*[@package!='{package}' and @package!='com.android.systemui']"))
                    .GroupBy(e => e.GetAttribute("package"))
                    .Select(g => $"{g.Key} window '{string.Join(" ", g.Select(e => e.GetAttribute("text")).Where(t => !string.IsNullOrWhiteSpace(t)).Take(5))}'"));
            }
            finally
            {
                if (context.StartsWith("WEBVIEW", StringComparison.Ordinal))
                {
                    DriverFactory.SwitchToWebViewContext(driverApp);
                }
            }
            return prompts;
        }
    }
}
