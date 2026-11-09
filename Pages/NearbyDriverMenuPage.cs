using System.Text.Json;
using System.Text.RegularExpressions;
using OpenQA.Selenium;

namespace MobileAutomation.Tests.Pages;

// A status line of the menu ("Searching for nearby Driver Apps…", "Connecting…") and whether a spinner sits beside it
public record MenuStatus(string Text, bool Spinner, double Top);

public record MenuButton(string Text, bool Disabled);

// One Driver App result card. Key is its Identity Code when it shows one, otherwise its primary identity (the POC's
// RTD-XXXX); it stays the same while the card's distance changes
public record DriverCard(string Key, string Primary, int PrimaryWeight, IReadOnlyList<string> Lines, bool Truncated, bool Disabled, double Top)
{
    private static readonly Regex Distance = new(@"\bDistance unavailable\b|(^|\s)~?\d+(\.\d+)?\s?(m|ft|meters?|feet)\b", RegexOptions.IgnoreCase);

    public string Text => string.Join("\n", Lines);
    public bool ShowsDistance => Distance.IsMatch(Text);
    public string? IdentityCode => Regex.Match(Text, @"Identity Code:\s*\n?\s*([^\n]+)") is { Success: true } m ? m.Groups[1].Value.Trim() : null;

    public static bool TextShowsDistance(IEnumerable<string> lines) => Distance.IsMatch(string.Join("\n", lines));
}

// What the top Flex overlay showed at one moment (T: ms since epoch, browser clock). Overlay names that overlay
// (tag#id), so the same menu can be told from a new one
public record MenuSnapshot(
    bool Open,
    long T,
    string? Overlay,
    string? Title,
    IReadOnlyList<string> Lines,
    MenuStatus? Searching,
    string? NoResults,
    MenuStatus? Progress,
    bool Spinner,
    bool Determinate,
    IReadOnlyList<MenuButton> Buttons,
    IReadOnlyList<DriverCard> Cards)
{
    public bool HasLine(string text) => Lines.Contains(text);

    public bool HasButton(string text) => Buttons.Any(b => !b.Disabled && b.Text.Equals(text, StringComparison.OrdinalIgnoreCase));

    public DriverCard? Card(string key) => Cards.FirstOrDefault(c => c.Key == key);

    public override string ToString() => Open
        ? $"{Overlay} '{Title}' [{string.Join(" | ", Lines)}] buttons [{string.Join(", ", Buttons.Select(b => b.Disabled ? $"({b.Text})" : b.Text))}]"
        : "no overlay open";
}

public record MenuMark(string What, long T);

public record MenuRecording(IReadOnlyList<MenuSnapshot> Log, IReadOnlyList<MenuMark> Marks)
{
    public long? MarkTime(string what) => Marks.FirstOrDefault(m => m.What == what)?.T;

    // The last snapshot taken at or before t (what was on screen at that moment)
    public MenuSnapshot? At(long t) => Log.LastOrDefault(s => s.T <= t);

    public IEnumerable<MenuSnapshot> Between(long from, long to) => Log.Where(s => s.T > from && s.T < to);
}

