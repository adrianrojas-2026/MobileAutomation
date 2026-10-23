using MobileAutomation.Tests.Utilities;
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

    protected const string FormModal = "//ion-modal[.//app-form-container]";
    private const string FieldContainer = "//app-form-detail//div[contains(@class,'element-container')]";

    // The modal was renamed from "Collection Load Ticket" to "Load - Collection"; accept both while environments differ
    // Form pages that share this modal layout (e.g. Load - Disposal) override the title
    protected virtual IReadOnlyList<string> FormTitles => ["Load - Collection", "Collection Load Ticket"];
    private By FormTitle => By.XPath(
        $"{FormModal}//ion-header//*[{string.Join(" or ", FormTitles.Select(t => $"normalize-space()='{t}'"))}]");
    private By CurrentStepName => By.XPath("//app-form-standard-footer//ion-label[contains(@class,'current-step-name')]");
    protected By FooterButton(string text) => By.XPath(
        $"//app-form-standard-footer//ion-button[translate(normalize-space(),'abcdefghijklmnopqrstuvwxyz','ABCDEFGHIJKLMNOPQRSTUVWXYZ')='{text.ToUpperInvariant()}' and not(@disabled)]");

    // Innermost field container owning the label: a wrapping container (step group, ...) also contains the label,
    // and its first //input belongs to a different field
    protected static string LabelledField(string label) =>
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
    protected By ChoiceSelector(string label) => By.XPath(
        $"{FieldContainer}[.//app-choiceinput][.//ion-label[contains(normalize-space(.),'{label}')]]//ion-label[contains(@class,'select-value')]");

    // Choice picker modal (Truck, Truck Debris Type, ...)
    protected By ChoiceItems => By.XPath("//app-choice-modal//div[contains(@class,'items')]/ion-item");
    // Case-insensitive: the options are now listed in upper case (e.g. "VEGETATIVE/WOODY")
    private By ChoiceCheckbox(string option) => By.XPath(
        $"//app-choice-modal//div[contains(@class,'items')]/ion-item[.//ion-label[translate(normalize-space(),'abcdefghijklmnopqrstuvwxyz','ABCDEFGHIJKLMNOPQRSTUVWXYZ')='{option.ToUpperInvariant()}']]//ion-checkbox");
    protected By ChoiceDoneButton => By.XPath("//app-choice-modal//ion-button[normalize-space()='Done']");

    // Location step
    private By LocateButton => By.XPath($"{FieldContainer}[.//app-locationinput]//ion-button[.//ion-icon[@name='locate']]");
    private By LatitudeInput => By.XPath("//app-form-detail//app-locationinput//input[@placeholder='Latitude']");
    private By LongitudeInput => By.XPath("//app-form-detail//app-locationinput//input[@placeholder='Longitude']");
    private By SelectFromMapLink => By.XPath("//app-form-detail//app-locationinput//u[normalize-space()='tap here']");
    private By MapLoader => By.CssSelector("div.map-loader");
    private By MapModalTitle => By.XPath("//app-location-selection-modal//ion-title[normalize-space()='Select Location']");
    private By MapConfirmButton => By.XPath("//app-location-selection-modal//ion-button[normalize-space()='Confirm']");

    // Review & Submit step, QR alert and QR modal
    // "Notes & Comments" is now a single-line input; older form versions used a textarea
    private By NotesTextArea => By.XPath(
        $"{FieldContainer}[.//ion-label[contains(normalize-space(.),'Notes')]]//*[self::textarea or self::input]");
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

    // NEXT is rendered disabled until every required field on the step is answered
    public bool IsNextEnabled(int timeoutSeconds = 15) => FindVisible(FooterButton("Next"), timeoutSeconds) != null;

    // -------------------------------------------------------------------------
    // Generic field helpers
    // -------------------------------------------------------------------------
    // For fields that only some versions of the form definition include
    public bool HasField(string label) => Driver.FindElements(FieldInput(label)).Count > 0;

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

        // The "Search for Truck" picker no longer lists every attribute (Truck Capacity / Vehicle Tag are missing),
        // so take those from the auto-filled fields; the caller verifies them against the picked truck
        truck = truck with
        {
            DriverName = string.IsNullOrEmpty(truck.DriverName) ? GetFieldValue("Driver Name") : truck.DriverName,
            TruckCapacity = string.IsNullOrEmpty(truck.TruckCapacity) ? GetFieldValue("Truck Capacity") : truck.TruckCapacity,
            VehicleTag = string.IsNullOrEmpty(truck.VehicleTag) ? GetFieldValue("Truck License Plate") : truck.VehicleTag
        };
        return truck;
    }

    // -------------------------------------------------------------------------
    // Step 3 - Collection Start
    // -------------------------------------------------------------------------

    // Every <input type=file> on the page, including ones inside shadow roots (e.g. pwa-camera-modal)
    private const string FindFileInputsScript =
        "const found = [];" +
        "const walk = root => root.querySelectorAll('*').forEach(el => {" +
        "  if (el.matches('input[type=file]')) found.push(el);" +
        "  if (el.shadowRoot) walk(el.shadowRoot);" +
        "});" +
        "walk(document);" +
        "return found;";

    // Collection photos use an "ADD" tile; other photo fields put an Add/Take Photo button beside the label
    private By PhotoAddButton(string label) => By.XPath(
        $"{LabelledField(label)}//ion-button[" +
        string.Join(" or ", new[] { "ADD", "PHOTO", "CAMERA", "UPLOAD" }.Select(t =>
            $"contains(translate(normalize-space(.),'abcdefghijklmnopqrstuvwxyz','ABCDEFGHIJKLMNOPQRSTUVWXYZ'),'{t}')")) +
        " or .//ion-icon[contains(@name,'camera') or contains(@name,'add') or contains(@name,'image')]]");

    // Attaches an image to a photo field (e.g. "Photo of First Debris Pile") and waits for the step to accept it
    public void AddPhoto(string fieldLabel, string imagePath)
    {
        if (Driver is AndroidDriver androidDriver)
        {
            AddPhotoFromGallery(androidDriver, fieldLabel, imagePath);
            WaitForPhotoAccepted();
            return;
        }

        string fullPath = Path.GetFullPath(imagePath);
        var chromium = Driver as ChromiumDriver;

        // ADD opens the OS file chooser; intercept it so the dialog never shows and the file is set on the input instead
        chromium?.ExecuteCdpCommand("Page.setInterceptFileChooserDialog", new Dictionary<string, object?> { ["enabled"] = true });
        try
        {
            ScrollAndClick(PhotoAddButton(fieldLabel), jsClickWhenIntercepted: true);

            var fileInput = new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(d =>
                (((IJavaScriptExecutor)d).ExecuteScript(FindFileInputsScript) as IReadOnlyCollection<object>)?
                    .OfType<IWebElement>().LastOrDefault());

            // Capacitor's camera fallback creates the input hidden; Selenium only types into rendered inputs
            ((IJavaScriptExecutor)Driver).ExecuteScript(
                "arguments[0].hidden = false; arguments[0].style.display = 'block'; arguments[0].style.visibility = 'visible';", fileInput);
            fileInput!.SendKeys(fullPath);
            Console.WriteLine($"📷 Attached '{Path.GetFileName(fullPath)}' to '{fieldLabel}'.");
        }
        finally
        {
            chromium?.ExecuteCdpCommand("Page.setInterceptFileChooserDialog", new Dictionary<string, object?> { ["enabled"] = false });
        }

        WaitForPhotoAccepted();
    }

    // NEXT (SUBMIT on the last step) stays disabled until the required photo is stored
    private void WaitForPhotoAccepted() =>
        new WebDriverWait(Driver, TimeSpan.FromSeconds(30)).Until(d =>
            d.FindElements(FooterButton("Next")).Concat(d.FindElements(FooterButton("Submit"))).Any(e => e.Displayed));

    // "Add Photo" action sheet (Take Photo / Select from Gallery) the app shows on a device
    private By SelectFromGalleryButton => By.XPath(
        "//ion-action-sheet//button[contains(translate(normalize-space(.),'abcdefghijklmnopqrstuvwxyz','ABCDEFGHIJKLMNOPQRSTUVWXYZ'),'GALLERY')]");

    // First photo in the Android photo picker (Android 13+), or in the documents/gallery picker older versions open
    private static readonly By PickerFirstPhoto = By.XPath(
        "(//*[contains(@resource-id,'icon_thumbnail') or contains(@resource-id,'id/thumbnail')]" +
        " | //android.widget.ImageView[contains(@content-desc,'Photo taken') or contains(@content-desc,'Image')])[1]");

    // Emulator camera app (ACTION_IMAGE_CAPTURE): shutter, then the review screen's confirm button
    private static readonly By CameraShutterButton = By.XPath(
        "//*[contains(@resource-id,'shutter_button')] | //*[@content-desc='Shutter' or @content-desc='Take photo' or @content-desc='Take picture']");
    private static readonly By CameraConfirmButton = By.XPath(
        "//*[contains(@resource-id,'done_button') or contains(@resource-id,'btn_done') or contains(@resource-id,'confirm_button')]" +
        " | //*[@content-desc='Done' or @content-desc='OK' or @content-desc='Confirm']");
    // First-run camera prompts (e.g. "Remember photo locations?")
    private static readonly By CameraOnboardingButton = By.XPath(
        "//android.widget.Button[@text='NEXT' or @text='Next' or @text='No thanks' or @text='NO THANKS' or @text='OK' or @text='Got it' or @text='GOT IT']");

    // Device flow: ADD opens either the "Add Photo" sheet (gallery allowed) or the camera directly, when the field
    // blocks gallery uploads (options.media.preventUploadFromGallery)
    private void AddPhotoFromGallery(AndroidDriver androidDriver, string fieldLabel, string imagePath)
    {
        ScrollAndClick(PhotoAddButton(fieldLabel), jsClickWhenIntercepted: true);
        bool galleryAllowed = FindVisible(SelectFromGalleryButton, 5) != null;

        string webContext = androidDriver.Context;
        if (galleryAllowed)
        {
            string udid = AdbHelper.GetUdid(androidDriver)
                ?? throw new InvalidOperationException("Could not tell which device the Appium session is on.");
            string devicePath = AdbHelper.PushToGallery(udid, imagePath);
            ScrollAndClick(SelectFromGalleryButton, jsClickWhenIntercepted: true);
            Console.WriteLine($"📷 '{fieldLabel}' allows gallery uploads. Picking '{devicePath}'...");
        }
        else
        {
            Console.WriteLine($"📷 '{fieldLabel}' requires a live photo. Taking one with the emulator camera...");
        }

        // Picker and camera are native activities on top of the app's WebView
        androidDriver.Context = "NATIVE_APP";
        try
        {
            if (galleryAllowed)
            {
                var photo = FindVisible(PickerFirstPhoto, 30)
                    ?? throw new NoSuchElementException("The Android photo picker did not show any photo to select.");
                photo.Click();
            }
            else
            {
                TakeCameraPhoto(androidDriver);
            }
        }
        finally
        {
            // The app's WebView comes back once the picker/camera returns its result
            new WebDriverWait(androidDriver, TimeSpan.FromSeconds(30)).Until(_ => androidDriver.Contexts.Contains(webContext));
            androidDriver.Context = webContext;
        }
    }

    private static void TakeCameraPhoto(AndroidDriver androidDriver)
    {
        // Clear the camera permission dialog and first-run prompts until the shutter is usable
        var shutterWait = new WebDriverWait(androidDriver, TimeSpan.FromSeconds(45));
        shutterWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        var shutter = shutterWait.Until(d =>
        {
            PermissionHelper.HandleSystemPermissions(androidDriver);
            d.FindElements(CameraOnboardingButton).FirstOrDefault(b => b.Displayed)?.Click();
            return d.FindElements(CameraShutterButton).FirstOrDefault(b => b.Displayed && b.Enabled);
        });
        Thread.Sleep(1000); // Let the preview start; an early tap can be ignored
        shutter.Click();

        var confirmWait = new WebDriverWait(androidDriver, TimeSpan.FromSeconds(30));
        confirmWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        confirmWait.Until(d => d.FindElements(CameraConfirmButton).FirstOrDefault(b => b.Displayed))!.Click();
        Console.WriteLine("📷 Photo taken with the emulator camera.");
    }

    // -------------------------------------------------------------------------
    // Step 4 - Collection Information
    // -------------------------------------------------------------------------
    // Wheel picker overlay (e.g. Load Call (%): 0, 5, 10 ... with CANCEL / CONFIRM). Its columns only move by
    // scroll/drag, so the option is selected through the picker's column model and then confirmed
    private const string SelectPickerOptionScript =
        "const picker = [...document.querySelectorAll('ion-picker, ion-picker-legacy')].pop();" +
        "if (!picker || !picker.columns || !picker.columns.length) return 'no-picker';" +
        "const col = picker.columns[0];" +
        "const i = col.options.findIndex(o => String(o.text).trim() === arguments[0] || String(o.value) === arguments[0]);" +
        "if (i < 0) return 'options: ' + col.options.map(o => o.text).join(', ');" +
        "col.selectedIndex = i;" +
        "picker.columns = [...picker.columns];" +
        "return 'ok';";

    private By PickerConfirmButton => By.XPath(
        "(//ion-picker | //ion-picker-legacy)//button[translate(normalize-space(),'confirm','CONFIRM')='CONFIRM']");

    public void SelectPickerValue(string fieldLabel, string value)
    {
        var field = Driver.FindElements(FieldInput(fieldLabel)).FirstOrDefault(e => e.Displayed) ?? Find(Field(fieldLabel));
        ScrollIntoView(field);
        field.Click();

        var result = new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(d =>
        {
            var outcome = ((IJavaScriptExecutor)d).ExecuteScript(SelectPickerOptionScript, value) as string;
            return outcome == "no-picker" ? null : outcome;
        });
        if (result != "ok")
        {
            throw new InvalidOperationException($"'{value}' is not an option of the '{fieldLabel}' picker ({result}).");
        }

        Thread.Sleep(300); // Column re-render settle before confirming
        ScrollAndClick(PickerConfirmButton, jsClickWhenIntercepted: true);
        new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(_ => GetFieldValue(fieldLabel).Contains(value));
        Console.WriteLine($"🎚️ '{fieldLabel}' set to {GetFieldValue(fieldLabel)}.");
    }

    // Ticks the first option in a choice picker (e.g. Disposal Site) and returns its name
    public string SelectFirstChoiceOption(string fieldLabel)
    {
        ScrollAndClick(ChoiceSelector(fieldLabel));

        var firstItem = new WebDriverWait(Driver, TimeSpan.FromSeconds(15))
            .Until(d => d.FindElements(ChoiceItems).FirstOrDefault(e => e.Displayed));
        string name = firstItem.FindElement(By.XPath("./div[contains(@class,'with-attributes')]/ion-label")).Text.Trim();
        Console.WriteLine($"☑️ Selecting '{name}' for '{fieldLabel}'.");

        EnsureChecked(firstItem.FindElement(By.TagName("ion-checkbox")));
        ScrollAndClick(ChoiceDoneButton);
        new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(_ => GetChoiceValue(fieldLabel).Contains(name, StringComparison.OrdinalIgnoreCase));
        return name;
    }

    public void SelectChoiceOption(string fieldLabel, string option)
    {
        ScrollAndClick(ChoiceSelector(fieldLabel));
        // Ionic can keep a hidden copy of the picker in the DOM, so take the visible checkbox
        var checkbox = FindVisible(ChoiceCheckbox(option), 15)
            ?? throw new NoSuchElementException($"Option '{option}' was not displayed in the '{fieldLabel}' picker.");
        EnsureChecked(checkbox);
        ScrollAndClick(ChoiceDoneButton);
        new WebDriverWait(Driver, TimeSpan.FromSeconds(10)).Until(_ => GetChoiceValue(fieldLabel).Contains(option, StringComparison.OrdinalIgnoreCase));
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
                // The location field only accepts a fix within 15 ft; a plain mocked location reports ~16 ft, so set
                // the accuracy explicitly. "mobile:" commands run in the native context
                string context = androidDriver.Context;
                androidDriver.Context = "NATIVE_APP";
                try
                {
                    androidDriver.ExecuteScript("mobile: setGeolocation", new Dictionary<string, object>
                    {
                        ["latitude"] = latitude,
                        ["longitude"] = longitude,
                        ["altitude"] = 0,
                        ["accuracy"] = 1
                    });
                }
                catch (WebDriverException)
                {
                    // Older UiAutomator2 drivers lack mobile: setGeolocation; the plain location is less accurate, so
                    // the location step may still fall back to the map
                    androidDriver.Location = new OpenQA.Selenium.Appium.Location { Latitude = latitude, Longitude = longitude, Altitude = 0 };
                }
                finally
                {
                    androidDriver.Context = context;
                }
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
        InjectEmulatorGpsFix();
        ScrollAndClick(LocateButton);

        // Prefer GPS: the "tap here" map link can show while the fix is still being acquired, so it only
        // decides the outcome once GPS has had its full timeout (the app fills 0,0 as a best estimate)
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(gpsTimeoutSeconds)) { PollingInterval = TimeSpan.FromSeconds(2) };
        wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
        bool gotGps;
        try
        {
            gotGps = wait.Until(_ =>
            {
                if (HasRealCoordinates()) return true;
                InjectEmulatorGpsFix();
                return false;
            });
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
            // On a phone the floating chat bubble sits on top of CONFIRM
            ScrollAndClick(MapConfirmButton, 30, jsClickWhenIntercepted: true);
        }

        new WebDriverWait(Driver, TimeSpan.FromSeconds(30)).Until(_ => HasRealCoordinates());
        // Address Number / Street Name are reverse-geocoded from the coordinates. The lookup sometimes fails
        // ("Failed to fetch your address"), so ask for the location once more before giving up
        if (!WaitForStreetName(30))
        {
            Console.WriteLine("📍 Address lookup failed. Requesting the location again...");
            InjectEmulatorGpsFix();
            ScrollAndClick(LocateButton);
            if (!WaitForStreetName(30))
            {
                throw new WebDriverTimeoutException(
                    $"Street Name was not filled in for {ReadInputValue(LatitudeInput)}, {ReadInputValue(LongitudeInput)} (reverse geocoding failed twice).");
            }
        }

        var location = (ReadInputValue(LatitudeInput), ReadInputValue(LongitudeInput));
        Console.WriteLine($"📍 Location captured: {location.Item1}, {location.Item2} ({GetFieldValue("Address Number")} {GetFieldValue("Street Name")})");
        return location;
    }

    private bool WaitForStreetName(int timeoutSeconds)
    {
        try
        {
            return new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds)).Until(_ => GetFieldValue("Street Name").Length > 0);
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }

    // Emulators: Appium's mocked location doesn't always reach the app (it kept the emulator's default fix on
    // Android 15), so also send the mock fix through the emulator console, which arrives as a real GPS reading
    private void InjectEmulatorGpsFix()
    {
        if (Driver is not AndroidDriver androidDriver) return;
        string? udid = AdbHelper.GetUdid(androidDriver);
        if (udid == null || !udid.StartsWith("emulator-")) return;

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        AdbHelper.Run(udid, $"emu geo fix {MockLongitude.ToString(culture)} {MockLatitude.ToString(culture)} 0", timeoutMs: 5000);
    }

    private bool HasRealCoordinates()
    {
        var lat = ReadInputValue(LatitudeInput);
        var lng = ReadInputValue(LongitudeInput);
        return lat.Length > 0 && lng.Length > 0 && !(lat == "0" && lng == "0");
    }

    // The choice picker reopens with the field's current value already ticked, and clicking a ticked checkbox
    // clears it, so only click when it isn't checked yet
    protected void EnsureChecked(IWebElement checkbox)
    {
        ScrollIntoView(checkbox);
        if (checkbox.GetDomProperty("checked") is "true" or "True")
        {
            Console.WriteLine("☑️ Option already selected in the picker. Leaving it checked.");
            return;
        }
        try
        {
            checkbox.Click();
        }
        catch (Exception ex) when (ex is ElementNotInteractableException or ElementClickInterceptedException)
        {
            // The picker modal can still be animating in; ion-checkbox toggles on a DOM click event as well
            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].click();", checkbox);
        }
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

    public bool IsGenerateQrPromptShown(int timeoutSeconds = 10) => FindVisible(GenerateQrAlert, timeoutSeconds) != null;

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
        // The modal is torn down while it is being checked; a stale image just means it is closing, so poll again
        var closedWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(15));
        closedWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        closedWait.Until(d => !d.FindElements(QrCodeImage).Any(e => e.Displayed));
    }
}
