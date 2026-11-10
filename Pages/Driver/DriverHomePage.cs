using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using MobileAutomation.Tests.Utilities;

namespace MobileAutomation.Tests.Pages.Driver;

// An Ionic alert the Driver app showed, and the button the test closed it with
public record DriverAlert(string Header, string Message, IReadOnlyList<string> Buttons, string ClickedButton);

// RecoveryTrac Driver app Home (#/home): the CURRENT LOAD card and its QR code, plus the Scan QR Code button.
// DOM locators: Web mode only for now (the emulator camera flow is not automated).
public class DriverHomePage : BasePage
{
    public DriverHomePage(IWebDriver driver)
        : base(driver)
    {
    }

    private const string NoActiveLoad = "NO ACTIVE LOAD";

    private static string Upper(string expression) =>
        $"translate({expression}, 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ')";

    // Innermost element whose whole text is the caption, whatever the tag (ion-button, button, div, span...);
    // the click bubbles up to the actual button
    private static By ButtonByText(string caption) => By.XPath(
        $"//*[{Upper("normalize-space(.)")}='{caption}'][not(*[{Upper("normalize-space(.)")}='{caption}'])]");

    // "Scan QR Code" in Driver 1.2+, "Scan Load Code" in 1.1.x
    private By ScanQrCodeButton => By.XPath($"{ButtonByText("SCAN QR CODE").Criteria} | {ButtonByText("SCAN LOAD CODE").Criteria}");

    // The load number ("No Active Load" until a collection QR is scanned) sits right after the CURRENT LOAD caption
    private By CurrentLoadValue => By.XPath(
        $"//*[{Upper("normalize-space(text())")}='CURRENT LOAD']/following-sibling::*[normalize-space()][1]");

    private By CancelLoadButton => ButtonByText("CANCEL LOAD");
    private By AlertButtons => By.CssSelector("ion-alert button.alert-button");

    // Shown when the scanned collection QR already has receipts (re-used ticket); a fresh run never sees it
    private By DuplicateLoadOkButton => By.XPath(
        "//ion-alert[.//*[contains(normalize-space(),'Possible Duplicate Load')]]//button[translate(normalize-space(),'ok','OK')='OK']");

    // Largest visible image-like element in Home's load card: the active load's QR code. Scoped to app-home because
    // the Welcome page (with its large logo) stays laid out behind Home
    private const string FindLargestQrScript =
        "let best = null, bestArea = 0;" +
        "const scope = document.querySelector('app-home [class^=\"load-card\"], app-home [class*=\" load-card\"]')?.closest('ion-card, .load-card') || document.querySelector('app-home') || document;" +
        "scope.querySelectorAll('img, canvas, svg').forEach(el => {" +
        "  const r = el.getBoundingClientRect();" +
        "  const visible = r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden';" +
        "  if (visible && r.width * r.height > bestArea) { best = el; bestArea = r.width * r.height; }" +
        "});" +
        "return best;";

    public string CurrentLoad => FindVisible(CurrentLoadValue, 10)?.Text.Trim() ?? string.Empty;

