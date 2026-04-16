using System.Net;
using System.Net.Mail;

namespace Template.Api.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string code);
}

public class EmailService : IEmailService
{
    private readonly string _smtpServer;
    private readonly int _smtpPort;
    private readonly string _username;
    private readonly string _password;
    private readonly string _senderEmail;
    private readonly string _senderName;

    public EmailService(string smtpServer, int smtpPort, string username, string password, string senderEmail, string senderName)
    {
        _smtpServer = smtpServer;
        _smtpPort = smtpPort;
        _username = username;
        _password = password;
        _senderEmail = senderEmail;
        _senderName = senderName;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string code)
    {
        try
        {
            using var client = new SmtpClient(_smtpServer, _smtpPort)
            {
                Credentials = new NetworkCredential(_username, _password),
                EnableSsl = true
            };

            var message = new MailMessage
            {
                From = new MailAddress(_senderEmail, _senderName),
                Subject = "Your Verification Code",
                Body = $"Your verification code is: {code}\n\nThis code expires in 10 minutes.",
                IsBodyHtml = false
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send email to {toEmail}: {ex.Message}");
        }
    }
}
