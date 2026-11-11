using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium;

namespace MobileAutomation.Tests.Flows;

// A submitted 'Load - Disposal' ticket: its ID, QR code (decoded by the caller when needed) and submitted card
public record SubmittedDisposal(string TicketId, byte[] QrPng, bool QrPromptedOnSubmit, IReadOnlyDictionary<string, string> Card);

// Flex side of the 'Load - Disposal' ticket (steps 23-26 of the QR E2E): import the driver's load QR, make the load
// call, submit and get the disposal QR the Driver app scans for its receipt. Shared by the QR E2E and the disposal
// QR baselines, so the BLE disposal tests (Receive Data / Send Receipt) can reuse it up to where QR and Bluetooth
// part ways.
public class LoadDisposalFlow
{
    public const string FormName = "Load - Disposal";
    public const string LoadCall = "50";
    public const string ImportStep = "Import Collection Data";

    private readonly IWebDriver _flex;
    private readonly string _node;

    public FormsPage Forms { get; }
    public ClockInPage ClockIn { get; }
    public LoadDisposalFormPage Form { get; }

    // Generated QR codes are stored here and attached to the test results (same folder as the collection QR codes)
    public string QrDirectory { get; }

    public LoadDisposalFlow(IWebDriver flex, string node)
    {
        _flex = flex;
        _node = node;
        Forms = new FormsPage(flex);
        ClockIn = new ClockInPage(flex);
        Form = new LoadDisposalFormPage(flex);
        QrDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "QrCodes");
        Directory.CreateDirectory(QrDirectory);
    }

    // The driver's load QR is what the disposal site scans: store it as a test attachment and stage it on the Flex
    // camera before the scan
    public void StoreAndStageDriversQr(byte[] driverQrPng)
    {
        string driverQrPath = Path.Combine(QrDirectory, $"DriverCurrentLoad_{_node}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        File.WriteAllBytes(driverQrPath, driverQrPng);
        TestContext.AddTestAttachment(driverQrPath, "Driver app current load QR code");
        StageDriversQr(driverQrPng);
    }

    public void StageDriversQr(byte[] driverQrPng) =>
        QrScanFeed.Stage(_flex, driverQrPng, rasterizer: _flex, DriverFactory.FlexCameraFeedPath);

    // Device only: hands Flex's scanner any text, e.g. a damaged load payload no real QR would carry
    public void StageDriversQrText(string text) => QrScanFeed.StageText(_flex, text);

    // -------------------------------------------------------------------------
    // STEP 23: All Forms > Load - Disposal > New. Returns the disposal tickets already submitted
    // -------------------------------------------------------------------------
    public IReadOnlyList<string> OpenNewForm()
    {
        Forms.OpenAllForms();
        Forms.OpenForm(FormName);
        var ticketsBefore = Forms.GetSubmittedTicketIds();
        Forms.ClickNew();
        if (ClockIn.ClockInIfPrompted(workingOn: "testing", trackMileage: false))
        {
            Forms.ClickNew();
        }
        Assert.That(Form.IsOpen(), Is.True, $"The '{FormName}' form did not open after clicking New.");
        return ticketsBefore;
    }

    // -------------------------------------------------------------------------
    // STEP 24: Import Collection Data - scan the staged driver's QR code and pick a disposal site
    // -------------------------------------------------------------------------
    public string ScanDriversQrAndSelectSite()
    {
        Form.WaitForStep(ImportStep);
        Form.ScanDriversQrCode();
        return Form.SelectFirstDisposalSite();
    }

    // The step also asks why the QR could not be scanned, so NEXT only enables once the scan imported the load
    public void AssertImportedAndContinue(string disposalSite, string truckNumber)
    {
        Assert.That(Form.IsNextEnabled(timeoutSeconds: 20), Is.True,
            $"NEXT stayed disabled after scanning the driver's QR code and selecting '{disposalSite}'. The collection data was probably not imported.");
        Assert.That(Form.GetFieldValue("Truck Number"), Is.EqualTo(truckNumber),
            "The Truck Number imported from the driver's QR code does not match the collection ticket's truck.");
        Form.ClickNext();
    }

    // -------------------------------------------------------------------------
    // STEP 25: Truck Container Load Call - confirm imported data, 50% load call, photo, Submit
    // -------------------------------------------------------------------------
    public void CompleteLoadCallAndSubmit()
    {
        Form.WaitForStep("Truck Container Load Call");
        Form.SelectRadio("Is the above capacity correct?", "Yes");
        Form.SelectRadio("Is the above debris class correct?", "Yes");
        Form.SelectPickerValue("Load Call", LoadCall);
        Form.AddPhoto("Load Call Photo", Path.Combine(AppContext.BaseDirectory, "TestData", "DebrisPile.png"));
        Form.Submit();
    }

    // -------------------------------------------------------------------------
    // STEP 26: Disposal ticket is listed (and Synced), linked to the collection ticket; its QR code is stored.
    // waitForSync: false when the devices are offline; the caller then checks the sync once back online
    // -------------------------------------------------------------------------
    public SubmittedDisposal VerifySubmittedAndGetQr(IReadOnlyList<string> ticketsBefore, string collectionTicketId,
        string truckNumber, bool waitForSync = true)
    {
        // Some form versions prompt for the QR right after submit (like Load - Collection); otherwise it is
        // opened from the submitted card once the ticket is listed
        bool qrPromptedOnSubmit = Form.IsGenerateQrPromptShown();
        byte[]? qrPng = null;
        if (qrPromptedOnSubmit)
        {
            Form.ProceedWithQrGeneration();
            qrPng = Form.GetQrCodePng();
            Form.CloseQrModal();
        }

        var newTickets = Forms.GetSubmittedTicketIds().Except(ticketsBefore).ToList();
        Assert.That(newTickets, Has.Count.EqualTo(1),
            $"Expected exactly one new submitted disposal ticket, found: [{string.Join(", ", newTickets)}].");

        string ticketId = newTickets[0];
        Console.WriteLine($"🎫 New disposal ticket submitted: {ticketId}");
        if (waitForSync)
        {
            Assert.That(Forms.WaitUntilSynced(ticketId), Is.True,
                $"Submitted disposal ticket {ticketId} did not reach the 'Synced' status.");
        }

        var card = Forms.GetSubmittedCardValues(ticketId);
        Assert.Multiple(() =>
        {
            Assert.That(card.GetValueOrDefault("Collection Document Number"), Is.EqualTo(collectionTicketId.TrimStart('#')),
                $"Disposal ticket {ticketId} is not linked to collection ticket {collectionTicketId}.");
            Assert.That(card.GetValueOrDefault("Truck Number"), Is.EqualTo(truckNumber),
                $"Disposal ticket {ticketId} shows an unexpected Truck Number.");
        });

        if (!qrPromptedOnSubmit)
        {
            qrPng = OpenCardQr(ticketId);
        }
        Assert.That(qrPng, Is.Not.Null.And.Not.Empty, "The disposal ticket QR code image is empty.");

        string qrPath = Path.Combine(QrDirectory, $"LoadDisposal_{_node}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        File.WriteAllBytes(qrPath, qrPng!);
        TestContext.AddTestAttachment(qrPath, "Generated Load - Disposal QR code");
        Console.WriteLine($"🔳 Disposal QR code stored at: {qrPath}");

        return new SubmittedDisposal(ticketId, qrPng!, qrPromptedOnSubmit, card);
    }

    // The QR modal of a submitted disposal ticket, opened from its card (next to Print). A ticket that has not synced
    // yet (e.g. submitted offline) is still modifiable, so the app first asks to finalize it (Generate QR Code)
    public byte[] OpenCardQr(string ticketId)
    {
        Forms.OpenSubmittedCardQr(ticketId);
        if (Form.IsGenerateQrPromptShown(timeoutSeconds: 5))
        {
            Console.WriteLine($"ℹ️ Ticket {ticketId} is not finalized yet; confirming Generate QR Code from its card.");
            Form.ProceedWithQrGeneration();
        }
        byte[] qrPng = Form.GetQrCodePng();
        Form.CloseQrModal();
        return qrPng;
    }
}
