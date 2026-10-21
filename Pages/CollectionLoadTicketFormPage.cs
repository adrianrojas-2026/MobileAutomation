using MobileAutomation.Tests.Drivers;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Chromium;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages;

// Attributes shown for a truck in the Truck choice list; the form auto-fills its fields from these
public record TruckOption(string TruckNumber, string DriverName, string TruckCapacity, string VehicleTag);

// Multi-step "Collection Load Ticket" form modal: Truck > Trailer > Collection Information > Location Information > Review & Submit.
// Fields are located by their visible label so the same locators work in Web mode and in the mobile WEBVIEW context.
public class CollectionLoadTicketFormPage : BasePage
{
    // Default GPS fix used when the browser/emulator location is mocked (Polk City Rd, FL)
    public const double MockLatitude = 28.1586965;
    public const double MockLongitude = -81.6926507;

    public CollectionLoadTicketFormPage(IWebDriver driver)
        : base(driver)
    {
    }

    private const string FormModal = "//ion-modal[.//app-form-container]";
    private const string FieldContainer = "//app-form-detail//div[contains(@class,'element-container')]";

    private By FormTitle => By.XPath($"{FormModal}//ion-title[normalize-space()='Collection Load Ticket']");
    private By CurrentStepName => By.XPath("//app-form-standard-footer//ion-label[contains(@class,'current-step-name')]");
    private By FooterButton(string text) => By.XPath(
        $"//app-form-standard-footer//ion-button[translate(normalize-space(),'abcdefghijklmnopqrstuvwxyz','ABCDEFGHIJKLMNOPQRSTUVWXYZ')='{text.ToUpperInvariant()}' and not(@disabled)]");

    // Innermost field container owning the label: a wrapping container (step group, ...) also contains the label,
    // and its first //input belongs to a different field
    private static string LabelledField(string label) =>
        $"{FieldContainer}[.//ion-label[contains(normalize-space(.),'{label}')]]" +
        $"[not(.//div[contains(@class,'element-container')][.//ion-label[contains(normalize-space(.),'{label}')]])]";

    private By Field(string label) => By.XPath(LabelledField(label));
    private By FieldInput(string label) => By.XPath($"{LabelledField(label)}//input");
    private By RadioOption(string questionLabel, string option) => By.XPath(
        $"{FieldContainer}[.//ion-label[contains(normalize-space(.),'{questionLabel}')]]" +
        $"//ion-radio-group/ion-item[.//ion-label[normalize-space()='{option}']]");
    private By CheckedRadioOption(string questionLabel, string option) => By.XPath(
        $"{FieldContainer}[.//ion-label[contains(normalize-space(.),'{questionLabel}')]]" +
        $"//ion-radio-group/ion-item[.//ion-label[normalize-space()='{option}']]//ion-radio[@aria-checked='true' or contains(@class,'radio-checked')]");
    private By ChoiceSelector(string label) => By.XPath(
        $"{FieldContainer}[.//app-choiceinput][.//ion-label[contains(normalize-space(.),'{label}')]]//ion-label[contains(@class,'select-value')]");

    // Choice picker modal (Truck, Truck Debris Type, ...)
    private By ChoiceItems => By.XPath("//app-choice-modal//div[contains(@class,'items')]/ion-item");
    private By ChoiceCheckbox(string option) => By.XPath(
        $"//app-choice-modal//div[contains(@class,'items')]/ion-item[.//ion-label[normalize-space()='{option}']]//ion-checkbox");
    private By ChoiceDoneButton => By.XPath("//app-choice-modal//ion-button[normalize-space()='Done']");

    // Location step
    private By LocateButton => By.XPath($"{FieldContainer}[.//app-locationinput]//ion-button[.//ion-icon[@name='locate']]");
    private By LatitudeInput => By.XPath("//app-form-detail//app-locationinput//input[@placeholder='Latitude']");
    private By LongitudeInput => By.XPath("//app-form-detail//app-locationinput//input[@placeholder='Longitude']");
    private By SelectFromMapLink => By.XPath("//app-form-detail//app-locationinput//u[normalize-space()='tap here']");
    private By MapLoader => By.CssSelector("div.map-loader");
    private By MapModalTitle => By.XPath("//app-location-selection-modal//ion-title[normalize-space()='Select Location']");
    private By MapConfirmButton => By.XPath("//app-location-selection-modal//ion-button[normalize-space()='Confirm']");

    // Review & Submit step, QR alert and QR modal
    private By NotesTextArea => By.XPath($"{FieldContainer}[.//ion-label[contains(normalize-space(.),'Notes')]]//textarea");
    private By GenerateQrAlert => By.XPath("//ion-alert[.//h2[normalize-space()='Generate QR Code']]");
    private By GenerateQrProceedButton => By.XPath(
        "//ion-alert[.//h2[normalize-space()='Generate QR Code']]//button[.//span[normalize-space()='Proceed']]");
    private By QrCodeImage => By.XPath("//app-qr-code-modal//ngx-qrcode//img");
    private By QrModalCloseButton => By.XPath("//app-qr-code-modal//span[contains(@class,'close_icon')]");