// Flex Nearby Driver menu (#10082 FEAT-NDT-2 discovery, FEAT-NDT-10 progress, FEAT-NDT-6 access flow), whatever
// Flex overlay is on top. R1 builds it as a standard Flex Action Menu whose DOM is not known yet, so it is read by the
// texts the requirements define rather than by tag or class. The same reading works on the POC's
// 'Send via Bluetooth' picker, so the tests run today and report each R1 gap by requirement ID.
// The recorder logs every change of the overlay from inside the WebView, so states that last a few hundred
// milliseconds (Connecting…) are seen and their order is known.
public class NearbyDriverMenuPage : BasePage
{
    // R1 texts (10082 requirements plan). An ASCII "..." on screen is read as "…"
    public const string SearchingText = "Searching for nearby Driver Apps…";
    public const string NoResultsText = "No nearby Driver Apps found.";
    public const string NoResultsHint = "Make sure the Driver App is open and nearby.";
    public const string TryAgain = "Try Again";
    public const string SendDataTitle = "Send Data";
    public const string ConnectingText = "Connecting…";
    public const string ReconnectingText = "Reconnecting…";
    public const string SendingDataText = "Sending data…";
    public const string Cancel = "Cancel";
    public const string NoSessionPrimary = "Driver App";
    public const string LoadingTruckDetails = "Loading truck details…";
    public const string BluetoothAccessText = "Flex needs Bluetooth access to find and connect to nearby Driver Apps.";
    public const string ContinueText = "Continue";
    public const string Done = "Done";
    public const string SentPrefix = "Sent to ";
    // FEAT-LDT-10 success: the menu closes and a toast shows "Data sent to Driver App {Identity Code}." (no Driver
    // Session) or "Data sent to Truck {Truck Number}." (Flex nearby-messages.ts)
    public const string DataSentPrefix = "Data sent to ";
    public const string DataSentToDriverApp = "Data sent to Driver App ";
    public const string DataSentToTruck = "Data sent to Truck ";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public NearbyDriverMenuPage(IWebDriver driver)
        : base(driver)
    {
    }

