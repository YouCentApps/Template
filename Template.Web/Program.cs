using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Template.Common.Services.Api;
using Template.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Template.Common.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure app settings
//var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? string.Empty;
//var isDevelopment = builder.HostEnvironment.IsDevelopment();


// Environment and settings
var environment = new WebMyEnvironment();
var settings = new Settings(environment);


//var appConfig = new AppConfiguration
//{
//    ApiBaseUrl = apiBaseUrl,
//    IsDevelopment = isDevelopment
//};

var appConfig = new AppConfiguration
{
    ApiBaseUrl = settings.ApiUrl,
    IsDevelopment = environment.IsDevelopment()
};

builder.Services.AddSingleton<IAppConfiguration>(appConfig);

// Register storage and auth state
builder.Services.AddScoped<IStorageService, BrowserStorageService>();
builder.Services.AddScoped<AuthStateService>(sp =>
{
    var storageService = sp.GetRequiredService<IStorageService>();
    return new AuthStateService(storageService);
});

// Register the authentication message handler
builder.Services.AddScoped<AuthenticationMessageHandler>();

// Configure HttpClient with API base URL and authentication handler
builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IAppConfiguration>();
    var authHandler = sp.GetRequiredService<AuthenticationMessageHandler>();
    authHandler.InnerHandler = new HttpClientHandler();

    if (config.ApiBaseUrl is null)
    {
        throw new InvalidOperationException("API base URL is not configured.");
    }

    return new HttpClient(authHandler) { BaseAddress = new Uri(config.ApiBaseUrl) };
});

builder.Services.AddScoped<ITemplateApiClient, TemplateApiClient>();

var host = builder.Build();

// Initialize auth state (restore session from storage)
var authState = host.Services.GetRequiredService<AuthStateService>();
await authState.InitializeAsync().ConfigureAwait(false);

await host.RunAsync().ConfigureAwait(false);
