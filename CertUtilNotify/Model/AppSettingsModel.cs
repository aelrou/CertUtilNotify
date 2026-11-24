namespace CertUtilNotify.Model
{
	class AppSettingsModel
	{
		public string LogsDirectory { get; set; } = "C:\\Users\\Public\\CertutilNotify\\Logs";
		
		public string OpenSSLPath { get; set; } = "C:\\Program Files\\OpenSSL-Win64\\bin\\openssl.exe";
		public List<string> Domains { get; set; } = new List<string> { "www.google.com", "www.cloudflare.com" };
		public int DaysToWarnExpire { get; set; } = 3;
		public int DaysToNotifyNew { get; set; } = 2;
		public string EmailSubject { get; set; } = "MyServer security certificate status";
		public string ToAddressList { get; set; } = "recipient1@address.com,recipient2@address.com";
		public string SMTP_Host { get; set; } = "smtp.server.com";
		public int SMTP_Port { get; set; } = 587;
		public string FromAddress { get; set; } = "sender@address.com";
		public string SMTP_Username { get; set; } = "sender@address.com";
		public string SMTP_Password { get; set; } = "password";
	}
}
