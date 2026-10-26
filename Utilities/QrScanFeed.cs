using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using ZXing;

namespace MobileAutomation.Tests.Utilities;

// Hands a QR code to an app's scanner before it starts scanning.
//   Web mode: the browser's fake camera shows the image (FakeCameraFeed).
//   Mobile:   the emulator cameras cannot show a given image on Android 12, so the QR is decoded here and the
//             app's ML Kit BarcodeScanner plugin is answered from inside its WebView: startScan reports the
//             decoded text through the regular 'barcodesScanned' event instead of opening the camera. The rest
//             of the app's scan handling (validation, navigation, data import) runs unchanged.
public static class QrScanFeed
{
    // Wraps Capacitor's JS bridge for the BarcodeScanner plugin only; calls to every other plugin go to native code
    private const string InstallScannerStubScript =
        "const cap = window.Capacitor;" +
        "if (!cap) return 'no-capacitor';" +
        "window.__rtQrValue = arguments[0];" +
        "if (!cap.__rtScannerStub) {" +
        "  cap.__rtScannerStub = { listeners: {}, timer: null, nextId: 0 };" +
        "  const stub = cap.__rtScannerStub;" +
        "  const nativeCallback = cap.nativeCallback.bind(cap);" +
        "  const nativePromise = cap.nativePromise.bind(cap);" +
        "  const stop = () => { clearTimeout(stub.timer); stub.timer = null; };" +
        "  cap.nativeCallback = (plugin, method, options, callback) => {" +
        "    if (plugin !== 'BarcodeScanner' || method !== 'addListener') return nativeCallback(plugin, method, options, callback);" +
        "    const id = 'rt-scan-' + (++stub.nextId);" +
        "    (stub.listeners[options.eventName] = stub.listeners[options.eventName] || {})[id] = callback;" +
        "    return id;" +
        "  };" +
        "  cap.nativePromise = (plugin, method, options) => {" +
        "    if (plugin !== 'BarcodeScanner') return nativePromise(plugin, method, options);" +
        "    switch (method) {" +
        "      case 'startScan': {" +
        "        stop();" +
        // Report the code once, shortly after the scanner opens. Repeating it is not safe: Flex's scanner modal
        // dismisses itself on the first result, and a second one dismisses the form modal underneath
        "        stub.timer = setTimeout(() => {" +
        "          stub.timer = null;" +
        "          const value = window.__rtQrValue;" +
        "          const barcode = { rawValue: value, displayValue: value, format: 'QR_CODE', valueType: 'TEXT', cornerPoints: [] };" +
        "          Object.values(stub.listeners['barcodesScanned'] || {}).forEach(cb => cb({ barcodes: [barcode] }));" +
        "        }, 800);" +
        "        return Promise.resolve();" +
        "      }" +
        "      case 'stopScan': stop(); return Promise.resolve();" +
        "      case 'removeListener':" +
        "        Object.values(stub.listeners).forEach(l => delete l[options && options.callbackId]);" +
        "        return Promise.resolve();" +
        "      case 'removeAllListeners': stop(); stub.listeners = {}; return Promise.resolve();" +
        "      case 'checkPermissions': case 'requestPermissions': return Promise.resolve({ camera: 'granted' });" +
        "      case 'isSupported': return Promise.resolve({ supported: true });" +
        "      case 'isGoogleBarcodeScannerModuleAvailable': return Promise.resolve({ available: true });" +
        "      case 'getMaxZoomRatio': return Promise.resolve({ zoomRatio: 1 });" +
        "      case 'getMinZoomRatio': return Promise.resolve({ zoomRatio: 1 });" +
        "      case 'getZoomRatio': return Promise.resolve({ zoomRatio: 1 });" +
        "      case 'isTorchEnabled': return Promise.resolve({ enabled: false });" +
        "      case 'isTorchAvailable': return Promise.resolve({ available: false });" +
        "      default: return Promise.resolve();" +
        "    }" +
        "  };" +
        "}" +
        "return 'ok';";

    // appDriver: the app that is about to scan. rasterizer: any browser/WebView session that can decode the PNG.
    // browserFeedPath: that app's fake camera file in Web mode (null on devices)
    public static void Stage(IWebDriver appDriver, byte[] qrPng, IWebDriver rasterizer, string? browserFeedPath)
    {
        if (appDriver is not AndroidDriver androidDriver)
        {
            FakeCameraFeed.Show(browserFeedPath, qrPng, rasterizer);
            return;
        }

        string qrText = Decode(qrPng, rasterizer);
        if (!androidDriver.Context.StartsWith("WEBVIEW", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The app must be in its WEBVIEW context before staging a QR code.");
        }
        var outcome = androidDriver.ExecuteScript(InstallScannerStubScript, qrText) as string;
        if (outcome != "ok")
        {
            throw new InvalidOperationException($"Could not hook the app's barcode scanner ({outcome}).");
        }
        Console.WriteLine($"🔳 Scanner primed with the staged QR code ({qrText.Length} characters).");
    }

    // Frame sizes tried when decoding. Dense codes (the disposal QR is ~125 modules) need several pixels per module:
    // squeezed into the 640x480 camera frame they get ~3 px each and nearest-neighbour scaling leaves them uneven
    private static readonly (int Width, int Height)[] DecodeFrames =
        [(1200, 1200), (1600, 1600), (FakeCameraFeed.Width, FakeCameraFeed.Height)];

    // Text encoded in a QR code image
    public static string Decode(byte[] qrPng, IWebDriver rasterizer)
    {
        var reader = new BarcodeReaderGeneric
        {
            Options = { PossibleFormats = [BarcodeFormat.QR_CODE], TryHarder = true }
        };
        // The staged codes are clean, upright app renders, so PureBarcode can skip the finder-pattern detector,
        // which misreads some codes' data as a finder (e.g. a Node1 driver load QR that only decoded at one frame size)
        var pureReader = new BarcodeReaderGeneric
        {
            Options = { PossibleFormats = [BarcodeFormat.QR_CODE], PureBarcode = true }
        };
        foreach (var (width, height) in DecodeFrames)
        {
            byte[] luma = FakeCameraFeed.RasterizeLuma(qrPng, rasterizer, width, height);
            var source = new PlanarYUVLuminanceSource(luma, width, height, 0, 0, width, height, false);
            string? text = reader.Decode(source)?.Text ?? pureReader.Decode(source)?.Text;
            if (text != null)
            {
                return text;
            }
        }
        throw new InvalidOperationException("No QR code could be read from the staged image.");
    }
}
