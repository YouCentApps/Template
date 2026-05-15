using System.Net;
using System.Net.Mail;

namespace Template.Api.Services;

internal interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string code);
}

internal sealed class EmailService(string smtpServer, int smtpPort, string username, string password, string senderEmail, string senderName) : IEmailService
{
    private readonly string _smtpServer = smtpServer;
    private readonly int _smtpPort = smtpPort;
    private readonly string _username = username;
    private readonly string _password = password;
    private readonly string _senderEmail = senderEmail;
    private readonly string _senderName = senderName;

    public async Task SendVerificationEmailAsync(string toEmail, string code)
    {
        try
        {
            using var client = new SmtpClient(_smtpServer, _smtpPort)
            {
                Credentials = new NetworkCredential(_username, _password),
                EnableSsl = true
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_senderEmail, _senderName),
                Subject = "Your Verification Code",
                Body = $"Your verification code is: {code}\n\nThis code expires in 10 minutes.",
                IsBodyHtml = false
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message).ConfigureAwait(false);
        }
        catch (SmtpException ex)
        {
            Console.WriteLine($"Failed to send code via email to {toEmail}: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Failed to send code via email to {toEmail}: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Invalid email address '{toEmail}': {ex.Message}");
        }
    }
}