    public bool IsOpen(int timeoutSeconds = 15) => FindVisible(FormTitle, timeoutSeconds) != null;

    public string CurrentStep => Find(CurrentStepName).Text.Trim();

    public void WaitForStep(string stepName, int timeoutSeconds = 15)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        wait.Until(d => d.FindElements(CurrentStepName).FirstOrDefault()?.Text.Trim() == stepName);
    }

    // The floating chat bubble sits on top of the footer's right-hand button
    public void ClickNext() => ScrollAndClick(FooterButton("Next"), jsClickWhenIntercepted: true);

    // -------------------------------------------------------------------------
    // Generic field helpers
    // -------------------------------------------------------------------------
    public string GetFieldValue(string label)
    {
        Find(FieldInput(label));
        return ReadInputValue(FieldInput(label));
    }

    public void EnterFieldValue(string label, string value)
    {
        var input = Find(FieldInput(label));
        ScrollIntoView(input);
        input.Clear();
        input.SendKeys(value);
        Driver.HideKeyboardIfShown();
    }

    public void SelectRadio(string questionLabel, string option)
    {
        ScrollAndClick(RadioOption(questionLabel, option));
        Find(CheckedRadioOption(questionLabel, option));
    }

    public string GetChoiceValue(string label) => Find(ChoiceSelector(label)).Text.Trim();

    // -------------------------------------------------------------------------
    // Step 1 - Truck
    // -------------------------------------------------------------------------

    // Opens the Truck picker, ticks the first truck in the list and returns the attributes it advertises
    public TruckOption SelectFirstTruck()
    {
        ScrollAndClick(ChoiceSelector("Truck"));

        var firstItem = new WebDriverWait(Driver, TimeSpan.FromSeconds(15))
            .Until(d => d.FindElements(ChoiceItems).FirstOrDefault(e => e.Displayed));

        string Attribute(string key) => firstItem
            .FindElements(By.XPath($".//div[contains(concat(' ',normalize-space(@class),' '),' attribute ')][.//span[contains(@class,'key') and normalize-space()='{key}:']]//span[contains(@class,'value')]"))
            .FirstOrDefault()?.Text.Trim() ?? string.Empty;

        var truck = new TruckOption(
            TruckNumber: firstItem.FindElement(By.XPath("./div[contains(@class,'with-attributes')]/ion-label")).Text.Trim(),
            DriverName: Attribute("Driver Name"),
            TruckCapacity: Attribute("Truck Capacity"),
            VehicleTag: Attribute("Vehicle Tag"));
        Console.WriteLine($"🚚 Selecting truck '{truck.TruckNumber}' (Driver: {truck.DriverName}, Capacity: {truck.TruckCapacity}, Tag: {truck.VehicleTag})");

        ScrollIntoView(firstItem);
        EnsureChecked(firstItem.FindElement(By.TagName("ion-checkbox")));
        ScrollAndClick(ChoiceDoneButton);

        // Wait for the form to auto-fill from the selected truck
        var autoFillWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(10));
        autoFillWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        try
        {
            autoFillWait.Until(_ => GetFieldValue("Truck Capacity").Length > 0);
        }
        catch (WebDriverTimeoutException ex)
        {
            // Report what the locator actually matched, so a wrong input is obvious from the failure message
            var matched = string.Join(" | ", Driver.FindElements(FieldInput("Truck Capacity")).Select(i =>
                $"value='{i.GetDomProperty("value")}' displayed={i.Displayed} outerHTML={i.GetDomProperty("outerHTML")}"));
            throw new WebDriverTimeoutException($"Truck Capacity was not auto-filled after selecting the truck. Inputs matched: [{matched}]", ex);
        }
        return truck;
    }

    // -------------------------------------------------------------------------
    // Step 3 - Collection Information
    // -------------------------------------------------------------------------
    public void SelectChoiceOption(string fieldLabel, string option)
    {
        ScrollAndClick(ChoiceSelector(fieldLabel));
        EnsureChecked(Find(ChoiceCheckbox(option)));
        ScrollAndClick(ChoiceDoneButton);
        new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(_ => GetChoiceValue(fieldLabel).Contains(option));
    }

    // -------------------------------------------------------------------------
    // Step 4 - Location Information
    // -------------------------------------------------------------------------

    // Mocks the device location so the GPS button returns a deterministic fix
    public void MockGpsLocation(double latitude = MockLatitude, double longitude = MockLongitude)
    {
        try
        {
            if (Driver is AndroidDriver androidDriver)
            {
                androidDriver.Location = new Location { Latitude = latitude, Longitude = longitude, Altitude = 0 };
            }
            else if (Driver is ChromiumDriver chromiumDriver)
            {
                var origin = new Uri(Driver.Url).GetLeftPart(UriPartial.Authority);
                chromiumDriver.ExecuteCdpCommand("Browser.grantPermissions", new Dictionary<string, object?>
                {
                    ["origin"] = origin,
                    ["permissions"] = new[] { "geolocation" }
                });
                chromiumDriver.ExecuteCdpCommand("Emulation.setGeolocationOverride", new Dictionary<string, object?>
                {
                    ["latitude"] = latitude,
                    ["longitude"] = longitude,
                    ["accuracy"] = 10
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not mock GPS location ({ex.Message}). The map fallback will be used if needed.");
        }
    }

    // Clicks the GPS button; if no precise fix arrives, confirms a location from the map instead
    public (string Latitude, string Longitude) CaptureLocation(int gpsTimeoutSeconds = 20)
    {
        ScrollAndClick(LocateButton);

        // Prefer GPS: the "tap here" map link can show while the fix is still being acquired, so it only
        // decides the outcome once GPS has had its full timeout (the app fills 0,0 as a best estimate)
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(gpsTimeoutSeconds));
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
        bool gotGps;
        try
        {
            gotGps = wait.Until(_ => HasRealCoordinates());
        }
        catch (WebDriverTimeoutException)
        {
            gotGps = false;
        }

        if (!gotGps)
        {
            Console.WriteLine("📍 GPS did not return a location. Selecting location from the map...");
            ScrollAndClick(SelectFromMapLink, 30);

            // Wait for the "Select Location" modal, then for the map tiles to finish loading before confirming
            if (FindVisible(MapModalTitle, 30) == null)
            {
                throw new WebDriverTimeoutException("The 'Select Location' map did not open after tapping 'tap here'.");
            }
            var mapWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(45));
            mapWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            mapWait.Until(d => !d.FindElements(MapLoader).Any(e => e.Displayed)
                               && d.FindElements(MapConfirmButton).Any(e => e.Displayed));
            Console.WriteLine("🗺️ Map loaded. Confirming location...");
            ScrollAndClick(MapConfirmButton, 30);
        }

        new WebDriverWait(Driver, TimeSpan.FromSeconds(30)).Until(_ => HasRealCoordinates());
        // Address Number / Street Name are reverse-geocoded from the coordinates
        new WebDriverWait(Driver, TimeSpan.FromSeconds(30)).Until(_ => GetFieldValue("Street Name").Length > 0);

        var location = (ReadInputValue(LatitudeInput), ReadInputValue(LongitudeInput));
        Console.WriteLine($"📍 Location captured: {location.Item1}, {location.Item2} ({GetFieldValue("Address Number")} {GetFieldValue("Street Name")})");
        return location;
    }

    private bool HasRealCoordinates()
    {
        var lat = ReadInputValue(LatitudeInput);
        var lng = ReadInputValue(LongitudeInput);
        return lat.Length > 0 && lng.Length > 0 && !(lat == "0" && lng == "0");
    }

    // The choice picker reopens with the field's current value already ticked, and clicking a ticked checkbox
    // clears it, so only click when it isn't checked yet
    private void EnsureChecked(IWebElement checkbox)
    {
        ScrollIntoView(checkbox);
        if (checkbox.GetDomProperty("checked") is "true" or "True")
        {
            Console.WriteLine("☑️ Option already selected in the picker. Leaving it checked.");
            return;
        }
        checkbox.Click();
        new WebDriverWait(Driver, TimeSpan.FromSeconds(5)).Until(_ => checkbox.GetDomProperty("checked") is "true" or "True");
    }

    // Live value of the visible match (Ionic keeps hidden copies of visited pages); empty when nothing matches
    private string ReadInputValue(By locator)
    {
        var inputs = Driver.FindElements(locator);
        var input = inputs.FirstOrDefault(i => i.Displayed) ?? inputs.FirstOrDefault();
        return input?.GetDomProperty("value")?.Trim() ?? string.Empty;
    }

    // -------------------------------------------------------------------------
    // Step 5 - Review & Submit
    // -------------------------------------------------------------------------
    public void EnterNotes(string notes) => EnterText(NotesTextArea, notes);

    public void Submit() => ScrollAndClick(FooterButton("Submit"), jsClickWhenIntercepted: true);

    public void ProceedWithQrGeneration()
    {
        Find(GenerateQrAlert);
        ScrollAndClick(GenerateQrProceedButton);
    }

    // The QR is rendered as a base64 PNG data URL, so the exact image can be stored without a screenshot
    public byte[] GetQrCodePng()
    {
        var img = new WebDriverWait(Driver, TimeSpan.FromSeconds(30)).Until(d =>
        {
            var element = d.FindElements(QrCodeImage).FirstOrDefault(e => e.Displayed);
            return element != null && (element.GetAttribute("src") ?? "").StartsWith("data:image/png;base64,") ? element : null;
        });
        var src = img.GetAttribute("src")!;
        return Convert.FromBase64String(src[(src.IndexOf(',') + 1)..]);
    }

    public void CloseQrModal()
    {
        ScrollAndClick(QrModalCloseButton);
        new WebDriverWait(Driver, TimeSpan.FromSeconds(15)).Until(d => !d.FindElements(QrCodeImage).Any(e => e.Displayed));
    }
}
