using System.Globalization;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages.Driver;

// RecoveryTrac Driver app Receipt (/single-receipt/<receipt id>), opened after scanning the disposal ticket's QR or from
// a card on the Receipts tab. A header with the receipt ID and date (no captions), then one card per field the QR
// carries: a label (RECEIPT IDENTIFIER, TRUCK NUMBER, ... as Flex names them; the app only upper-cases them in CSS)
// followed by its value. A Back button in the footer
public class DriverReceiptPage : BasePage
{
    public DriverReceiptPage(IWebDriver driver)
        : base(driver)
    {
    }

    private const string ReceiptRoute = "/single-receipt/";

    // How the app shows a receipt's date (dayjs "MM/DD/YYYY hh:mm A", device local time)
    public const string DateFormat = "MM/dd/yyyy hh:mm tt";

    private static string Upper(string expression) =>
        $"translate({expression}, 'abcdefghijklmnopqrstuvwxyz', 'ABCDEFGHIJKLMNOPQRSTUVWXYZ')";

    private static By DetailValue(string caption) => By.XPath(
        $"//*[{Upper("normalize-space(text())")}='{caption.ToUpperInvariant()}']/following-sibling::*[normalize-space()][1]");

    // Ionic keeps hidden copies of cached pages, so only the visible receipt counts
    private static readonly By HeaderId = By.CssSelector("app-single-receipt .receipt-header__ticket-no");
    private static readonly By HeaderDate = By.CssSelector("app-single-receipt .receipt-header__date");
    private static readonly By FieldCards = By.CssSelector("app-single-receipt .field-card");
    private static readonly By BackButton = By.XPath(
        "//app-single-receipt//ion-footer//ion-button[normalize-space()='Back']");

    // Whether the app is on a receipt right now (no wait)
    public bool IsShown => new Uri(Driver.Url).AbsolutePath.StartsWith(ReceiptRoute, StringComparison.OrdinalIgnoreCase);

    // The receipt ID in the route (/single-receipt/RQTE-LC-100N.A)
    public string RouteId => Uri.UnescapeDataString(new Uri(Driver.Url).AbsolutePath[ReceiptRoute.Length..]);

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

    // The receipt number in the header (the receipt's identifier in the app's database)
    public string Id => FindVisible(HeaderId, 15)?.Text.Trim()
        ?? throw new NoSuchElementException("The receipt does not show its number in the header.");

    // The date in the header, as shown ("11/11/2026 10:42 AM")
    public string DateText => FindVisible(HeaderDate, 15)?.Text.Trim()
        ?? throw new NoSuchElementException("The receipt does not show its date in the header.");

    // Every field card, label -> value, in the order shown. Labels as the app's DOM holds them (the CSS upper-cases
    // them on screen), so callers compare them case-insensitively
    public IReadOnlyDictionary<string, string> Fields()
    {
        FindVisible(FieldCards, 15);
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in Driver.FindElements(FieldCards).Where(c => c.Displayed))
        {
            string label = card.FindElement(By.CssSelector(".field-card__label")).Text.Trim();
            string value = card.FindElement(By.CssSelector(".field-card__value")).Text.Trim();
            if (label.Length > 0) fields.TryAdd(label, value);
        }
        return fields;
    }

    // Parses a date as the app shows it; null when it is in another format (e.g. a raw v2 receipt date)
    public static DateTime? ParseDate(string text) =>
        DateTime.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    // The device's local time now, read in the app's WebView, so a receipt date can be compared with the clock and
    // time zone that produced it (the emulator's, not the test host's)
    public DateTime DeviceNow()
    {
        var parts = ((IJavaScriptExecutor)Driver).ExecuteScript(
            "const d = new Date(); return [d.getFullYear(), d.getMonth() + 1, d.getDate(), d.getHours(), d.getMinutes(), d.getSeconds()];")
            as IReadOnlyCollection<object> ?? throw new InvalidOperationException("Could not read the device's clock.");
        var n = parts.Select(Convert.ToInt32).ToArray();
        return new DateTime(n[0], n[1], n[2], n[3], n[4], n[5]);
    }

    // Taps Back and returns the route it led to (e.g. /home or /receipts)
    public string GoBack(int timeoutSeconds = 15)
    {
        var back = FindVisible(BackButton, 15) ?? throw new NoSuchElementException("The receipt's Back button was not displayed.");
        back.Click();
        try
        {
            new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds)).Until(_ => !IsShown);
        }
        catch (WebDriverTimeoutException ex)
        {
            throw new WebDriverTimeoutException($"Back did not leave the receipt (URL: {Driver.Url}).", ex);
        }
        return new Uri(Driver.Url).AbsolutePath;
    }
}