    // Defines window.__nearby once per page load: the snapshot of the top overlay and the elements behind it
    private const string Bootstrap = """
        if (!window.__nearby) window.__nearby = (() => {
          const norm = s => (s || '').replace(/\.\.\./g, '…').replace(/\s+/g, ' ').trim();
          const visible = el => {
            if (!el || !el.isConnected) return false;
            const r = el.getBoundingClientRect(), cs = getComputedStyle(el);
            return r.width > 0 && r.height > 0 && cs.visibility !== 'hidden' && cs.display !== 'none';
          };
          const ownText = el => norm([...el.childNodes].filter(n => n.nodeType === 3).map(n => n.textContent).join(' '));
          const disabled = el => el.disabled === true || ['', 'true'].includes(el.getAttribute('disabled'))
            || el.getAttribute('aria-disabled') === 'true' || /\b(item|button)-disabled\b/.test(el.getAttribute('class') || '')
            || getComputedStyle(el).pointerEvents === 'none';
          const top = () => {
            const open = [...document.querySelectorAll('ion-modal, ion-popover, ion-action-sheet, ion-alert')]
              .filter(o => !o.classList.contains('overlay-hidden') && visible(o));
            return open[open.length - 1] || null;
          };
          const all = root => [...root.querySelectorAll('*')].filter(visible);
          const owner = (root, test) => all(root).find(el => test(ownText(el))) || null;
          const spinnerNear = el => {
            for (let a = el, i = 0; a && i < 3; a = a.parentElement, i++) {
              if ([...a.querySelectorAll('ion-spinner, [role=progressbar]:not([aria-valuenow])')].some(visible)) return true;
            }
            return false;
          };
          const status = el => el ? { text: ownText(el), spinner: spinnerNear(el), top: el.getBoundingClientRect().top } : null;
          const identity = /Identity Code:|Capacity:|^\s*Driver App\b|^\s*RTD-/m;
          const cards = (root = top()) => {
            if (!root) return [];
            const c = all(root).filter(el => el.matches('ion-item, ion-card, button, [role=button]')
              && !el.closest('ion-footer, ion-header') && identity.test(el.innerText));
            return c.filter(el => !c.some(o => o !== el && el.contains(o)));
          };
          const primaryEl = card => all(card).find(el => ownText(el).length > 0) || card;
          const keyOf = card => {
            const m = card.innerText.match(/Identity Code:\s*\n?\s*([^\n]+)/);
            return norm(m ? m[1] : ownText(primaryEl(card)));
          };
          const truncated = card => [card, ...all(card)].some(el => getComputedStyle(el).textOverflow === 'ellipsis' && el.scrollWidth > el.clientWidth + 1)
            || /…\s*$/m.test(card.innerText);
          const buttons = (root = top()) => {
            if (!root) return [];
            const cs = cards(root);
            const b = all(root).filter(el => el.matches('ion-button, button, [role=button]') && !cs.some(c => c === el || c.contains(el)));
            return b.filter(el => !b.some(o => o !== el && o.contains(el)));
          };
          const button = text => buttons().find(el => norm(el.innerText).toLowerCase() === text.toLowerCase() && !disabled(el)) || null;
          const tapOutside = () => {
            const root = top();
            const backdrop = root && ((root.shadowRoot && root.shadowRoot.querySelector('ion-backdrop')) || root.querySelector('ion-backdrop'));
            if (!backdrop) return false;
            backdrop.click();
            return true;
          };
          const snap = () => {
            const root = top(), t = Date.now();
            if (!root) return { open: false, t, overlay: null, title: null, lines: [], searching: null, noResults: null, progress: null,
              spinner: false, determinate: false, buttons: [], cards: [] };
            const titleEl = all(root).find(el => el.matches('ion-title, h1, h2, h3, [role=heading], .alert-title, .action-sheet-title'));
            const noResults = owner(root, x => x.startsWith('No nearby Driver Apps'));
            return {
              open: true, t,
              overlay: root.tagName.toLowerCase() + '#' + (root.id || ''),
              title: titleEl ? norm(titleEl.innerText) : null,
              lines: root.innerText.split('\n').map(norm).filter(Boolean),
              searching: status(owner(root, x => x.startsWith('Searching for'))),
              noResults: noResults ? ownText(noResults) : null,
              progress: status(owner(root, x => /^(Connecting|Reconnecting|Sending|Receiving|Waiting|Preparing)/.test(x))),
              spinner: [...root.querySelectorAll('ion-spinner, [role=progressbar]:not([aria-valuenow])')].some(visible),
              determinate: [...root.querySelectorAll('ion-progress-bar, progress, [aria-valuenow]')].some(el => visible(el) && el.getAttribute('type') !== 'indeterminate')
                || /\d+\s*%|\b\d+\s*(bytes?|packets?|KB|MB)\b/i.test(root.innerText),
              buttons: buttons(root).map(el => ({ text: norm(el.innerText), disabled: disabled(el) })),
              cards: cards(root).map(c => {
                const p = primaryEl(c);
                return { key: keyOf(c), primary: ownText(p), primaryWeight: parseInt(getComputedStyle(p).fontWeight, 10) || 400,
                  lines: c.innerText.split('\n').map(norm).filter(Boolean), truncated: truncated(c), disabled: disabled(c),
                  top: c.getBoundingClientRect().top };
              })
            };
          };
          return { snap, cards, keyOf, button, tapOutside, rec: null };
        })();
        const n = window.__nearby;
        """;

    private object? Run(string script, params object[] args) =>
        ((IJavaScriptExecutor)Driver).ExecuteScript(Bootstrap + script, args);

    public MenuSnapshot Snapshot() =>
        JsonSerializer.Deserialize<MenuSnapshot>((string)Run("return JSON.stringify(n.snap());")!, Json)!;

    public bool IsOpen(int timeoutSeconds = 15) => WaitFor(s => s.Open, timeoutSeconds) != null;

    // Polls until the overlay matches; null once the timeout elapses
    public MenuSnapshot? WaitFor(Func<MenuSnapshot, bool> condition, double timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (true)
        {
            var snapshot = Snapshot();
            if (condition(snapshot)) return snapshot;
            if (DateTime.UtcNow >= deadline) return null;
            Thread.Sleep(250);
        }
    }

