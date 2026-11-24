
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace CertUtilNotify.Method;

class EmailService
{
	internal void SendEmail
		(
			string subject,
			string toAddressList,
			string smtpHost,
			int smtpPort,
			string fromAddress,
			string smtpUsername,
			string smtpPassword,
			string body
		)
	{
		try
		{
			using MimeMessage message = new MimeMessage();
			message.From.Add(MailboxAddress.Parse(fromAddress));
			message.To.AddRange(InternetAddressList.Parse(toAddressList));
			message.Subject = subject;
			TextPart text = new TextPart("plain");
			text.Text = body;
			message.Body = text;

			using SmtpClient client = new SmtpClient();
			client.Timeout = 10000; // 10 sec
			// Port 465 is implicit TLS. Port 587 and the other submission ports use STARTTLS.
			SecureSocketOptions options = smtpPort == 465
				? SecureSocketOptions.SslOnConnect
				: SecureSocketOptions.StartTls;
			client.Connect(smtpHost, smtpPort, options);
			client.Authenticate(smtpUsername, smtpPassword);
			client.Send(message);
			client.Disconnect(true);

			Console.WriteLine("SMTP success.");
		}
		catch (Exception ex)
		{
			Console.WriteLine("SMTP error: " + ex.Message + Program.nl + ex.StackTrace);
		}
	}
}
