
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace CertUtilNotify.Method;

class EmailService
{
	internal void SendEmail
		(
			string subject,
			string toAddressList,
			string fromAddress,
			string smtpUsername,
			string smtpPassword,
			string smtpHost,
			int smtpPort,
			string body
		)
	{
		try
		{
			// Disable certificate validation
			ServicePointManager.ServerCertificateValidationCallback = AcceptAllCertificates;

			NetworkCredential credential = new NetworkCredential();
			credential.UserName = smtpUsername;
			credential.Password = smtpPassword;

			MailAddress from = new MailAddress(fromAddress);

			using MailMessage message = new MailMessage();
			message.Subject = subject;
			message.To.Add(toAddressList);
			message.From = from;
			message.IsBodyHtml = false;
			message.Body = body;

			using SmtpClient client = new SmtpClient();
			client.Timeout = 10000; // 10 sec
			client.UseDefaultCredentials = false;
			client.EnableSsl = true;
			client.Credentials = credential;
			client.Host = smtpHost;
			client.Port = smtpPort;
			/*
			Port 465: .NET SmtpClient uses "implicit SSL/TLS" and the SSL handshake must begin immediately before any SMTP command is sent.
			Port 587: .NET SmtpClient uses "explicit SSL (STARTTLS)" by default unless the server supports implicit TLS at connection time.
			In other words, apparently .NET SmtpClient does not support implicit SSL (port 465) correctly, so use port 587 instead.
			*/
			client.Send(message);

			Console.WriteLine("SMTP success.");
		}
		catch (Exception ex)
		{
			Console.WriteLine("SMTP error: " + ex.Message + Program.nl + ex.StackTrace);
		}
		finally
		{
			{
				// Re-enable vertificate validation
				ServicePointManager.ServerCertificateValidationCallback = null;
			}
		}
	}

	bool AcceptAllCertificates
		(
		object sender,
		X509Certificate certificate,
		X509Chain chain,
		SslPolicyErrors sslPolicyErrors
		)
	{
		return true;
	}
}
