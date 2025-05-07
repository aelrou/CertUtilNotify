namespace CertUtilNotify.Model
{
	class AppSettingsModel
	{
		public string LogsDirectory { get; set; } = "C:\\Users\\Public\\CertutilNotify\\Logs";
		public int DaysToWarnExpire { get; set; } = 3;
		public int DaysToNotifyNew { get; set; } = 2;
		public string EmailSubject { get; set; } = "MyServer security certificate status";
		public string ToAddressList { get; set; } = "recipient1@address.com,recipient2@address.com";
		public string FromAddress { get; set; } = "sender@address.com";
		public string SMTP_Username { get; set; } = "sender@address.com";
		public string SMTP_Password { get; set; } = "password";
		public string SMTP_Host { get; set; } = "smtp.server.com";
		public int SMTP_Port { get; set; } = 587;
	}
}
