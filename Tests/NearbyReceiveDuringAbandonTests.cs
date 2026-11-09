using MobileAutomation.Tests.Reporting;
using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Tests;
using MobileAutomation.Tests.Utilities;

namespace RecoveryTrac.AutomatedTests
{
    // Defect scenario (no requirement ID covers it yet): the Driver App receives a load over Nearby while the driver is
    // abandoning the previous one. The abandon prompt ("Are you sure you want to abandon the active QR code?", Confirm
    // counting down from 5) is not tied to the load it was opened for: a Nearby load replaces the active one behind the
    // prompt, and Confirm then removes whatever load is active (HomePage.abandonActiveLoad > removeActiveLoadTicket).
    // The new load is deleted, although the Driver App showed 'Load <n> received.' and Flex reported it as sent.
    // Expected (pending PO confirmation): the load that arrived after the prompt opened stays the active load, with
    // its QR. Kept out of the BLE-R1-C2 run, as it fails until the Driver App is fixed.
    [TestFixture]
    [Category("BLE-R1-C2-Defects")]
    [NonParallelizable]
    public class NearbyReceiveDuringAbandonTests : NearbyTestBase
    {
        [Test]
        [Requirements()]
        public void NearbyLoad_ReceivedWhileAbandonPromptOpen_IsKeptAfterConfirm()
        {
            // Flex: two new tickets, A for the load being abandoned and B for the one sent during the prompt
            var (flow, ticketA, _) = StartFlexOnSubmittedTicket("Nearby load A, abandoned on the Driver App", freshTicket: true);
            var draftB = flow.FillAndSubmitTicket($"Automated test - Nearby load B, sent during the abandon prompt {Node} {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            flow.TicketForm.DeclineQrGeneration();
            string ticketB = flow.VerifyTicketSubmittedAndSynced(draftB);
            string loadA = ticketA.TrimStart('#');
            string loadB = ticketB.TrimStart('#');
            Assume.That(loadB, Is.Not.EqualTo(loadA), "Flex returned the same ticket twice, so the two loads cannot be told apart.");

            // -------------------------------------------------------------------------
            // STEP 1: the Driver App on Home receives load A
            // -------------------------------------------------------------------------
            PrepareGridDriverApps();
            var menuA = OpenNearby(flow, ticketA);
            var own = FindOwnDriverApp(menuA);
            var cardA = WaitForSelectableCard(menuA, own);
            var sentA = SendToCard(menuA, cardA);
            AssertSentTo(sentA, cardA, ticketA);
            CloseMenuAfterSend(menuA, sentA);
            string currentBefore = own.Home.WaitForActiveLoad(timeoutSeconds: 30);
            Assume.That(currentBefore, Is.EqualTo(loadA), $"Setup: the Driver App did not hold load {loadA} after the first send.");
            Console.WriteLine($"🚚 Driver app holds load {loadA}.");

            // -------------------------------------------------------------------------
            // STEP 2: Flex lists the Driver App for ticket B; the driver taps Cancel Load, and while Confirm is still
            // counting down Flex sends load B
            // -------------------------------------------------------------------------
            var menuB = OpenNearby(flow, ticketB);
            var cardB = WaitForSelectableCard(menuB, own, timeoutSeconds: 30);

            own.Home.StartRecordingToasts();
            Assume.That(own.Home.OpenAbandonLoadPrompt(), Is.True, "Setup: the Driver App showed no abandon prompt after Cancel Load.");
            string? captionAtOpen = own.Home.AbandonConfirmCaption;
            Console.WriteLine($"🚚 Driver app: abandon prompt open for load {loadA} ('{captionAtOpen}').");

            var sentB = SendToCard(menuB, cardB);
            bool promptOpenAtArrival = own.Home.IsAbandonPromptOpen;
            bool countdownAtArrival = own.Home.IsAbandonCountdownRunning;
            string? captionAtArrival = own.Home.AbandonConfirmCaption;
            AssertSentTo(sentB, cardB, ticketB);
            CloseMenuAfterSend(menuB, sentB);

            string? toast = own.Home.ToastMessage(timeoutSeconds: 10);
            string loadBehindPrompt = own.Home.CurrentLoad;
            Console.WriteLine($"🚚 Driver app after the send: prompt open {promptOpenAtArrival} ('{captionAtArrival}', countdown running " +
                              $"{countdownAtArrival}), toast '{toast}', load behind the prompt '{loadBehindPrompt}'.");
            Assume.That(promptOpenAtArrival, Is.True,
                $"Setup: the abandon prompt was no longer open when load {loadB} arrived, so this case was not tested.");
            if (!countdownAtArrival)
            {
                Assert.Warn($"Load {loadB} arrived after the Confirm countdown ended ('{captionAtArrival}'); the prompt was still open, so the case still applies.");
            }

            // -------------------------------------------------------------------------
            // STEP 3: the driver taps Confirm, meaning to abandon load A
            // -------------------------------------------------------------------------
            own.Home.ConfirmAbandonLoad();
            own.Home.IsScanQrCodeShown(timeoutSeconds: 5); // let Home settle after the prompt closes
            bool hasLoad = own.Home.HasActiveLoad;
            string finalLoad = own.Home.CurrentLoad;
            string? driverPayload = hasLoad ? QrScanFeed.Decode(own.Home.GetCurrentLoadQrPng(), DriverFactory.DriverFlex) : null;
            Console.WriteLine($"🚚 Driver app after Confirm: current load '{finalLoad}'.");

            // Reference: ticket B's QR payload in Flex
            flow.Forms.OpenSubmittedCardQr(ticketB);
            string flexPayloadB = QrScanFeed.Decode(flow.TicketForm.GetQrCodePng(), DriverFactory.DriverFlex);
            flow.TicketForm.CloseQrModal();

            BaselineRecorder.Save("BLE-AbandonRace", Node, new
            {
                ticketA,
                ticketB,
                driverApp = cardB.Key,
                captionAtOpen,
                captionAtArrival,
                countdownAtArrival,
                toast,
                loadBehindPrompt,
                finalLoad,
                payloadMatchesQr = driverPayload == flexPayloadB
            });

            Assert.Multiple(() =>
            {
                Assert.That(toast, Is.EqualTo($"Load {loadB} received."),
                    $"The Driver App did not tell the driver that load {loadB} arrived while the abandon prompt was open.");
                Assert.That(finalLoad, Is.EqualTo(loadB),
                    $"Tapping Confirm on the abandon prompt opened for load {loadA} also removed load {loadB}, which arrived over Nearby " +
                    $"while the prompt was open (the Driver App had shown 'Load {loadB} received.' and Flex reported it as sent). " +
                    "Home shows no load and no QR.");
                if (hasLoad && finalLoad == loadB)
                {
                    Assert.That(driverPayload, Is.EqualTo(flexPayloadB),
                        $"Load {loadB} kept after Confirm carries a different payload than ticket {ticketB}'s QR code.");
                }
            });
        }
    }
}
