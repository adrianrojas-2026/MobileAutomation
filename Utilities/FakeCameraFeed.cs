using System.Text;
using OpenQA.Selenium;

namespace MobileAutomation.Tests.Utilities;

// Feeds a still image to Chromium's fake camera (--use-file-for-fake-video-capture) so the apps' web QR scanners
// "see" a code produced earlier in the test. Chromium opens the file each time a page starts the camera, so
// rewriting it before tapping Scan is enough to change what the scanner reads.
public static class FakeCameraFeed
{
    internal const int Width = 640;
    internal const int Height = 480;

    // Draws the PNG centred on a white frame (the quiet zone scanners need) and returns its luma plane as base64
    private const string RasterizeScript =
        "const [png, w, h, done] = arguments;" +
        "const img = new Image();" +
        "img.onerror = () => done(null);" +
        "img.onload = () => {" +
        "  const canvas = document.createElement('canvas'); canvas.width = w; canvas.height = h;" +
        "  const ctx = canvas.getContext('2d');" +
        "  ctx.fillStyle = '#fff'; ctx.fillRect(0, 0, w, h);" +
        "  ctx.imageSmoothingEnabled = false;" +
        "  const scale = Math.min(w, h) * 0.8 / Math.max(img.width, img.height);" +
        "  const dw = img.width * scale, dh = img.height * scale;" +
        "  ctx.drawImage(img, (w - dw) / 2, (h - dh) / 2, dw, dh);" +
        "  const rgba = ctx.getImageData(0, 0, w, h).data;" +
        "  const luma = new Uint8Array(w * h);" +
        "  for (let i = 0; i < luma.length; i++) luma[i] = (0.299 * rgba[4 * i] + 0.587 * rgba[4 * i + 1] + 0.114 * rgba[4 * i + 2]) | 0;" +
        "  let bin = '';" +
        "  for (let i = 0; i < luma.length; i += 8192) bin += String.fromCharCode.apply(null, luma.subarray(i, i + 8192));" +
        "  done(btoa(bin));" +
        "};" +
        "img.src = 'data:image/png;base64,' + png;";

    // Plain white frame, so a camera opened before any QR is staged shows nothing scannable
    public static void WriteBlank(string path) => WriteFrame(path, Enumerable.Repeat((byte)255, Width * Height).ToArray());

    // rasterizer: any open browser/WebView session; it only decodes and scales the PNG, its page is untouched
    public static void Show(string? path, byte[] png, IWebDriver rasterizer)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new NotSupportedException("The fake camera feed is only available in Web mode; QR scanning on emulators is not automated yet.");
        }

        WriteFrame(path, RasterizeLuma(png, rasterizer));
        Console.WriteLine($"🎥 Fake camera now shows the staged QR code ({Path.GetFileName(path)}).");
    }

    // Width x Height greyscale (one byte per pixel) of the PNG, centred on white
    internal static byte[] RasterizeLuma(byte[] png, IWebDriver rasterizer)
    {
        var lumaBase64 = ((IJavaScriptExecutor)rasterizer).ExecuteAsyncScript(RasterizeScript, Convert.ToBase64String(png), Width, Height) as string
            ?? throw new InvalidOperationException("The browser could not decode the QR code image.");
        return Convert.FromBase64String(lumaBase64);
    }

    // Single-frame Y4M (Chromium loops it); greyscale is enough for a QR code, so chroma is neutral
    private static void WriteFrame(string path, byte[] luma)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var chroma = new byte[Width / 2 * (Height / 2)];
        Array.Fill(chroma, (byte)128);

        using var file = File.Create(path);
        file.Write(Encoding.ASCII.GetBytes($"YUV4MPEG2 W{Width} H{Height} F30:1 Ip A1:1 C420jpeg\nFRAME\n"));
        file.Write(luma);
        file.Write(chroma);
        file.Write(chroma);
    }
}
