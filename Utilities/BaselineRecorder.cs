using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MobileAutomation.Tests.Utilities;

// Saves what a QR baseline observed as JSON next to the test results, so the BLE handoff tests have a recorded
// expected result to compare against (e.g. how a second ticket or damaged data is handled today)
public static class BaselineRecorder
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Save(string scenarioId, string node, object observed)
    {
        string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Baselines");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"{scenarioId}_{node}_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        var record = new Dictionary<string, object?>
        {
            ["scenario"] = scenarioId,
            ["node"] = node,
            ["test"] = TestContext.CurrentContext.Test.Name,
            ["recordedAt"] = DateTime.Now.ToString("O"),
            ["observed"] = observed
        };
        File.WriteAllText(path, JsonSerializer.Serialize(record, Options));
        TestContext.AddTestAttachment(path, $"{scenarioId} baseline");
        MobileAutomation.Tests.Reporting.QrBaselineReport.AddBaseline(path);
        Console.WriteLine($"📒 {scenarioId} baseline recorded at: {path}");
        return path;
    }

    // Uppercase hex SHA-256 of a payload's UTF-8 bytes: compares payloads without storing them twice
    public static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
