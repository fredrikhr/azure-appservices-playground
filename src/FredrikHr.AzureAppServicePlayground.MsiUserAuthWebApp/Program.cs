using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;

using FredrikHr.AzureAppServicePlayground.MsiUserAuthWebApp;

using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
if (builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] is { Length: > 0 } appInsightsConnectionString)
{
    builder.Services.Configure<TelemetryConfiguration>(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}
builder.Services.AddLogging(logging =>
{
    logging.AddAzureWebAppDiagnostics();
    logging.AddApplicationInsights();
});
builder.Services.AddHttpClient<HttpClientTransport>();
builder.Services.AddSingleton<DefaultAzureCredentialOptions>(svcs =>
{
    return new()
    {
        Transport = svcs.GetRequiredService<HttpClientTransport>()
    };
});
builder.Services.AddSingleton<TokenCredentialOptions>(
    svcs => svcs.GetRequiredService<DefaultAzureCredentialOptions>()
);
builder.Services.AddSingleton<DefaultAzureCredential>(
    svcs => new(svcs.GetService<DefaultAzureCredentialOptions>() ?? new())
);
builder.Services.AddSingleton<ManagedIdentityCredential>(svcs =>
{
    var options = svcs.GetRequiredService<DefaultAzureCredentialOptions>();
    return options switch
    {
        { ManagedIdentityClientId: string clientId } => new(clientId, options),
        { ManagedIdentityResourceId: { } resourceId } => new(resourceId, options),
        _ => new(options: options),
    };
});
builder.Services.AddSingleton<TokenCredential>(
    svcs => svcs.GetRequiredService<DefaultAzureCredential>()
);
builder.Services.AddSingleton<AzureIdentityEventSourceLoggingForwarder>();

var app = builder.Build();

_ = app.Services.GetRequiredService<AzureIdentityEventSourceLoggingForwarder>();
app.MapGet("/", () => "Hello World!");
app.MapGet($"/{nameof(IConfiguration)}", ConfigDebug.OnGetRequest);
app.MapGet($"/{nameof(DefaultAzureCredentialOptions)}", ([FromServices] DefaultAzureCredentialOptions options) => options);
app.MapGet($"/{nameof(TokenCredential)}", async (
    HttpRequest httpRequ,
    [FromServices] DefaultAzureCredentialOptions options,
    [FromServices] TokenCredential credential,
    CancellationToken cancelToken
    ) =>
{
    var resourceId = options switch
    {
        { ManagedIdentityClientId: string clientId } => clientId,
        _ => (new Uri(new(httpRequ.GetEncodedUrl()), "/")).ToString()
    };
    var requCtx = new TokenRequestContext(
        new[] { $"{resourceId}/.default" },
        tenantId: options.TenantId
        );
    var accessToken = await credential.GetTokenAsync(requCtx, cancelToken);
    return $"Retrieved access token for {string.Join(" ", requCtx.Scopes)}";
});

app.Run();
