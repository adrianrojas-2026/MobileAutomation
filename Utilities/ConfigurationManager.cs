using Microsoft.Extensions.Configuration;

namespace MobileAutomation.Tests.Utilities;

public static class TestConfiguration
{
    private static IConfiguration? _configuration;

    public static IConfiguration Configuration =>
        _configuration ??= new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile(
                "Config/appsettings.json",
                optional: false,
                reloadOnChange: false)
            .AddUserSecrets(typeof(TestConfiguration).Assembly, optional: true)
            .Build();
}