    public bool HasActiveLoad
    {
        get
        {
            string load = CurrentLoad;
            return load.Length > 0 && !load.Equals(NoActiveLoad, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Whether the last WaitForActiveLoad had to acknowledge a 'Possible Duplicate Load' alert
    public bool DuplicateLoadAlertShown { get; private set; }

    public bool IsScanQrCodeShown(int timeoutSeconds = 5) => FindVisible(ScanQrCodeButton, timeoutSeconds) != null;

    // Captions of the visible Home buttons (Scan QR Code, or Scan Receipt Code / Cancel Load with an active load)
    public IReadOnlyList<string> VisibleHomeActions() =>
        Driver.FindElements(By.CssSelector("app-home ion-button, app-home button"))
            .Where(e => e.Displayed)
            .Select(e => e.Text.Trim())
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();

    // Stage the QR first (QrScanFeed.Stage); on a device the page objects here run in the app's WEBVIEW context.
    // cancelActiveLoad: false scans on top of the current load, as a driver scanning a second ticket would; Scan QR
    // Code must then still be offered
    public void ScanQrCode(bool cancelActiveLoad = true)
    {
        if (cancelActiveLoad) CancelStaleLoadIfPresent();
        ClickVisible(ScanQrCodeButton, "Scan QR Code");
    }

    // BLE builds: advertises this phone for 2 minutes and shows its truck code ("Your truck code RTD-XXXX") for the
    // monitor to pick in Flex. Offered next to Scan QR Code, and under Cancel Load when a load is active
    public void ReceiveViaBluetooth() => ClickVisible(ButtonByText("RECEIVE VIA BLUETOOTH"), "Receive via Bluetooth");

    // The POC's receive mode button; R1 drops the need for it (10082-NDT-1-N02)
    public bool IsReceiveViaBluetoothShown(int timeoutSeconds = 5) => FindVisible(ButtonByText("RECEIVE VIA BLUETOOTH"), timeoutSeconds) != null;

    // Whether the POC's receive panel (Receive via Bluetooth) is showing, i.e. the driver put the app in receive mode
    public bool IsReceiving => Driver.FindElements(By.CssSelector("app-home .receive-panel")).Any(e => e.Displayed);

    // Bottom tab bar of the main layout: "home" or "receipts". Ionic doesn't reflect the tab name as an attribute, but
    // puts it in the button's id
    public void OpenTab(string tab)
    {
        ClickVisible(By.Id($"tab-button-{tab}"), tab);
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(15));
        wait.Until(d => new Uri(d.Url).AbsolutePath.TrimStart('/').StartsWith(tab, StringComparison.OrdinalIgnoreCase));
    }

    private By TruckCode => By.CssSelector("app-home .receive-panel__id");
    private By ReceiveStatus => By.CssSelector("app-home .receive-panel__status");

    // The RTD-XXXX code once the phone advertises (after "Preparing Bluetooth…")
    public string WaitForTruckCode(int timeoutSeconds = 30)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        try
        {
            return wait.Until(d =>
            {
                string? code = d.FindElements(TruckCode).FirstOrDefault(e => e.Displayed)?.Text.Trim();
                return code is { Length: > 4 } && code.StartsWith("RTD-", StringComparison.Ordinal) ? code : null;
            });
        }
        catch (WebDriverTimeoutException ex)
        {
            string status = Driver.FindElements(ReceiveStatus).FirstOrDefault(e => e.Displayed)?.Text.Trim() ?? "no receive panel";
            throw new WebDriverTimeoutException($"The Driver app did not show its truck code after Receive via Bluetooth ({status}).", ex);
        }
    }

    // Records each toast as Ionic adds it, so a short-lived one can still be read. Call before the action that raises it
    public void StartRecordingToasts() => IonicToasts.StartRecording(Driver);

    // Message of the toast on screen, e.g. "Load DG0T-LC-100H received.", or the last one recorded since
    // StartRecordingToasts when it has already closed
    public string? ToastMessage(int timeoutSeconds = 10) => IonicToasts.Message(Driver, timeoutSeconds);

    // With an active load the home button becomes Scan Receipt Code: scan the disposal ticket's QR to get the receipt
    public void ScanReceiptCode() => ClickVisible(ButtonByText("SCAN RECEIPT CODE"), "Scan Receipt Code");

    // Ionic keeps hidden copies of cached pages, so click the visible match rather than the first one in the DOM
    private void ClickVisible(By locator, string caption)
    {
        var element = FindVisible(locator, 30)
            ?? throw new NoSuchElementException($"The Driver app '{caption}' button was not displayed.");
        ScrollIntoView(element);
        try
        {
            element.Click();
        }
        catch (Exception ex) when (ex is ElementClickInterceptedException or ElementNotInteractableException)
        {
            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].click();", element);
        }
    }

