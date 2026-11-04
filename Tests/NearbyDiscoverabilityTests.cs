using System.Diagnostics;
using Microsoft.Extensions.Configuration;
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
    // from the background. The POC only advertises after 'Receive via Bluetooth' on Home, so these fail until R1.
    //
    // Without receive mode the Driver app shows no code to look for, so each test finds its own Driver app by
    // difference: Flex's picker is searched with every grid Driver app stopped, then the new entry that shows up once
    // this node's Driver app runs is the one under test. That is why the fixture runs on its own (all emulators share
    // one simulated Bluetooth) and stops the Driver app on the other grid emulators.
    [TestFixture]
    [Category("BLE-R1-C1")]
    [NonParallelizable]
    public class NearbyDiscoverabilityTests : DualAppTestBase
    {
        private const string Node = "Node2";
        private const string DriverPackage = "com.tetratech.recoverytracdriver";

        protected override string NodeForCurrentTest => Node;
        protected override string? AccountForCurrentTest => "testuser2";

        // 10082-NDT-1-P01 / N02: Driver app open on Home, never put in receive mode, and nothing for the driver to
        // approve on its screen while Flex finds it
        [Test]
        public void NDT1_P01_N02_DriverOnHome_IsDiscoverable_WithoutReceiveMode()
        {
            var (picker, othersListed) = OpenPickerWithOnlyOtherDevices();

            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();
            Assert.That(driverHome.IsReceiving, Is.False, "The Driver app opened in receive mode; this test must not use it.");

            string? found = FindOwnDriverApp(picker, othersListed, "10082-NDT-1-P01");
            var prompts = DriverPromptsShown((AndroidDriver)driverApp);
            Assert.Multiple(() =>
            {
                Assert.That(found, Is.Not.Null,
                    "10082-NDT-1-P01 / N02 not met: Flex did not list the Driver app open in the foreground on Home, without " +
                    "receive mode (the POC only advertises after 'Receive via Bluetooth').");
                Assert.That(prompts, Is.Empty,
                    "10082-NDT-1-N02 not met: the Driver app asks the driver to approve or pair while Flex searches: " +
                    $"[{string.Join("; ", prompts)}].");
            });
        }

        // 10082-NDT-1-P02: the Driver app stays discoverable as the driver moves between its pages, Home > Receipts > Home
        [Test]
        public void NDT1_P02_DriverOnEveryPage_StaysDiscoverable()
        {
            var (picker, othersListed) = OpenPickerWithOnlyOtherDevices();

            var (_, driverHome) = DriverAppFlow.StartOnHome();
            string? onHome = FindOwnDriverApp(picker, othersListed, "10082-NDT-1-P02 (Home)");

            driverHome.OpenTab("receipts");
            Console.WriteLine("🚚 Driver app is on Receipts.");
            string? onReceipts = FindOwnDriverApp(picker, othersListed, "10082-NDT-1-P02 (Receipts)");

            driverHome.OpenTab("home");
            Console.WriteLine("🚚 Driver app is back on Home.");
            string? backOnHome = FindOwnDriverApp(picker, othersListed, "10082-NDT-1-P02 (back on Home)");

            // The requirement is about being listed, not about keeping the same name, so a changed name is only logged
            var names = new[] { onHome, onReceipts, backOnHome }.OfType<string>().Distinct().ToList();
            if (names.Count > 1) Console.WriteLine($"📡 The Driver app was listed under different names: [{string.Join(", ", names)}].");

            Assert.Multiple(() =>
            {
                Assert.That(onHome, Is.Not.Null, "10082-NDT-1-P02 not met: Flex did not list the Driver app open on Home.");
                Assert.That(onReceipts, Is.Not.Null,
                    "10082-NDT-1-P02 not met: Flex did not list the Driver app open on Receipts " +
                    "(the POC only advertises from Home, after 'Receive via Bluetooth').");
                Assert.That(backOnHome, Is.Not.Null,
                    "10082-NDT-1-P02 not met: Flex did not list the Driver app after the driver moved back to Home from Receipts.");
            });
        }

        // 10082-NDT-1-N01: Driver app on Welcome and then the Tutorial, before it ever reached Home
        [Test]
        public void NDT1_N01_DriverBeforeHome_IsDiscoverable()
        {
            var (picker, othersListed) = OpenPickerWithOnlyOtherDevices();

            var driverApp = DriverAppFlow.StartOnWelcome();
            Assume.That(driverApp, Is.Not.Null, "The Driver app did not open on its Welcome screen, so this case cannot be set up.");
            Console.WriteLine("🚚 Driver app is on Welcome (GET STARTED not tapped).");
            string? onWelcome = FindOwnDriverApp(picker, othersListed, "10082-NDT-1-N01 (Welcome)");

            var onboarding = new DriverOnboardingPage(driverApp!);
            onboarding.ClickGetStarted();
            Assume.That(onboarding.IsTutorialDisplayed(30), Is.True,
                $"The Driver app did not open its Tutorial after GET STARTED ({onboarding.DescribeLocation()}).");
            Console.WriteLine("🚚 Driver app is on the Tutorial (SKIP not tapped).");
            string? onTutorial = FindOwnDriverApp(picker, othersListed, "10082-NDT-1-N01 (Tutorial)");

            Assert.Multiple(() =>
            {
                Assert.That(onWelcome, Is.Not.Null,
                    "10082-NDT-1-N01 not met: Flex did not list the Driver app open in the foreground on its Welcome screen, " +
                    "before it reached Home.");
                Assert.That(onTutorial, Is.Not.Null,
                    "10082-NDT-1-N01 not met: Flex did not list the Driver app open in the foreground on its Tutorial, " +
                    "before it reached Home.");
            });
        }

        // 10082-NDT-1-P03: back from the background, discoverable again without the driver re-enabling anything
        [Test]
        public void NDT1_P03_DriverBackFromBackground_IsDiscoverableAgain()
        {
            var (picker, othersListed) = OpenPickerWithOnlyOtherDevices();

            var (driverApp, _) = DriverAppFlow.StartOnHome();
            var driverAndroid = (AndroidDriver)driverApp;
            string driverUdid = AdbHelper.GetUdid(driverAndroid)!;

            string? before = FindOwnDriverApp(picker, othersListed, "10082-NDT-1-P03 (before backgrounding)");
            Assert.That(before, Is.Not.Null,
                "10082-NDT-1-P03 precondition (P01) not met: Flex did not list the Driver app open on Home, so returning " +
                "from the background cannot be checked.");

            // Home key, as a driver switching to another app would
            AdbHelper.Run(driverUdid, "shell input keyevent KEYCODE_HOME");
            Console.WriteLine("🚚 Driver app sent to the background.");
            picker.Rescan();
            string? whileBackgrounded = picker.FindTruck(code => code == before, before!, searches: 1);
            // The requirement only covers the foreground; whether it keeps advertising in the background is recorded
            Console.WriteLine($"📡 While in the background the Driver app was {(whileBackgrounded != null ? "still" : "not")} listed.");

            driverAndroid.ActivateApp(DriverAppFlow.DriverPackage(driverAndroid));
            Console.WriteLine("🚚 Driver app back in the foreground; nothing tapped.");
            string? after = FindOwnDriverApp(picker, othersListed, "10082-NDT-1-P03 (after returning)");

            BaselineRecorder.Save("NDT-1-P03", Node, new { before, listedWhileBackgrounded = whileBackgrounded != null, after });
            Assert.That(after, Is.Not.Null,
                "10082-NDT-1-P03 not met: after returning from the background, Flex did not list the Driver app again " +
                "without the driver re-enabling discoverability.");
        }

        // Control for the checks above: the same search by difference, with the Driver app in the POC's receive mode.
        // It passes on the POC, so a failure above means the Driver app was not advertising, not that the search
        // cannot find it
        [Test]
        public void NDT1_Control_DriverInReceiveMode_IsFoundByDifference()
        {
            var (picker, othersListed) = OpenPickerWithOnlyOtherDevices();

            var (driverApp, driverHome) = DriverAppFlow.StartOnHome();
            driverHome.CancelStaleLoadIfPresent();
            driverHome.ReceiveViaBluetooth();
            NearbyDevicesPermission.AllowWhenAsked((AndroidDriver)driverApp, timeoutSeconds: 5);
            string truckCode = driverHome.WaitForTruckCode();

            string? found = FindOwnDriverApp(picker, othersListed, "control");
            Assert.That(found, Is.EqualTo(truckCode),
                "The search by difference did not find the Driver app even in receive mode, so the NDT-1 results above can't be trusted.");
        }

        // Stops every grid Driver app (granting it Nearby access), logs Flex in and opens the 'Send via Bluetooth'
        // picker on a submitted ticket, then returns what one search lists with no grid Driver app running (other phones
        // nearby)
        private (BleTruckPickerPage Picker, IReadOnlyList<string> OthersListed) OpenPickerWithOnlyOtherDevices()
        {
            if (DriverFactory.DriverFlex is not AndroidDriver flexAndroid)
            {
                Assert.Ignore("Nearby discoverability needs devices (RunMode=Mobile).");
                return default;
            }

            PrepareGridDriverApps();

            var flow = new CollectionLoadTicketFlow(DriverFactory.DriverFlex, _wait.Value!, Node);
            flow.LoginAndEnterWebView();
            string ticketId = AnySubmittedTicket(flow);

            flow.Forms.OpenSubmittedCardBluetooth(ticketId);
            NearbyDevicesPermission.AllowWhenAsked(flexAndroid, timeoutSeconds: 5);
            var picker = new BleTruckPickerPage(DriverFactory.DriverFlex);
            Assert.That(picker.IsOpen(), Is.True, $"The 'Send via Bluetooth' picker did not open for ticket {ticketId}.");

            var othersListed = picker.WaitForSearchToEnd();
            Console.WriteLine($"📡 With no grid Driver app running Flex lists: [{string.Join(", ", othersListed)}].");
            return (picker, othersListed);
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

        // Searches again until an entry that was not listed before this node's Driver app ran shows up
        private static string? FindOwnDriverApp(BleTruckPickerPage picker, IReadOnlyList<string> othersListed, string check)
        {
            picker.Rescan();
            var timer = Stopwatch.StartNew();
            string? found = picker.FindTruck(code => !othersListed.Contains(code), "this node's Driver app", searches: 2);
            Console.WriteLine(found != null
                ? $"📡 {check}: Flex lists the Driver app as '{found}' after {timer.Elapsed.TotalSeconds:N1} s."
                : $"📡 {check}: Flex did not list the Driver app in 2 searches ({timer.Elapsed.TotalSeconds:N1} s).");
            return found;
        }

        // The ticket only carries the send; any submitted 'Load - Collection' ticket opens the picker. One is submitted
        // when the account has none yet
        private static string AnySubmittedTicket(CollectionLoadTicketFlow flow)
        {
            flow.Forms.OpenFormsMenu();
            flow.Forms.OpenForm(CollectionLoadTicketFlow.FormName);
            var submitted = flow.Forms.GetSubmittedTicketIds();
            if (submitted.Count > 0) return submitted[0];

            var draft = flow.FillAndSubmitTicket($"Automated test - Nearby discoverability {Node} {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            flow.TicketForm.DeclineQrGeneration();
            return flow.VerifyTicketSubmittedAndSynced(draft);
        }

        // All grid emulators share one simulated Bluetooth, so a Driver app left open on another node would be listed
        // too. The fixture is NonParallelizable, so none of them is in use by another test.
        // NDT-1 only applies while the runtime access Nearby needs is available, so it is granted up front: a Driver app
        // left waiting on Android's Nearby devices question would fail for the wrong reason, and N02 would count the
        // question as a prompt. Granting (unlike revoking) does not stop the app; on Android 11 and older pm grant just
        // fails, as these permissions don't exist there
        private static void PrepareGridDriverApps()
        {
            var online = AdbHelper.OnlineDevices();
            var driverUdids = TestConfiguration.Configuration.GetSection("MobileConfigurations").GetChildren()
                .Where(c => c.Key.EndsWith("_Driver", StringComparison.Ordinal) && c["Udid"] is { } udid && online.Contains(udid))
                .Select(c => c["Udid"]!)
                .Distinct();
            foreach (string udid in driverUdids)
            {
                AdbHelper.Run(udid, $"shell am force-stop {DriverPackage}");
                foreach (string permission in NearbyDevicesPermission.Driver)
                {
                    AdbHelper.Run(udid, $"shell pm grant {DriverPackage} {permission}");
                }
            }
            Console.WriteLine("🛑 Driver app stopped, with Nearby devices access granted, on every online grid Driver emulator.");
        }
    }
}
