using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages.Driver;

// RecoveryTrac Driver app Receipt (/single-receipt/<receipt id>), opened after scanning the disposal ticket's QR.
// Each detail is a caption (RECEIPT IDENTIFIER, TRUCK NUMBER, ...) followed by its value. Web mode only for now.
public class DriverReceiptPage : BasePage
{
    public DriverReceiptPage(IWebDriver driver)
        : base(driver)
    {
    }

    private const string ReceiptRoute = "/single-receipt/";

    private static string Upper(string expression) =>
        $"translate({expression}, 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ')";

    private static By DetailValue(string caption) => By.XPath(
        $"//*[{Upper("normalize-space(text())")}='{caption.ToUpperInvariant()}']/following-sibling::*[normalize-space()][1]");

    // Waits for the scanner to hand over to the receipt and returns its identifier (e.g. RQTE-LC-100N.A)
    public string WaitForReceipt(int timeoutSeconds = 60)
    {
        try
        {
            new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds))
                .Until(d => new Uri(d.Url).AbsolutePath.StartsWith(ReceiptRoute, StringComparison.OrdinalIgnoreCase));
        }
        catch (WebDriverTimeoutException ex)
        {
            throw new WebDriverTimeoutException(
                $"The Driver app did not open a receipt after scanning the disposal QR code (URL: {Driver.Url}).", ex);
        }
        return GetValue("Receipt Identifier");
    }

    public string GetValue(string caption) =>
        FindVisible(DetailValue(caption), 15)?.Text.Trim()
        ?? throw new NoSuchElementException($"The receipt does not show a '{caption}' detail.");
}