    // Starts (or restarts) logging every change of the top overlay, plus a sample every 100 ms for changes that come
    // from CSS alone. Unchanged snapshots (ignoring time and position) are logged once
    public void StartRecording() => Run("""
        if (n.rec) { n.rec.obs.disconnect(); clearInterval(n.rec.timer); }
        const rec = { log: [], marks: [], last: '' };
        const take = () => {
          if (rec.log.length >= 3000) return;
          const s = n.snap();
          const k = JSON.stringify(s, (key, v) => key === 't' || key === 'top' ? undefined : v);
          if (k !== rec.last) { rec.last = k; rec.log.push(s); }
        };
        rec.obs = new MutationObserver(take);
        rec.obs.observe(document.body, { subtree: true, childList: true, characterData: true, attributes: true });
        rec.timer = setInterval(take, 100);
        n.rec = rec;
        take();
        """);

    // Records an action at the browser's clock, so it can be placed among the logged snapshots
    public void Mark(string what) => Run("if (n.rec) n.rec.marks.push({ what: arguments[0], t: Date.now() });", what);

    public MenuRecording Recording() =>
        JsonSerializer.Deserialize<MenuRecording>((string)Run(
            "return JSON.stringify(n.rec ? { log: n.rec.log, marks: n.rec.marks } : { log: [], marks: [] });")!, Json)!;

    public MenuRecording StopRecording()
    {
        var recording = Recording();
        Run("if (n.rec) { n.rec.obs.disconnect(); clearInterval(n.rec.timer); n.rec = null; }");
        return recording;
    }

    // Taps the result card, as a real tap (a JS click would skip the touch handling R1 adds for list shifts)
    public void SelectCard(string key)
    {
        var card = Run("return n.cards().find(c => n.keyOf(c) === arguments[0]) || null;", key) as IWebElement
            ?? throw new NoSuchElementException($"No Driver App card '{key}' in the Nearby menu ({Snapshot()}).");
        Mark("select");
        card.Click();
    }

    // Taps the enabled button with that caption in the top overlay; false when there is none
    public bool ClickButton(string caption)
    {
        if (Run("return n.button(arguments[0]);", caption) is not IWebElement button) return false;
        Mark($"click {caption}");
        try
        {
            button.Click();
        }
        catch (Exception ex) when (ex is ElementClickInterceptedException or ElementNotInteractableException)
        {
            ((IJavaScriptExecutor)Driver).ExecuteScript("arguments[0].click();", button);
        }
        return true;
    }

    // Starts another discovery attempt: Try Again in R1, Rescan in the POC. False when neither is offered (e.g. R1
    // still searching, as it keeps searching while a Driver App is listed)
    public bool RestartSearch() => ClickButton(TryAgain) || ClickButton("Rescan");

    // Taps the top overlay's backdrop (outside the menu); false when it has none
    public bool TapOutside()
    {
        Mark("tap-outside");
        return Run("return n.tapOutside();") is true;
    }

    public void PressEscape()
    {
        Mark("escape");
        Run("document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));");
    }

    // Selects the card, then tries a passive dismissal (backdrop tap + Escape) after dismissAfterMs and taps Cancel
    // after cancelAfterMs, all timed inside the WebView: a transfer between emulators takes about a second, too short
    // to aim at with separate WebDriver round trips. Each step is marked in the recording ('cancel-missing' when no
    // Cancel was shown). False when the card is not listed
    public bool SelectThenDismissAndCancel(string key, int dismissAfterMs, int cancelAfterMs) => Run("""
        const [key, dismissAfter, cancelAfter] = arguments;
        const card = n.cards().find(c => n.keyOf(c) === key);
        if (!card) return false;
        const mark = what => n.rec && n.rec.marks.push({ what, t: Date.now() });
        mark('select');
        card.click();
        setTimeout(() => {
          mark('tap-outside'); n.tapOutside();
          mark('escape'); document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
        }, dismissAfter);
        setTimeout(() => {
          const b = n.button('Cancel');
          mark(b ? 'cancel' : 'cancel-missing');
          if (b) b.click();
        }, cancelAfter);
        return true;
        """, key, dismissAfterMs, cancelAfterMs) is true;
}