    // A load left over from an earlier (aborted) run replaces Scan QR Code with Scan Receipt Code / Cancel Load,
    // so cancel it to start from "No Active Load"
    public void CancelStaleLoadIfPresent()
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(30));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        // WebDriverWait only accepts reference/bool results, so the screen state is returned as a string
        string homeState = wait.Until(d =>
        {
            if (d.FindElements(ScanQrCodeButton).Any(e => e.Displayed)) return "no-load";
            if (d.FindElements(CancelLoadButton).Any(e => e.Displayed)) return "active-load";
            return null;
        });
        if (homeState == "no-load") return;

        Console.WriteLine($"⚠️ Driver app already has an active load ('{CurrentLoad}') from an earlier run. Cancelling it...");
        if (OpenAbandonLoadPrompt())
        {
            ConfirmAbandonLoad();
        }

        if (FindVisible(ScanQrCodeButton, 30) == null)
        {
            throw new WebDriverTimeoutException($"The Driver app still shows load '{CurrentLoad}' after tapping Cancel Load.");
        }
    }

    // Taps Cancel Load. The app asks "Are you sure you want to abandon the active QR code?" with Cancel and a Confirm
    // button that counts down ("CONFIRM (5)") and stays disabled until it reaches zero. False when no prompt shows up
    public bool OpenAbandonLoadPrompt()
    {
        ClickVisible(CancelLoadButton, "Cancel Load");
        return FindVisible(AlertButtons, 5) != null;
    }

    // The abandon prompt's Confirm button, or null when no prompt is open
    private IWebElement? AbandonConfirmButton(ISearchContext d)
    {
        var buttons = d.FindElements(AlertButtons).Where(b => b.Displayed).ToList();
        return buttons.FirstOrDefault(b => (b.GetAttribute("class") ?? "").Contains("alert-button-role-confirm"))
               ?? buttons.LastOrDefault();
    }

    // The Confirm caption while the abandon prompt is open ("Confirm (3)" during the countdown), null when it is closed
    public string? AbandonConfirmCaption
    {
        get
        {
            try { return AbandonConfirmButton(Driver)?.Text.Trim(); }
            catch (StaleElementReferenceException) { return AbandonConfirmButton(Driver)?.Text.Trim(); }
        }
    }

    // True while the abandon prompt's Confirm is still counting down (disabled)
    public bool IsAbandonCountdownRunning
    {
        get
        {
            try
            {
                var confirm = AbandonConfirmButton(Driver);
                return confirm != null && (!confirm.Enabled || confirm.GetAttribute("disabled") is not null);
            }
            catch (StaleElementReferenceException)
            {
                return false;
            }
        }
    }

    // Waits for the abandon prompt's countdown to end and taps Confirm. Returns the caption it tapped
    public string ConfirmAbandonLoad(int timeoutSeconds = 20)
    {
        var confirmWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        confirmWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        var confirm = confirmWait.Until(d =>
        {
            var button = AbandonConfirmButton(d);
            return button != null && button.Enabled && button.GetAttribute("disabled") is null ? button : null;
        });
        string caption = confirm.Text.Trim();
        Console.WriteLine($"Driver app: confirming the abandon prompt with '{caption}'.");
        confirm.Click();
        return caption;
    }

    // What a driver's finger does on a Home button while an alert (e.g. the abandon prompt) covers it: sent as a
    // pointer tap at the button's centre, not an element click, so it lands on whatever is on top there, as a real
    // tap would. Returns the topmost element at that point ("ion-alert > ion-backdrop" when the alert covers it)
    public string TapHomeButtonUnderAlert(string caption)
    {
        var button = FindVisible(ButtonByText(caption.ToUpperInvariant()), 5)
            ?? throw new NoSuchElementException($"The Driver app '{caption}' button is not laid out on Home.");
        string top = ((IJavaScriptExecutor)Driver).ExecuteScript(
            "const r = arguments[0].getBoundingClientRect();" +
            "const el = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);" +
            "if (!el) return 'nothing';" +
            "const name = e => e.tagName.toLowerCase() + (typeof e.className === 'string' && e.className.trim() ? '.' + e.className.trim().split(/\\s+/)[0] : '');" +
            "const alert = el.closest('ion-alert');" +
            "return alert && alert !== el ? 'ion-alert > ' + name(el) : name(el);", button) as string ?? "nothing";
        Console.WriteLine($"Driver app: tapping '{caption}' while an alert is open; topmost element there: {top}.");
        new OpenQA.Selenium.Interactions.Actions(Driver).MoveToElement(button).Click().Perform();
        return top;
    }

    // Whether the abandon prompt is still open
    public bool IsAbandonPromptOpen
    {
        get
        {
            try { return Driver.FindElements(AlertButtons).Any(b => b.Displayed); }
            catch (StaleElementReferenceException) { return false; }
        }
    }

    // Waits for the scanned collection to become the current load and returns its number
    public string WaitForActiveLoad(int timeoutSeconds = 60)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        DuplicateLoadAlertShown = false;
        try
        {
            return wait.Until(d =>
            {
                var duplicateOk = d.FindElements(DuplicateLoadOkButton).FirstOrDefault(e => e.Displayed);
                if (duplicateOk != null)
                {
                    Console.WriteLine("⚠️ Driver app: 'Possible Duplicate Load' alert shown for the scanned QR code. Acknowledging it with OK.");
                    DuplicateLoadAlertShown = true;
                    duplicateOk.Click();
                    return null;
                }

                var load = d.FindElements(CurrentLoadValue).FirstOrDefault(e => e.Displayed)?.Text.Trim();
                return !string.IsNullOrEmpty(load) && !load.Equals(NoActiveLoad, StringComparison.OrdinalIgnoreCase) ? load : null;
            });
        }
        catch (WebDriverTimeoutException ex)
        {
            throw new WebDriverTimeoutException(
                $"The Driver app did not pick up a load after scanning (Current Load: '{CurrentLoad}'). Check that the scanner read the collection QR code.", ex);
        }
    }

    // Waits for an alert (e.g. an invalid or duplicate code after a scan), records it and closes it with its cancel
    // button when it has one, so the app's state is left as the scan made it. Null when no alert shows up
    public DriverAlert? CaptureAndDismissAlert(int timeoutSeconds = 10)
    {
        var alert = FindVisible(By.CssSelector("ion-alert"), timeoutSeconds);
        if (alert == null) return null;

        string Text(string css) => string.Join(" ", alert.FindElements(By.CssSelector(css)).Select(e => e.Text.Trim()).Where(t => t.Length > 0));
        var buttons = alert.FindElements(By.CssSelector("button.alert-button")).Where(b => b.Displayed).ToList();
        var dismiss = buttons.FirstOrDefault(b => (b.GetAttribute("class") ?? "").Contains("alert-button-role-cancel"))
                      ?? buttons.LastOrDefault();
        var captured = new DriverAlert(Text(".alert-head"), Text(".alert-message"),
            buttons.Select(b => b.Text.Trim()).ToList(), dismiss?.Text.Trim() ?? string.Empty);
        Console.WriteLine($"🔔 Driver app alert: '{captured.Header}' - '{captured.Message}' [{string.Join(", ", captured.Buttons)}], closing with '{captured.ClickedButton}'.");

        try
        {
            dismiss?.Click();
        }
        catch (Exception ex) when (ex is ElementClickInterceptedException or ElementNotInteractableException)
        {
            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].click();", dismiss);
        }
        return captured;
    }

    // Screenshot of the current load's QR code, for the Flex disposal scanner to read
    public byte[] GetCurrentLoadQrPng()
    {
        var qr = new WebDriverWait(Driver, TimeSpan.FromSeconds(15)).Until(d =>
            ((IJavaScriptExecutor)d).ExecuteScript(FindLargestQrScript) as IWebElement);
        ScrollIntoView(qr);
        Thread.Sleep(500); // Let the QR finish rendering/fading in before the capture

        // The app renders the QR as a PNG data URL (img) or a canvas: take those exact pixels. An element screenshot
        // on an emulator can be cropped by a few pixels (it clipped a finder pattern on Android 12) and then no
        // longer decodes
        var dataUrl = ((IJavaScriptExecutor)Driver).ExecuteScript(QrDataUrlScript, qr) as string;
        const string pngPrefix = "data:image/png;base64,";
        if (dataUrl != null && dataUrl.StartsWith(pngPrefix, StringComparison.Ordinal))
        {
            return Convert.FromBase64String(dataUrl[pngPrefix.Length..]);
        }
        return ((ITakesScreenshot)qr).GetScreenshot().AsByteArray;
    }

    private const string QrDataUrlScript =
        "const el = arguments[0];" +
        "if (el.tagName === 'IMG') return el.currentSrc || el.src;" +
        "if (el.tagName === 'CANVAS') { try { return el.toDataURL('image/png'); } catch (e) { return null; } }" +
        "return null;";
}
