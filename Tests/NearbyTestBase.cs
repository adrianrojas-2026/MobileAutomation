using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Flows;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;

namespace MobileAutomation.Tests.Tests;

// The Driver App this node's test started and how Flex lists it. FoundAfter counts from the start of the discovery
// attempt that found it (SinceAttempt). Others: the card keys Flex listed before this Driver App was started
public record OwnDriverApp(string Key, IWebDriver App, DriverHomePage Home, Stopwatch SinceAttempt, double FoundAfter, MenuSnapshot WhenFound,
    IReadOnlySet<string> Others);

// What Flex reported after a Driver App card was picked: its success toast / 'Sent to' line, or the failure's reason.
// SinceSelect keeps running from the tap; MenuStillOpen is true when Flex kept the menu open (older builds: Done)
public record SendOutcome(string Result, Stopwatch SinceSelect, double Seconds, bool MenuStillOpen);

// Feature #10082 Nearby tests (R1-C1 discoverability, R1-C2 transfer core) on Node 2 with testuser2. All emulators
// share one simulated Bluetooth, so these fixtures run on their own and stop the Driver app on the other grid
// emulators; a Driver App Flex lists is then the one this test started (checked by difference, as other phones
// could be nearby).
public abstract class NearbyTestBase : DualAppTestBase
{
    protected const string Node = "Node2";
    protected const string FlexPackage = "com.tetratech.recoverytracflex";
    protected const string DriverPackage = "com.tetratech.recoverytracdriver";

    protected override string NodeForCurrentTest => Node;
    protected override string? AccountForCurrentTest => "testuser2";

    // All grid emulators share one simulated Bluetooth, so a Driver app left open on another node would be listed
    // too. The fixtures are NonParallelizable, so none of them is in use by another test.
    // NDT-1 only applies while the runtime access Nearby needs is available, so it is granted up front: a Driver app
    // left waiting on Android's Nearby devices question would fail for the wrong reason, and N02 would count the
    // question as a prompt
    protected static void PrepareGridDriverApps()
    {
        var online = AdbHelper.OnlineDevices();
        var driverUdids = TestConfiguration.Configuration.GetSection("MobileConfigurations").GetChildren()
            .Where(c => c.Key.EndsWith("_Driver", StringComparison.Ordinal) && c["Udid"] is { } udid && online.Contains(udid))
            .Select(c => c["Udid"]!)
            .Distinct();
        foreach (string udid in driverUdids)
        {
            AdbHelper.Run(udid, $"shell am force-stop {DriverPackage}");
            NearbyDevicesPermission.Grant(udid, DriverPackage, NearbyDevicesPermission.Driver);
        }
        Console.WriteLine("🛑 Driver app stopped, with Nearby devices access granted, on every online grid Driver emulator.");
    }

