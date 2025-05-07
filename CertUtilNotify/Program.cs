using CertUtilNotify.Method;
using CertUtilNotify.Model;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace CertUtilNotify
{
    static class Program
    {
		public static readonly string nl = Environment.NewLine;
		public static readonly string DateTimeFilenameFormat = "yyyy-MM-dd_HHmmss";
		public static readonly string DateFormat = "yyyy-MM-dd";
		public static readonly string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

		public static readonly string AppSettingsFile = "appsettings.json";
		public static IConfigurationRoot AppSettings;

		public static string LogsDirectory;
		public static int DaysToWarnExpire;
		public static int DaysToNotifyNew;
		public static string EmailSubject;
		public static string ToAddressList;
		public static string FromAddress;
		public static string SMTP_Username;
		public static string SMTP_Password;
		public static string SMTP_Host;
		public static int SMTP_Port;

		[STAThread]
		static void Main()
		{
			if (!File.Exists(AppSettingsFile))
			{
				try
				{
					Console.WriteLine("Create default " + AppSettingsFile);
					AppSettingsModel appSettingsDefault = new AppSettingsModel();
					JsonSerializerOptions options = new JsonSerializerOptions();
					options.WriteIndented = true;
					string json = JsonSerializer.Serialize(appSettingsDefault, options);
					File.WriteAllText(AppSettingsFile, json);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message + nl + ex.StackTrace);
					Environment.Exit(1);
				}
			}

			if (File.Exists(AppSettingsFile))
			{
				try
				{
					Console.WriteLine("Read " + AppSettingsFile);
					ConfigurationBuilder appSettingsJson = new ConfigurationBuilder();
					appSettingsJson.AddJsonFile(AppSettingsFile, optional: false, reloadOnChange: true);
					AppSettings = appSettingsJson.Build();
					LogsDirectory = AppSettings.GetSection("LogsDirectory").Value;
					DaysToWarnExpire = int.Parse(AppSettings.GetSection("DaysToWarnExpire").Value);
					DaysToNotifyNew = int.Parse(AppSettings.GetSection("DaysToNotifyNew").Value);
					EmailSubject = AppSettings.GetSection("EmailSubject").Value;
					ToAddressList = AppSettings.GetSection("ToAddressList").Value;
					FromAddress = AppSettings.GetSection("FromAddress").Value;
					SMTP_Username = AppSettings.GetSection("SMTP_Username").Value;
					SMTP_Password = AppSettings.GetSection("SMTP_Password").Value;
					SMTP_Host = AppSettings.GetSection("SMTP_Host").Value;
					SMTP_Port = int.Parse(AppSettings.GetSection("SMTP_Port").Value);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message + nl + ex.StackTrace);
					Environment.Exit(1);
				}
			}

			Console.WriteLine(string.Concat("Populating HashSet<CertUtilModel>"));
			HashSet <CertUtilModel> certUtilSet = new HashSet<CertUtilModel>();
			certUtilSet = CertUtil.Query("My");

			bool notify = false;
			DateTime now = DateTime.UtcNow;
			string message = EmailSubject + " " + now.ToString(DateFormat) + "." + nl + nl;
			foreach (CertUtilModel c in certUtilSet)
			{
				if (c.NotAfter < now.AddDays(DaysToWarnExpire) && c.NotAfter > now)
				{
					notify = true;
					message = message + "-------- Expiring --------" + nl;
					message = message + c.Subject2_CN + " expires on " + c.NotAfter.ToString(DateFormat) + "." + nl;
					message = message + (c.NotAfter - now).TotalDays.ToString("0") + " days remaining." + nl;
					message = message + "Thumbprint " + c.Thumbprint + nl;
					message = message + nl;
				}

				if (c.NotBefore > now.AddDays(DaysToNotifyNew*-1) && c.NotBefore < now)
				{
					message = message + "-------- New --------" + nl;
					message = message + c.Subject2_CN + " issued on " + c.NotBefore.ToString(DateFormat) + "." + nl;
					message = message + (c.NotAfter - now).TotalDays.ToString("0") + " days remaining." + nl;
					message = message + "Thumbprint " + c.Thumbprint + nl;
					message = message + nl;
				}
			}

			if (notify)
			{
				Console.Write(message);
				EmailService emailService = new EmailService();
				emailService.SendEmail(
					subject: EmailSubject + " " + now.ToString(DateFormat),
					toAddressList: ToAddressList,
					fromAddress: FromAddress,
					smtpUsername: SMTP_Username,
					smtpPassword: SMTP_Password,
					smtpHost: SMTP_Host,
					smtpPort: SMTP_Port,
					body: message
				);
			}
		}
	}
}
