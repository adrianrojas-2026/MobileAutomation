using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages.Driver;

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

    // Stage the QR first (QrScanFeed.Stage); on a device the page objects here run in the app's WEBVIEW context
    public void ScanQrCode()
    {
        CancelStaleLoadIfPresent();
        ClickVisible(ScanQrCodeButton, "Scan QR Code");
    }

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
    private void CancelStaleLoadIfPresent()
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
        ClickVisible(CancelLoadButton, "Cancel Load");

        // Confirmation alert: its confirm button counts down ("CONFIRM (5)") and stays disabled until it reaches zero
        if (FindVisible(AlertButtons, 5) != null)
        {
            var confirmWait = new WebDriverWait(Driver, TimeSpan.FromSeconds(20));
            confirmWait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
            var confirm = confirmWait.Until(d =>
            {
                var buttons = d.FindElements(AlertButtons).Where(b => b.Displayed).ToList();
                var button = buttons.FirstOrDefault(b => (b.GetAttribute("class") ?? "").Contains("alert-button-role-confirm"))
                             ?? buttons.LastOrDefault();
                return button != null && button.Enabled && button.GetAttribute("disabled") is null ? button : null;
            });
            Console.WriteLine($"Driver app: confirming cancel with '{confirm.Text.Trim()}'.");
            confirm.Click();
        }

        if (FindVisible(ScanQrCodeButton, 30) == null)
        {
            throw new WebDriverTimeoutException($"The Driver app still shows load '{CurrentLoad}' after tapping Cancel Load.");
        }
    }

    // Waits for the scanned collection to become the current load and returns its number
    public string WaitForActiveLoad(int timeoutSeconds = 60)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        try
        {
            return wait.Until(d =>
            {
                var duplicateOk = d.FindElements(DuplicateLoadOkButton).FirstOrDefault(e => e.Displayed);
                if (duplicateOk != null)
                {
                    Console.WriteLine("⚠️ Driver app: 'Possible Duplicate Load' alert shown for the scanned QR code. Acknowledging it with OK.");
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
