using MobileAutomation.Tests.Drivers;
using MobileAutomation.Tests.Pages;
using MobileAutomation.Tests.Utilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Flows;

// A filled-in ticket right after Submit: what the later steps check the submitted card against
public record CollectionTicketDraft(IReadOnlyList<string> TicketsBefore, TruckOption Truck, string StreetName);

// Flex side of the 'Load - Collection' ticket (steps 1-20 of the QR E2E): log in, fill and submit the ticket,
// generate its QR, and find it under Submitted. Shared by the QR handoff tests and the QR baselines, so the BLE
// tests can later reuse the same ticket up to the point where QR and Bluetooth part ways.
public class CollectionLoadTicketFlow
{
    public const string FormName = "Load - Collection";

    private readonly IWebDriver _flex;
    private readonly WebDriverWait _wait;
    private readonly string _node;

    public FormsPage Forms { get; }
    public ClockInPage ClockIn { get; }
    public CollectionLoadTicketFormPage TicketForm { get; }

    // Generated QR codes are stored here and attached to the test results
    public string QrDirectory { get; }

    public CollectionLoadTicketFlow(IWebDriver flex, WebDriverWait wait, string node)
    {
        _flex = flex;
        _wait = wait;
        _node = node;
        Forms = new FormsPage(flex);
        ClockIn = new ClockInPage(flex);
        TicketForm = new CollectionLoadTicketFormPage(flex);
        QrDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "QrCodes");
        Directory.CreateDirectory(QrDirectory);
    }

    // -------------------------------------------------------------------------
    // STEP 1: Log in (credentials from user-secrets) and clear onboarding screens
    // -------------------------------------------------------------------------
    public void LoginAndEnterWebView()
    {
        new LoginPage(_flex).LoginIfNeeded(DriverFactory.LoginEmail, DriverFactory.LoginPassword);

        if (_flex is AndroidDriver androidDriver)
        {
            PermissionHelper.HandleSystemPermissions(androidDriver);
        }

        new PrivacyPolicyPage(_flex).ClearAllPendingAgreements();
        new HomePage(_flex).CompleteProjectAssignment(_wait);

        // Forms page objects use DOM locators; on mobile they run inside the app's WebView (no-op on web)
        if (_flex is AndroidDriver)
        {
            DriverFactory.SwitchToWebViewContext(_flex);
            Assert.That(((AndroidDriver)_flex).Context, Does.StartWith("WEBVIEW"),
                "Could not switch to the Flex WEBVIEW context. Is Appium running with --allow-insecure=uiautomator2:chromedriver_autodownload?");
        }
    }

    // STEPS 2-17: open a new ticket, fill every step and submit it
    public CollectionTicketDraft FillAndSubmitTicket(string notes)
    {
        // -------------------------------------------------------------------------
        // STEPS 2-3: Forms > Collection Load Ticket, snapshot already-submitted tickets
        // -------------------------------------------------------------------------
        Forms.OpenFormsMenu();
        Forms.OpenForm(FormName);
        var ticketsBefore = Forms.GetSubmittedTicketIds();

        // -------------------------------------------------------------------------
        // STEPS 4-7: New form, clocking in first when the app requires it
        // -------------------------------------------------------------------------
        Forms.ClickNew();
        if (ClockIn.ClockInIfPrompted(workingOn: "testing", trackMileage: false))
        {
            Forms.ClickNew();
        }
        Assert.That(TicketForm.IsOpen(), Is.True, $"The '{FormName}' form did not open after clicking New.");

        // -------------------------------------------------------------------------
        // STEPS 8-10: Truck step - pick the first truck and verify the auto-filled data
        // -------------------------------------------------------------------------
        TicketForm.WaitForStep("Truck");
        var truck = TicketForm.SelectFirstTruck();

        Assert.Multiple(() =>
        {
            Assert.That(TicketForm.GetFieldValue("Truck Number"), Is.EqualTo(truck.TruckNumber),
                "Truck Number was not auto-filled from the selected truck.");
            Assert.That(truck.TruckCapacity, Is.Not.Empty, "Truck Capacity was not auto-filled from the selected truck.");
            Assert.That(truck.VehicleTag, Is.Not.Empty, "Truck License Plate was not auto-filled from the selected truck.");
            Assert.That(TicketForm.GetFieldValue("Truck Capacity"), Is.EqualTo(truck.TruckCapacity),
                "Truck Capacity was not auto-filled from the selected truck.");
            Assert.That(TicketForm.GetFieldValue("Truck License Plate"), Is.EqualTo(truck.VehicleTag),
                "Truck License Plate was not auto-filled from the selected truck's Vehicle Tag.");
            Assert.That(TicketForm.GetFieldValue("Driver Name"), Is.EqualTo(truck.DriverName),
                "Driver Name was not auto-filled from the selected truck.");
        });

        TicketForm.SelectRadio("Is Driver Name Correct?", "Yes");
        TicketForm.SelectRadio("Is the above information correct?", "Yes");
        if (TicketForm.HasField("New Shared Control"))
        {
            TicketForm.EnterFieldValue("New Shared Control", "1");
        }
        TicketForm.ClickNext();

        // -------------------------------------------------------------------------
        // STEP 11: Trailer step
        // -------------------------------------------------------------------------
        TicketForm.WaitForStep("Trailer");
        TicketForm.SelectRadio("Does the truck have a trailer attached?", "No");
        TicketForm.ClickNext();

        // -------------------------------------------------------------------------
        // STEP 11b: Collection Start step - the debris pile photo is required
        // -------------------------------------------------------------------------
        TicketForm.WaitForStep("Collection Start");
        TicketForm.AddPhoto("Photo of First Debris Pile", Path.Combine(AppContext.BaseDirectory, "TestData", "DebrisPile.png"));
        TicketForm.ClickNext();

        // -------------------------------------------------------------------------
        // STEP 12: Collection Information step
        // -------------------------------------------------------------------------
        TicketForm.WaitForStep("Collection Information");
        Assert.That(TicketForm.GetFieldValue("Truck Number"), Is.EqualTo(truck.TruckNumber),
            "Truck Number on the Collection Information step does not match the selected truck.");
        TicketForm.SelectChoiceOption("Truck Debris Type", "Vegetative/Woody");
        TicketForm.ClickNext();

        // -------------------------------------------------------------------------
        // STEPS 13-15: Location Information step - GPS with map fallback
        // -------------------------------------------------------------------------
        TicketForm.WaitForStep("Location Information");
        TicketForm.MockGpsLocation();
        var (latitude, longitude) = TicketForm.CaptureLocation();
        string streetName = TicketForm.GetFieldValue("Street Name");

        Assert.Multiple(() =>
        {
            Assert.That(latitude, Is.Not.Empty.And.Not.EqualTo("0"), "Latitude was not captured.");
            Assert.That(longitude, Is.Not.Empty.And.Not.EqualTo("0"), "Longitude was not captured.");
            Assert.That(TicketForm.GetFieldValue("Address Number"), Is.Not.Empty, "Address Number was not populated from the location.");
            Assert.That(TicketForm.GetFieldValue("Street Name"), Is.Not.Empty, "Street Name was not populated from the location.");
        });

        TicketForm.SelectRadio("Is the above address info correct?", "Yes");

        // -------------------------------------------------------------------------
        // STEPS 16-17: Review & Submit
        // -------------------------------------------------------------------------
        TicketForm.ClickNext();
        TicketForm.WaitForStep("Review & Submit");
        TicketForm.EnterNotes(notes);
        TicketForm.Submit();

        return new CollectionTicketDraft(ticketsBefore, truck, streetName);
    }

    // STEPS 18-19: confirm QR generation and store the generated QR code as a test attachment
    public byte[] GenerateAndStoreQr()
    {
        TicketForm.ProceedWithQrGeneration();

        byte[] qrPng = TicketForm.GetQrCodePng();
        Assert.That(qrPng, Is.Not.Empty, "The generated QR code image is empty.");

        string qrPath = Path.Combine(QrDirectory, $"CollectionLoadTicket_{_node}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        File.WriteAllBytes(qrPath, qrPng);
        TestContext.AddTestAttachment(qrPath, "Generated Collection Load Ticket QR code");
        Console.WriteLine($"🔳 QR code stored at: {qrPath}");

        TicketForm.CloseQrModal();
        return qrPng;
    }

    // -------------------------------------------------------------------------
    // STEP 20: The new ticket is listed under Submitted with matching data and Synced
    // -------------------------------------------------------------------------
    public string VerifyTicketSubmittedAndSynced(CollectionTicketDraft draft)
    {
        var newTickets = Forms.GetSubmittedTicketIds().Except(draft.TicketsBefore).ToList();
        Assert.That(newTickets, Has.Count.EqualTo(1),
            $"Expected exactly one new submitted ticket, found: [{string.Join(", ", newTickets)}].");

        string ticketId = newTickets[0];
        Console.WriteLine($"🎫 New ticket submitted: {ticketId}");
        bool isSynced = Forms.WaitUntilSynced(ticketId);
        var card = Forms.GetSubmittedCardValues(ticketId);

        Assert.Multiple(() =>
        {
            // The 'Load - Collection' card lists Truck Number / Trailer Number / Street Name
            Assert.That(card.GetValueOrDefault("Truck Number"), Is.EqualTo(draft.Truck.TruckNumber),
                $"Submitted ticket {ticketId} shows an unexpected Truck Number.");
            Assert.That(card.GetValueOrDefault("Trailer Number"), Is.EqualTo("---"),
                $"Submitted ticket {ticketId} should show no trailer, as 'No' was answered on the Trailer step.");
            Assert.That(card.GetValueOrDefault("Street Name"), Is.EqualTo(draft.StreetName),
                $"Submitted ticket {ticketId} shows an unexpected Street Name.");
            Assert.That(isSynced, Is.True, $"Submitted ticket {ticketId} did not reach the 'Synced' status.");
        });

        return ticketId;
    }
}
