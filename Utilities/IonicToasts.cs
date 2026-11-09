using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace MobileAutomation.Tests.Utilities;

// Reads Ionic toasts in a WebView (Flex or the Driver app). The driver must be in the WEBVIEW context
public static class IonicToasts
{
    // Toasts last a couple of seconds and can be gone before the test gets to read them (it happened with two node
    // pairs running in parallel), so record each one as Ionic adds it. Call before the action that raises the toast
    public static void StartRecording(IWebDriver driver)
    {
        const string script =
            "if (!window.__rtToasts) {" +
            "  window.__rtToasts = [];" +
            "  const rec = t => {" +
            "    const m = (t.message || t.shadowRoot?.querySelector('.toast-message')?.textContent || '').toString().trim();" +
            "    if (m && !window.__rtToasts.includes(m)) window.__rtToasts.push(m);" +
            "  };" +
            "  new MutationObserver(ms => ms.forEach(m => m.addedNodes.forEach(n => {" +
            "    if (n.tagName === 'ION-TOAST') { rec(n); n.addEventListener('ionToastDidPresent', () => rec(n)); }" +
            "  }))).observe(document.body, { childList: true, subtree: true });" +
            "}" +
            "window.__rtToasts.length = 0;";
        ((IJavaScriptExecutor)driver).ExecuteScript(script);
    }

    // Message of the toast on screen (Ionic renders it in the toast's shadow DOM), or the last one recorded since
    // StartRecording when it has already closed. Null when none shows within the timeout
    public static string? Message(IWebDriver driver, int timeoutSeconds = 10)
    {
        const string script =
            "const t = [...document.querySelectorAll('ion-toast')].pop();" +
            "const m = t && t.shadowRoot && t.shadowRoot.querySelector('.toast-message');" +
            "const shown = m ? m.textContent.trim() : (t ? (t.message || '').toString() : '');" +
            "return shown || (window.__rtToasts || []).slice(-1)[0] || null;";
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            return wait.Until(d => ((IJavaScriptExecutor)d).ExecuteScript(script) as string is { Length: > 0 } text ? text : null);
        }
        catch (WebDriverTimeoutException)
        {
            return null;
        }
    }
}
