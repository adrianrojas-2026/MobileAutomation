using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Flows;
using MobileAutomation.Tests.Tests;
using OpenQA.Selenium.Appium.Android;

namespace RecoveryTrac.AutomatedTests
{
    // R1-C2 FEAT-NDT-5 (Nearby Transfer Supported Clients), #10082. In R1 only Flex Android and Driver App Android do
    // Nearby; every other Flex client (Web, and iOS until R2) keeps its QR action and shows no Nearby action at all,
    // not even a disabled one. Flex Android (P01) is covered by every other R1-C2 test and Driver App Android (P02) by
    // the R1-C1 NDT1_* tests. This one needs Flex Web (RunMode=Web); the grid has no iOS devices.
    // The POC shows its Bluetooth icon on Flex Web too (Chrome's device chooser), so this fails until R1.
    [TestFixture]
    [Category("BLE-R1-C2")]
    public class NearbySupportedClientsTests : NearbyTestBase
    {
        // 10082-NDT-5-P05, P06, N01: a submitted ticket on Flex Web offers its QR action and no Nearby action
        [Test]
        public void NDT5_FlexWithoutNearby_KeepsQrAction_ShowsNoNearbyAction()
        {
            if (DriverFactory.DriverFlex is AndroidDriver)
            {
                Assert.Ignore("NDT-5 P05/P06/N01 are about a Flex client without Nearby (Flex Web, or Flex iOS in R1): run with RunMode=Web.");
                return;
            }

            var flow = new CollectionLoadTicketFlow(DriverFactory.DriverFlex, _wait.Value!, Node);
            flow.LoginAndEnterWebView();
            string ticketId = AnySubmittedTicket(flow, "Nearby supported clients");

            var actions = flow.Forms.GetSubmittedCardActions(ticketId);
            Console.WriteLine($"🌐 Ticket {ticketId} actions on Flex Web: [{string.Join(", ", actions.Select(a => $"{a.Name}{(a.Displayed ? string.Empty : " (hidden)")}{(a.Disabled ? " (disabled)" : string.Empty)}"))}].");
            var nearby = actions.Where(a => a.Displayed && (a.Name.Contains("bluetooth", StringComparison.OrdinalIgnoreCase)
                                                            || a.Name.Contains("nearby", StringComparison.OrdinalIgnoreCase))).ToList();
            bool qrEnabled = actions.Any(a => a.Displayed && !a.Disabled && a.Name.Contains("qr", StringComparison.OrdinalIgnoreCase));

            int qrPngBytes = 0;
            if (qrEnabled)
            {
                flow.Forms.OpenSubmittedCardQr(ticketId);
                qrPngBytes = flow.TicketForm.GetQrCodePng().Length;
                flow.TicketForm.CloseQrModal();
            }

            Assert.Multiple(() =>
            {
                Assert.That(nearby.Select(a => a.Name), Is.Empty,
                    $"10082-NDT-5-P05 / N01 not met: Flex Web, which has no Nearby, shows a Nearby action on ticket {ticketId}" +
                    $"{(nearby.Any(a => a.Disabled) ? " (disabled)" : string.Empty)}.");
                Assert.That(qrEnabled, Is.True, $"10082-NDT-5-P06 not met: ticket {ticketId} offers no enabled QR action on Flex Web.");
                Assert.That(qrPngBytes, Is.GreaterThan(0), $"10082-NDT-5-P06 not met: the QR action did not show ticket {ticketId}'s QR code.");
            });
        }
    }
}