    // The ticket only carries the send; any submitted 'Load - Collection' ticket opens the Nearby menu. One is
    // submitted when the account has none yet
    protected static string AnySubmittedTicket(CollectionLoadTicketFlow flow, string purpose)
    {
        flow.Forms.OpenFormsMenu();
        flow.Forms.OpenForm(CollectionLoadTicketFlow.FormName);
        var submitted = flow.Forms.GetSubmittedTicketIds();
        if (submitted.Count > 0) return submitted[0];

        var draft = flow.FillAndSubmitTicket($"Automated test - {purpose} {Node} {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        flow.TicketForm.DeclineQrGeneration();
        return flow.VerifyTicketSubmittedAndSynced(draft);
    }

    // R1-C2: Flex logged in on the Submitted list of 'Load - Collection', with Bluetooth on. grantNearby gives Flex its
    // Nearby devices access up front, so the shared Bluetooth Access flow (NDT-6, tested on its own) stays out of the
    // way; without it the access is revoked and Android asks again, as after a fresh install
    // freshTicket submits a new ticket instead of reusing one, for tests that check the load the Driver App stores
    protected (CollectionLoadTicketFlow Flow, string TicketId, string FlexUdid) StartFlexOnSubmittedTicket(string purpose, bool grantNearby = true,
        bool freshTicket = false)
    {
        if (DriverFactory.DriverFlex is not AndroidDriver flexAndroid)
        {
            Assert.Ignore("Nearby transfer needs devices (RunMode=Mobile).");
            return default;
        }
        string flexUdid = AdbHelper.GetUdid(flexAndroid)!;

        if (!BluetoothHelper.IsBluetoothOn(flexUdid))
        {
            Assert.That(BluetoothHelper.SetBluetooth(flexUdid, true), Is.True, $"Bluetooth is off on the Flex device {flexUdid} and did not turn on.");
        }
        if (grantNearby)
        {
            NearbyDevicesPermission.Grant(flexUdid, FlexPackage, NearbyDevicesPermission.Flex);
        }
        else
        {
            // Revoking a permission stops the app, so relaunch it before logging in
            NearbyDevicesPermission.Reset(flexUdid, FlexPackage, NearbyDevicesPermission.Flex);
            flexAndroid.ActivateApp(FlexPackage);
        }

        var flow = new CollectionLoadTicketFlow(DriverFactory.DriverFlex, _wait.Value!, Node);
        flow.LoginAndEnterWebView();
        string ticketId;
        if (freshTicket)
        {
            var draft = flow.FillAndSubmitTicket($"Automated test - {purpose} {Node} {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            flow.TicketForm.DeclineQrGeneration();
            ticketId = flow.VerifyTicketSubmittedAndSynced(draft);
        }
        else
        {
            ticketId = AnySubmittedTicket(flow, purpose);
        }
        return (flow, ticketId, flexUdid);
    }

    // The Producer's Nearby action on a submitted ticket: the card's Bluetooth icon (POC). Update
    // FormsPage.OpenSubmittedCardBluetooth if R1 moves the entry point. The menu is recorded from just before the tap
    // ('nearby-action' mark), so what it showed first and when can be read back
    protected static NearbyDriverMenuPage OpenNearby(CollectionLoadTicketFlow flow, string ticketId)
    {
        var menu = new NearbyDriverMenuPage(DriverFactory.DriverFlex);
        menu.StartRecording();
        menu.Mark("nearby-action");
        flow.Forms.OpenSubmittedCardBluetooth(ticketId);
        Console.WriteLine($"📡 Nearby action tapped on ticket {ticketId}.");
        return menu;
    }

    // Seconds between two browser timestamps (MenuSnapshot.T / MenuMark.T)
    protected static double Seconds(long from, long to) => Math.Round((to - from) / 1000.0, 1);

    protected static bool? IsFlexScanning(string flexUdid) => BluetoothHelper.IsScanning(flexUdid, FlexPackage);

    // This node's Driver app on Home with no load, findable by Flex. On the POC that takes 'Receive via Bluetooth'; R1
    // drops that button, the app being discoverable whenever it is in the foreground (NDT-1). R1-C2 is about Flex,
    // so the POC's receive mode is used where it exists rather than failing on an R1-C1 gap
    protected static (IWebDriver App, DriverHomePage Home) StartDiscoverableDriverApp()
    {
        var (driverApp, driverHome) = DriverAppFlow.StartOnHome();
        if (driverApp is AndroidDriver driverDevice)
        {
            BluetoothHelper.EnsureBluetoothOn(AdbHelper.GetUdid(driverDevice)!);
        }
        driverHome.CancelStaleLoadIfPresent();
        if (driverHome.IsReceiveViaBluetoothShown())
        {
            driverHome.ReceiveViaBluetooth();
            NearbyDevicesPermission.AllowWhenAsked((AndroidDriver)driverApp, timeoutSeconds: 5);
            Console.WriteLine($"🚚 Driver app advertising in the POC's receive mode as {driverHome.WaitForTruckCode()}.");
        }
        else
        {
            Console.WriteLine("🚚 Driver app on Home with no receive mode button: discoverable in the foreground (R1).");
        }
        return (driverApp, driverHome);
    }

    // This node's Driver app on its Welcome screen (GET STARTED not tapped) with no load, in its WEBVIEW context and
    // recording toasts. A stale load can only be cancelled on Home, so it goes there first and then cold-starts the
    // app, which opens on Welcome again. Inconclusive when the app does not come back on Welcome
    protected static (IWebDriver App, DriverHomePage Home) StartDriverAppOnWelcome()
    {
        var (driverApp, driverHome) = DriverAppFlow.StartOnHome();
        var android = (AndroidDriver)driverApp;
        BluetoothHelper.EnsureBluetoothOn(AdbHelper.GetUdid(android)!);
        driverHome.CancelStaleLoadIfPresent();

        android.Context = "NATIVE_APP";
        string package = DriverAppFlow.DriverPackage(android);
        android.TerminateApp(package);
        android.ActivateApp(package);
        Assume.That(new DriverOnboardingPage(driverApp).IsWelcomeScreenDisplayed(60), Is.True,
            "The Driver app did not open on its Welcome screen after a cold start, so this case cannot be set up.");

        DriverFactory.SwitchToWebViewContext(driverApp);
        Assume.That(android.Context, Does.StartWith("WEBVIEW"), "Could not switch to the Driver app's WEBVIEW context on Welcome.");
        IonicToasts.StartRecording(driverApp);
        Console.WriteLine("🚚 Driver app on Welcome (GET STARTED not tapped), no load, recording toasts.");
        return (driverApp, driverHome);
    }

    // With the Nearby menu open and no grid Driver app running: notes what this first attempt lists (other devices),
    // starts this node's Driver app (on Home unless startDriverApp says otherwise), starts another attempt and returns
    // the new card, which is this node's Driver App
    protected static OwnDriverApp FindOwnDriverApp(NearbyDriverMenuPage menu, Func<(IWebDriver App, DriverHomePage Home)>? startDriverApp = null)
    {
        Assert.That(menu.IsOpen(), Is.True, "The Nearby menu did not open after the Nearby action was tapped.");
        var firstAttemptEnd = menu.WaitFor(s => s.Open && s.Searching == null, 20) ?? menu.Snapshot();
        var others = firstAttemptEnd.Cards.Select(c => c.Key).ToHashSet();
        Console.WriteLine($"📡 With no grid Driver app running Flex lists: [{string.Join(", ", others)}].");

        var (driverApp, driverHome) = (startDriverApp ?? StartDiscoverableDriverApp)();

        if (!menu.RestartSearch())
        {
            Console.WriteLine($"📡 No Try Again / Rescan offered ({menu.Snapshot()}); watching the current attempt.");
        }
        var sinceAttempt = Stopwatch.StartNew();
        var found = menu.WaitFor(s => s.Cards.Any(c => !others.Contains(c.Key)), 25);
        Assert.That(found, Is.Not.Null,
            "Precondition not met: Flex did not list this node's Driver App within 25 s, so the R1-C2 checks cannot run. " +
            $"Menu: {menu.Snapshot()}. If the NDT1_* tests (BLE-R1-C1) fail too, the Driver App is not advertising.");

        var own = found!.Cards.First(c => !others.Contains(c.Key));
        double foundAfter = sinceAttempt.Elapsed.TotalSeconds;
        Console.WriteLine($"📡 Flex lists this node's Driver App as '{own.Key}' ({string.Join(" / ", own.Lines)}) after {foundAfter:N1} s.");
        return new OwnDriverApp(own.Key, driverApp, driverHome, sinceAttempt, foundAfter, found, others);
    }

    // This node's Driver App card once it can be picked. A new card first shows 'Loading truck details…' (not
    // selectable, keyed by its Identity Code) and becomes the truck's card when its details arrive (keyed by the truck),
    // or stays a 'Driver App' card when the read fails (after 5 s); either way it is the card not listed before
    protected static DriverCard WaitForSelectableCard(NearbyDriverMenuPage menu, OwnDriverApp own, int timeoutSeconds = 15)
    {
        bool Ready(DriverCard c) => !own.Others.Contains(c.Key) && !c.Disabled && !c.Lines.Contains(NearbyDriverMenuPage.LoadingTruckDetails);
        var ready = menu.WaitFor(s => s.Cards.Any(Ready), timeoutSeconds);
        Assert.That(ready, Is.Not.Null,
            $"This node's Driver App card ('{own.Key}') did not become selectable within {timeoutSeconds} s ({menu.Snapshot()}).");
        var card = ready!.Cards.First(Ready);
        Console.WriteLine($"📡 Driver App card ready: '{card.Key}' ({string.Join(" / ", card.Lines)}).");
        return card;
    }

    // Picks the card and waits for Flex's outcome. FEAT-LDT-10: success closes the menu and Flex shows the toast
    // 'Data sent to Driver App <Identity Code>.' (or '... Truck <Truck Number>.'). Earlier builds kept the menu open
    // with 'Sent to <Identity Code>' and Done. A failure shows its reason with Try Again
    protected static SendOutcome SendToCard(NearbyDriverMenuPage menu, DriverCard card)
    {
        IonicToasts.StartRecording(DriverFactory.DriverFlex);
        var sinceSelect = Stopwatch.StartNew();
        menu.SelectCard(card.Key);
        var final = menu.WaitFor(s => !s.Open || s.Lines.Any(l => l.StartsWith(NearbyDriverMenuPage.SentPrefix, StringComparison.Ordinal))
                                      || s.HasButton(NearbyDriverMenuPage.TryAgain), 60);
        double seconds = sinceSelect.Elapsed.TotalSeconds;
        string result = final switch
        {
            null => $"no result within 60 s ({menu.Snapshot()})",
            { Open: false } => IonicToasts.Message(DriverFactory.DriverFlex, timeoutSeconds: 5) ?? "the Nearby menu closed without a toast",
            _ => final.Lines.FirstOrDefault(l => l.StartsWith(NearbyDriverMenuPage.SentPrefix, StringComparison.Ordinal))
                 ?? string.Join(" | ", final.Lines.Where(l => l != final.Title && !l.Equals(NearbyDriverMenuPage.TryAgain, StringComparison.OrdinalIgnoreCase)
                                                               && !l.Equals("Close", StringComparison.OrdinalIgnoreCase)))
        };
        Console.WriteLine($"📡 Flex: '{result}' after {seconds:N1} s.");
        return new SendOutcome(result, sinceSelect, seconds, final is { Open: true });
    }

    // Flex reported success, naming the Driver App that was picked
    protected static void AssertSentTo(SendOutcome sent, DriverCard card, string ticketId)
    {
        Assert.That(sent.Result, Does.StartWith(NearbyDriverMenuPage.DataSentPrefix).Or.StartWith(NearbyDriverMenuPage.SentPrefix),
            $"Flex could not send ticket {ticketId} to the Driver App '{card.Key}'.");
        if (sent.Result.StartsWith(NearbyDriverMenuPage.SentPrefix, StringComparison.Ordinal))
        {
            if (card.IdentityCode is { } code)
            {
                Assert.That(sent.Result, Is.EqualTo(NearbyDriverMenuPage.SentPrefix + code), "Flex reports a different Driver App than the one picked.");
            }
        }
        else if (card.Primary == NearbyDriverMenuPage.NoSessionPrimary && card.IdentityCode is { } code)
        {
            Assert.That(sent.Result, Is.EqualTo($"{NearbyDriverMenuPage.DataSentToDriverApp}{code}."), "Flex reports a different Driver App than the one picked.");
        }
        else
        {
            Assert.That(sent.Result, Does.StartWith(NearbyDriverMenuPage.DataSentToTruck), "Flex's success toast for a Driver App with a Driver Session names no truck.");
        }
    }

    // Closes the Nearby menu when Flex kept it open after the send (older builds: Done) and stops recording it
    protected static void CloseMenuAfterSend(NearbyDriverMenuPage menu, SendOutcome sent)
    {
        if (sent.MenuStillOpen)
        {
            menu.ClickButton(NearbyDriverMenuPage.Done);
            Assert.That(menu.WaitFor(s => !s.Open, 10), Is.Not.Null, $"The Nearby menu did not close after '{NearbyDriverMenuPage.Done}' ({menu.Snapshot()}).");
        }
        menu.StopRecording();
    }
}
