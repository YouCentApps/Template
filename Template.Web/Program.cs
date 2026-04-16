using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Template.Shared.Services.Api;
using Template.Shared.Services.State;
using Template.Shared.Configuration;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<Template.Shared.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure app settings
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7224";
var isDevelopment = builder.HostEnvironment.IsDevelopment();

var appConfig = new AppConfiguration
{
    ApiBaseUrl = apiBaseUrl,
    IsDevelopment = isDevelopment
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

    return new HttpClient(authHandler) { BaseAddress = new Uri(config.ApiBaseUrl) };
});

builder.Services.AddScoped<ITemplateApiClient, TemplateApiClient>();

var host = builder.Build();

// Initialize auth state (restore session from storage)
var authState = host.Services.GetRequiredService<AuthStateService>();
await authState.InitializeAsync();

await host.RunAsync();
