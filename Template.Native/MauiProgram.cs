using Microsoft.Extensions.Logging;
using Template.Native.Services;
using Template.Common.Services.Api;

namespace Template.Native;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Must be public for XAML binding in MAUI projects")]
public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		// Environment and settings
		var environment = new NativeMyEnvironment();
		var settings = new Settings(environment);

		var appConfig = new AppConfiguration
		{
			ApiBaseUrl = settings.ApiUrl,
			IsDevelopment = environment.IsDevelopment()
		};

		builder.Services.AddSingleton<IMyEnvironment>(environment);
		builder.Services.AddSingleton<ISettings>(settings);
		builder.Services.AddSingleton<IAppConfiguration>(appConfig);

		// Storage (MAUI Preferences)
		builder.Services.AddSingleton<IStorageService, NativeStorageService>();

		// Auth state
		builder.Services.AddSingleton<AuthStateService>(sp =>
		{
			var storageService = sp.GetRequiredService<IStorageService>();
			return new AuthStateService(storageService);
		});

		// HTTP client with auth handler
		builder.Services.AddTransient<AuthenticationMessageHandler>();
		builder.Services.AddSingleton(sp =>
		{
			var config = sp.GetRequiredService<IAppConfiguration>();
			var authHandler = sp.GetRequiredService<AuthenticationMessageHandler>();
			authHandler.InnerHandler = new HttpClientHandler();

			return new HttpClient(authHandler) { BaseAddress = config.ApiBaseUrl };
		});

		builder.Services.AddSingleton<ITemplateApiClient, TemplateApiClient>();

		var app = builder.Build();

		// Initialize auth state
		Task.Run(async () =>
		{
			var authState = app.Services.GetRequiredService<AuthStateService>();
			await authState.InitializeAsync().ConfigureAwait(false);
		}).Wait();

		return app;
	}
}
