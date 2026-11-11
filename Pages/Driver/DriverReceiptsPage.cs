using System.Text.RegularExpressions;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Pages.Driver;

// A receipt card on the Receipts tab: number, date and quantity ("—" when the receipt has no volume or weight)
public record ReceiptCard(string Id, string Date, string Quantity, string Unit);

// RecoveryTrac Driver app Receipts tab (/receipts, titled "Search Receipts"): date filter (today by default), search by
// number, one card per receipt, and a footer with the count ("1 Receipt", "N Receipts") and Export. The header's QR
// icon opens the receipt scanner while a load is active (the load scanner otherwise). Open it with
// DriverHomePage.OpenTab("receipts")
public class DriverReceiptsPage : BasePage
{
    public DriverReceiptsPage(IWebDriver driver)
        : base(driver)
    {
    }

    // Ionic keeps hidden copies of cached pages: everything is scoped to the receipts page and read when visible
    private const string Page = "app-receipts";
    private static readonly By Cards = By.CssSelector($"{Page} .receipt-card");
    private static readonly By EmptyState = By.CssSelector($"{Page} .empty-state__message");
    private static readonly By Count = By.CssSelector($"{Page} .footer-stats__count");
    private static readonly By SearchBox = By.CssSelector($"{Page} .filters-section__search input");
    private static readonly By EmptyStateClearFilters = By.CssSelector($"{Page} .empty-state__action");
    private static readonly By HeaderScanIcon = By.CssSelector($"{Page} ion-header ion-icon[name='qr-code-outline']");

    private static readonly Regex CountText = new(@"^(\d+) Receipts?$");

    // Waits until the list has loaded (cards or an empty state instead of the skeleton)
    public void WaitUntilLoaded(int timeoutSeconds = 20)
    {
        var wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(timeoutSeconds));
        wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));
        try
        {
            wait.Until(d => d.FindElements(Cards).Any(e => e.Displayed) || d.FindElements(EmptyState).Any(e => e.Displayed));
        }
        catch (WebDriverTimeoutException ex)
        {
            throw new WebDriverTimeoutException($"The Receipts tab did not finish loading (URL: {Driver.Url}).", ex);
        }
    }

    // The number of receipts the filters show: the footer count, or 0 on an empty state
    public int ShownCount()
    {
        WaitUntilLoaded();
        string? text = Driver.FindElements(Count).FirstOrDefault(e => e.Displayed)?.Text.Trim();
        if (text == null) return 0;
        var match = CountText.Match(text);
        return match.Success ? int.Parse(match.Groups[1].Value)
            : throw new InvalidOperationException($"Unexpected Receipts footer count '{text}'.");
    }

    // The visible cards, in the order shown
    public IReadOnlyList<ReceiptCard> CardsShown()
    {
        WaitUntilLoaded();
        return Driver.FindElements(Cards).Where(c => c.Displayed).Select(c => new ReceiptCard(
            c.FindElement(By.CssSelector(".receipt-card__ticket")).Text.Trim(),
            c.FindElement(By.CssSelector(".receipt-card__date")).Text.Trim(),
            c.FindElement(By.CssSelector(".receipt-card__qty-value")).Text.Trim(),
            c.FindElement(By.CssSelector(".receipt-card__qty-unit")).Text.Trim())).ToList();
    }

    public ReceiptCard? Card(string receiptId) => CardsShown().FirstOrDefault(c => c.Id == receiptId);

    // The empty state's text ("No receipts collected yet." or "No receipts found for …"), null when cards are shown
    public string? EmptyStateMessage()
    {
        WaitUntilLoaded();
        return Driver.FindElements(EmptyState).FirstOrDefault(e => e.Displayed)?.Text.Trim();
    }

    // Types into "Search by number" (filters as you type) and waits for the list to settle
    public void Search(string text)
    {
        var box = FindVisible(SearchBox, 15) ?? throw new NoSuchElementException("The Receipts tab's 'Search by number' box was not displayed.");
        box.Clear();
        box.SendKeys(text);
        // Each keystroke filters the list; give Angular a moment after the last one
        Thread.Sleep(800);
        WaitUntilLoaded();
    }

    // The empty state's Clear Filters (search cleared, dates back to today)
    public void ClearFiltersFromEmptyState()
    {
        var button = FindVisible(EmptyStateClearFilters, 10) ?? throw new NoSuchElementException("The empty state shows no 'Clear Filters' button.");
        button.Click();
        Thread.Sleep(800);
        WaitUntilLoaded();
    }

    // Taps a receipt card (outside its checkbox), which opens the receipt
    public void Open(string receiptId)
    {
        WaitUntilLoaded();
        var card = Driver.FindElements(Cards).Where(c => c.Displayed)
            .FirstOrDefault(c => c.FindElement(By.CssSelector(".receipt-card__ticket")).Text.Trim() == receiptId)
            ?? throw new NoSuchElementException($"Receipt {receiptId} is not listed on the Receipts tab.");
        card.FindElement(By.CssSelector(".receipt-card__body")).Click();
    }

    // The header's QR icon: the receipt scanner while a load is active. Stage the QR first (QrScanFeed.Stage)
    public void ScanFromHeader()
    {
        var icon = FindVisible(HeaderScanIcon, 15) ?? throw new NoSuchElementException("The Receipts tab's header shows no QR scan icon.");
        try
        {
            icon.Click();
        }
        catch (Exception ex) when (ex is ElementClickInterceptedException or ElementNotInteractableException)
        {
            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].closest('ion-button').click();", icon);
        }
    }

    // The date range as the empty state names it for a single day ("Nov 11, 2026"), in the device's locale-free
    // dayjs format "MMM D, YYYY" and the device's local date
    public string TodayLabel() => ((IJavaScriptExecutor)Driver).ExecuteScript(
        "const d = new Date();" +
        "const m = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'][d.getMonth()];" +
        "return m + ' ' + d.getDate() + ', ' + d.getFullYear();") as string ?? string.Empty;
}
