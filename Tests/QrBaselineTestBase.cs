using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Flows;
using MobileAutomation.Tests.Pages.Driver;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium;

namespace RecoveryTrac.AutomatedTests
{
    // Shared by the collection QR baselines (QrBaselineTests, QR-Bxx) and the disposal QR baselines
    // (QrDisposalBaselineTests, QR-Dxx): one node pair per fixture, a submitted collection ticket with its decoded QR,
    // and the helpers that build damaged payloads
    public abstract class QrBaselineTestBase : DualAppTestBase
    {
        // Largest payload the BLE transfer logic must carry (FR-4)
        protected const int MaxBlePayloadChars = 4200;

        protected readonly string _node;

        protected QrBaselineTestBase(string node)
        {
            _node = node;
        }

        protected override string NodeForCurrentTest => _node;

        protected record SubmittedTicket(CollectionLoadTicketFlow Flow, CollectionTicketDraft Draft, string TicketId, byte[] QrPng, string QrText);

        // Logs in on the first call, then fills, submits and syncs a ticket and decodes its QR
        protected SubmittedTicket SubmitTicket(string testName, CollectionLoadTicketFlow? loggedInFlow = null)
        {
            var flow = loggedInFlow;
            if (flow == null)
            {
                flow = new CollectionLoadTicketFlow(DriverFactory.DriverFlex, _wait.Value!, _node);
                flow.LoginAndEnterWebView();
            }

            var draft = flow.FillAndSubmitTicket(Notes(testName));
            byte[] qrPng = flow.GenerateAndStoreQr();
            string ticketId = flow.VerifyTicketSubmittedAndSynced(draft);
            string qrText = QrScanFeed.Decode(qrPng, DriverFactory.DriverFlex);
            Console.WriteLine($"🔳 Ticket {ticketId} QR payload: {qrText.Length} characters.");
            return new SubmittedTicket(flow, draft, ticketId, qrPng, qrText);
        }

        // Scans a collection QR on Driver Home (cancelling any load left over) and returns the new current load
        protected static string ScanCollectionQr(IWebDriver driverApp, DriverHomePage driverHome, byte[] qrPng)
        {
            StageOnDriver(driverApp, qrPng);
            driverHome.ScanQrCode();
            return driverHome.WaitForActiveLoad();
        }

        // Puts a QR code (collection or disposal) in front of the Driver app's scanner
        protected static void StageOnDriver(IWebDriver driverApp, byte[] qrPng) =>
            QrScanFeed.Stage(driverApp, qrPng, rasterizer: DriverFactory.DriverFlex, DriverFactory.DriverCameraFeedPath);

        // The QR_Bxx_... / QR_Dxx_... test running the scenario, for the ticket notes
        protected static string TestName => TestContext.CurrentContext.Test.MethodName!;

        protected string Notes(string testName) => $"Automated test - {testName} {_node} {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        // Changes the first letter or digit from the middle of the payload onwards to a different one of the same kind
        protected static string AlterOneCharacter(string payload)
        {
            int index = Enumerable.Range(payload.Length / 2, payload.Length - payload.Length / 2)
                .Concat(Enumerable.Range(0, payload.Length / 2))
                .First(i => char.IsAsciiLetterOrDigit(payload[i]));
            char c = payload[index];
            char altered = char.IsAsciiDigit(c) ? (char)('0' + (c - '0' + 1) % 10)
                : char.IsAsciiLetterUpper(c) ? (char)('A' + (c - 'A' + 1) % 26)
                : (char)('a' + (c - 'a' + 1) % 26);
            return string.Concat(payload.AsSpan(0, index), altered.ToString(), payload.AsSpan(index + 1));
        }

        // The two damaged variants of a real payload: what a camera can't produce but a Bluetooth transfer could
        protected static Dictionary<string, string> DamagedVariants(string payload) => new()
        {
            ["truncated-half"] = payload[..(payload.Length / 2)],
            ["one-char-altered"] = AlterOneCharacter(payload)
        };
    }
}
