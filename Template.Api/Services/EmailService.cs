using System.Net;
using System.Net.Mail;

namespace Template.Api.Services;

internal interface IEmailService
{
    Task<bool> SendVerificationEmailAsync(string toEmail, string code);
    Task<bool> SendFeedbackNotificationAsync(string subject, string body);
}

internal sealed class EmailService(string smtpServer, int smtpPort, string username, string password, string senderEmail, string senderName, string receiverEmail) : IEmailService
{
    private readonly string _smtpServer = smtpServer;
    private readonly int _smtpPort = smtpPort;
    private readonly string _username = username;
    private readonly string _password = password;
    private readonly string _receiverEmail = receiverEmail;
    private readonly string _senderEmail = senderEmail;
    private readonly string _senderName = senderName;

    public async Task<bool> SendVerificationEmailAsync(string toEmail, string code)
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
            return true;
        }
#pragma warning disable CA1031 // We must catch every failure mode (SMTP, auth, network, etc.) so callers never mistake a failed send for a successful one
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send code via email to {toEmail}: {ex.Message}");
            return false;
        }
#pragma warning restore CA1031
    }

    public async Task<bool> SendFeedbackNotificationAsync(string subject, string body)
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
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(_receiverEmail);

            await client.SendMailAsync(message).ConfigureAwait(false);
            return true;
        }
#pragma warning disable CA1031 // We must catch every failure mode (SMTP, auth, network, etc.) so callers never mistake a failed send for a successful one
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send feedback notification email: {ex.Message}");
            return false;
        }
#pragma warning restore CA1031
    }
}
