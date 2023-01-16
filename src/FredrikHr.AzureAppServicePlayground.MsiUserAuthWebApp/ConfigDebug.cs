using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc;

namespace FredrikHr.AzureAppServicePlayground.MsiUserAuthWebApp;

public static partial class ConfigDebug
{
    [GeneratedRegex("^(?<key>[^=]+)=(?<value>.*)\\s*\\((?<provider>.+)\\)$")]
    private static partial Regex ConfigDebugLineRegex();

    public static string OnGetRequest(
        [FromServices] IConfiguration? config = null,
        [FromServices] ILogger<IConfigurationRoot>? logger = null
        )
    {
        logger ??= Microsoft.Extensions.Logging.Abstractions
            .NullLogger<IConfigurationRoot>.Instance;
        if (config is not IConfigurationRoot configRoot)
            return "No configuration present.";
        using var configReader = new StringReader(configRoot.GetDebugView());
        for (string? configLine = configReader.ReadLine(); configLine is not null; configLine = configReader.ReadLine())
            if (configLine is { Length: > 0 })
            {
                if (ConfigDebugLineRegex().Match(configLine) is { Success: true } configMatch)
                {
                    logger.LogInformation("{ConfigurationKey}={ConfigurationValue} ({ConfigurationProvider})",
                        configMatch.Groups["key"],
                        configMatch.Groups["value"],
                        configMatch.Groups["provider"]
                        );
                }
                else
                    logger.LogInformation("{ConfigurationDebug}", configLine);
            }
        return "Configuration written to logger";
    }
}
