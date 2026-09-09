using Template.Common.Services.Data;
using Template.Common.Services.Auth;
using Template.Api.Endpoints;
using Template.Api.Services;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();

// Configure CORS
builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("AllowClients", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    }
    else
    {
        options.AddPolicy("AllowClients", policy =>
        {
            policy.WithOrigins(
                "https://yourdomain.com",
                "https://www.yourdomain.com"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        });
    }
});

// Register Azure Storage factory
var storageUri = new Uri(builder.Configuration["AzureStorage:StorageUri"]!);
var accountName = builder.Configuration["AzureStorage:AccountName"];
var accountKey = builder.Configuration["AzureStorage:AccountKey"];

if (string.IsNullOrWhiteSpace(storageUri.AbsoluteUri)
    || string.IsNullOrWhiteSpace(accountName)
    || string.IsNullOrWhiteSpace(accountKey))
{

    //Console.ForegroundColor = ConsoleColor.Yellow;
    //Console.WriteLine("WARNING: AzureStorage not configured. See appsettings.json.");
    //Console.WriteLine("Auth/profile/admin endpoints will fail until storage is configured.");
    //Console.ResetColor();

    // Register a factory that throws helpful errors at runtime
    builder.Services.AddSingleton<ITableClientFactory>(sp =>
        throw new InvalidOperationException("AzureStorage is not configured. Set StorageUri, AccountName, and AccountKey in appropriate configuration."));
}
else
{
    builder.Services.AddSingleton<ITableClientFactory>(
        new TableClientFactory(storageUri, accountName, accountKey));
}

// Register Email Service
var smtpServer = builder.Configuration["EmailSettings:SmtpServer"];
var smtpPort = builder.Configuration.GetValue<int>("EmailSettings:SmtpPort", 587);
var smtpUsername = builder.Configuration["EmailSettings:Username"];
var smtpPassword = builder.Configuration["EmailSettings:Password"];
var senderEmail = builder.Configuration["EmailSettings:SenderEmail"];
var senderName = builder.Configuration["EmailSettings:SenderName"] ?? "Template App";
var receiverEmail = builder.Configuration["EmailSettings:ReceiverEmail"];

if (!string.IsNullOrWhiteSpace(smtpServer) && !string.IsNullOrWhiteSpace(smtpUsername) && !string.IsNullOrWhiteSpace(smtpPassword))
{
    builder.Services.AddScoped<IEmailService>(sp =>
        new EmailService(smtpServer, smtpPort, smtpUsername, smtpPassword, senderEmail ?? smtpUsername, senderName, receiverEmail ?? senderEmail ?? smtpUsername));
    //Console.WriteLine("Email service configured");
}
else
{
    //Console.WriteLine("Email service not configured");
}

// Register repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();

// Register authentication services with email callback
builder.Services.AddScoped<IAuthenticationService>(sp =>
{
    var tableClientFactory = sp.GetRequiredService<ITableClientFactory>();
    var userRepository = sp.GetRequiredService<IUserRepository>();
    var emailService = sp.GetService<IEmailService>();

    Action<string, string>? emailCallback = null;
    if (emailService != null)
    {
        emailCallback = (email, code) =>
        {
            _ = emailService.SendVerificationEmailAsync(email, code);
        };
    }
    else
    {
        emailCallback = (email, code) =>
        {
            Console.WriteLine($"[OTP] Email: {email}, Code: {code}");
        };
    }

    return new AuthenticationService(tableClientFactory, userRepository, emailCallback);
});

builder.Services.AddScoped<IRegistrationService>(sp =>
{
    var tableClientFactory = sp.GetRequiredService<ITableClientFactory>();
    var userRepository = sp.GetRequiredService<IUserRepository>();
    var emailService = sp.GetService<IEmailService>();

    Action<string, string>? emailCallback = null;
    if (emailService != null)
    {
        emailCallback = (email, code) =>
        {
            _ = emailService.SendVerificationEmailAsync(email, code);
        };
    }
    else
    {
        emailCallback = (email, code) =>
        {
            Console.WriteLine($"[OTP] Email: {email}, Code: {code}");
        };
    }

    return new RegistrationService(tableClientFactory, userRepository, emailCallback);
});

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "An unexpected error occurred. Please try again later."
        }).ConfigureAwait(false);
    });
});

app.UseHttpsRedirection();
app.UseCors("AllowClients");

// Health check
app.MapGet("/api/health", () => Results.Ok(
    new 
    {
        status = "healthy",
        timestamp = DateTime.UtcNow,
        environment = app.Environment.EnvironmentName
    })
).WithName("HealthCheck");

// Map API endpoints
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapAdminEndpoints();

app.Run();
