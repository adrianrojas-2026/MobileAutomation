using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages;

// "Load - Disposal" form modal. Same modal/footer/picker layout as Load - Collection, so it reuses those helpers.
// First step "Import Collection Data": Disposal Site + Scan the Driver's QR Code (or a reason it can't be scanned).
public class LoadDisposalFormPage : CollectionLoadTicketFormPage
{
    public LoadDisposalFormPage(IWebDriver driver)
        : base(driver)
    {
    }

    protected override IReadOnlyList<string> FormTitles => ["Load - Disposal"];

    private By ScanDriversQrCodeButton => By.XPath(
        $"{FormModal}//ion-button[contains(translate(normalize-space(.),'abcdefghijklmnopqrstuvwxyz','ABCDEFGHIJKLMNOPQRSTUVWXYZ'),'SCAN THE DRIVER')]");
    private By CameraPreview => By.TagName("video");

    // Opens the scanner on the QR staged in the fake camera and waits for it to read the code and close
    public void ScanDriversQrCode(int timeoutSeconds = 45)
    {
        ScrollAndClick(ScanDriversQrCodeButton, jsClickWhenIntercepted: true);

        // On a device the scanner is answered by QrScanFeed (no <video> preview); the caller checks the import
        if (Driver is OpenQA.Selenium.Appium.Android.AndroidDriver) return;

        if (FindVisible(CameraPreview, 10) == null)
        {
            Console.WriteLine("⚠️ No camera preview appeared after tapping Scan the Driver's QR Code.");
            return;
        }
        Console.WriteLine("🎥 Flex scanner is open. Waiting for it to read the driver's QR code...");
        try
        {
            new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds))
                .Until(d => !d.FindElements(CameraPreview).Any(IsStillDisplayed));
        }
        catch (WebDriverTimeoutException ex)
        {
            throw new WebDriverTimeoutException("The Flex scanner stayed open: it could not read the driver's QR code.", ex);
        }
    }

    // The scanner tears its <video> down as soon as it reads the code, so the element can vanish between
    // FindElements and Displayed; a vanished preview means the scanner closed
    private static bool IsStillDisplayed(IWebElement element)
    {
        try
        {
            return element.Displayed;
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
    }

    public string SelectFirstDisposalSite() => SelectFirstChoiceOption("Disposal Site");

    // Imported Truck Number, or empty while the step shows no such field (nothing imported yet)
    public string ImportedTruckNumber => HasField("Truck Number") ? GetFieldValue("Truck Number") : string.Empty;

    // Text of an alert Flex shows after a scan (e.g. an invalid code), closed with its cancel button when it has one,
    // otherwise its last button, so the form stays as the scan left it. Null when no alert shows up
    public string? CaptureAndCloseAlert(int timeoutSeconds = 5)
    {
        var alert = FindVisible(By.CssSelector("ion-alert"), timeoutSeconds);
        if (alert == null) return null;

        string text = string.Join(" ", alert.FindElements(By.CssSelector(".alert-head, .alert-message"))
            .Select(e => e.Text.Trim()).Where(t => t.Length > 0));
        var buttons = alert.FindElements(By.CssSelector("button.alert-button")).Where(b => b.Displayed).ToList();
        var close = buttons.FirstOrDefault(b => (b.GetAttribute("class") ?? "").Contains("alert-button-role-cancel"))
                    ?? buttons.LastOrDefault();
        Console.WriteLine($"🔔 Flex alert after the scan: '{text}', closing with '{close?.Text.Trim()}'.");
        if (close != null) ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].click();", close);
        return text;
    }
}
