using CertUtilNotify.Method;
using CertUtilNotify.Model;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using Windows.Media.Capture;

namespace CertUtilNotify
{
    static class Program
    {
		public static readonly string nl = Environment.NewLine;
		public static readonly string DateTimeFilenameFormat = "yyyy-MM-dd_HHmmss";
		public static readonly string DateFormat = "yyyy-MM-dd";
		public static readonly string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
		public static readonly string DateTimeOpenSslFormat = "MMM d HH:mm:ss yyyy 'GMT'";

		public static readonly string AppSettingsFile = "appsettings.json";
		public static readonly string AppHistoryFile = "AppHistory.json";
		public static IConfigurationRoot AppSettings;

		public static string LogsDirectory;
		public static string OpenSSLPath;
		public static List<string> Domains = new List<string>();
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
					OpenSSLPath = AppSettings.GetSection("OpenSSLPath").Value;
					Domains = new List<string>();
					foreach (IConfigurationSection domain in AppSettings.GetSection("Domains").GetChildren())
					{
						if (!string.IsNullOrWhiteSpace(domain.Value))
						{
							Domains.Add(domain.Value.Trim());
						}
					}
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

			HistorySession session = new HistorySession();
			session.StartedAt = DateTime.UtcNow;

			HistoryModel history = new HistoryModel();
			history.Domain = "www.google.com";
			history.Expiration = DateTime.UtcNow.AddDays(50);
			history.Action = HistoryAction.Skipped;
			session.Entries.Add(history);

			HistoryModel history2 = new HistoryModel();
			history2.Domain = "www.cloudflare.com";
			history2.Expiration = DateTime.UtcNow.AddDays(60);
			history2.Action = HistoryAction.Skipped;
			session.Entries.Add(history2);

			HistoryModel history3 = new HistoryModel();
			history3.Domain = "www.microsoft.com";
			history3.Expiration = DateTime.UtcNow.AddDays(70);
			history3.Action = HistoryAction.Skipped;
			session.Entries.Add(history3);

			HistoryListModel historyDocument;
			bool saveHistory = true;
			try
			{
				historyDocument = HistoryListModel.Load(AppHistoryFile);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message + nl + ex.StackTrace);
				historyDocument = new HistoryListModel();
				saveHistory = false;
			}

			historyDocument.Add(session);
			if (saveHistory)
			{
				try
				{
					historyDocument.Save(AppHistoryFile);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message + nl + ex.StackTrace);
				}
			}

			foreach (KeyValuePair<string, HistorySession> entry in historyDocument.HistoryList)
			{
				if (entry.Value == null)
				{
					continue;
				}
				foreach (HistoryModel item in entry.Value.Entries)
				{
					Console.WriteLine(entry.Key);
					Console.WriteLine(entry.Value.StartedAt.ToString(DateTimeFormat));
					Console.WriteLine(item.Domain);
					Console.WriteLine(item.Expiration.ToString(DateTimeFormat));
					Console.WriteLine(item.Action.ToString());
				}
			}

			Environment.Exit(0); // Remove this line to enable the program to run. It is here to prevent accidental email sending during testing.

			StartLog();

			Console.WriteLine(string.Concat("Populating HashSet<CertUtilModel>"));
			HashSet <CertUtilModel> certUtilSet = new HashSet<CertUtilModel>();
			certUtilSet = CertUtil.Query("My");

			Console.WriteLine(string.Concat("Populating HashSet<OpenSslModel>"));
			HashSet<OpenSslModel> openSslSet = new HashSet<OpenSslModel>();
			foreach (string domain in Domains)
			{
				Console.WriteLine("Query " + domain);
				foreach (OpenSslModel remoteCert in OpenSslUtil.Query(domain))
				{
					openSslSet.Add(remoteCert);
				}
			}

			bool notify = false;
			DateTime now = DateTime.UtcNow;
			string message = EmailSubject + " " + now.ToString(DateFormat) + "." + nl + nl;
			foreach (CertUtilModel c in certUtilSet)
			{
				AppendCertificateNotice(ref message, ref notify, now, c.Subject2_CN, c.NotBefore, c.NotAfter, c.Thumbprint);
			}
			foreach (OpenSslModel c in openSslSet)
			{
				AppendCertificateNotice(ref message, ref notify, now, c.NoticeName(), c.NotBefore, c.NotAfter, c.Thumbprint);
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

		static void StartLog()
		{
			try
			{
				if (string.IsNullOrWhiteSpace(LogsDirectory))
				{
					Console.WriteLine("LogsDirectory is not set. Logging to the console only.");
					return;
				}

				Directory.CreateDirectory(LogsDirectory);
				string logFile = Path.Combine(LogsDirectory, DateTime.Now.ToString(DateTimeFilenameFormat) + ".log");
				StreamWriter file = new StreamWriter(logFile, append: false);
				file.AutoFlush = true;
				Console.SetOut(new TeeTextWriter(Console.Out, file));
				Console.WriteLine("Log file " + logFile);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Could not open the log file: " + ex.Message + nl + ex.StackTrace);
			}
		}

		static void AppendCertificateNotice(
			ref string message,
			ref bool notify,
			DateTime now,
			string? name,
			DateTime notBefore,
			DateTime notAfter,
			string? thumbprint)
		{
			if (notAfter < now.AddDays(DaysToWarnExpire) && notAfter > now)
			{
				notify = true;
				message = message + "-------- Expiring --------" + nl;
				message = message + name + " expires on " + notAfter.ToString(DateFormat) + "." + nl;
				message = message + (notAfter - now).TotalDays.ToString("0") + " days remaining." + nl;
				message = message + "Thumbprint " + thumbprint + nl;
				message = message + nl;
			}

			if (notBefore > now.AddDays(DaysToNotifyNew * -1) && notBefore < now)
			{
				message = message + "-------- New --------" + nl;
				message = message + name + " issued on " + notBefore.ToString(DateFormat) + "." + nl;
				message = message + (notAfter - now).TotalDays.ToString("0") + " days remaining." + nl;
				message = message + "Thumbprint " + thumbprint + nl;
				message = message + nl;
			}
		}

		sealed class TeeTextWriter : TextWriter
		{
			readonly TextWriter console;
			readonly TextWriter file;

			internal TeeTextWriter(TextWriter console, TextWriter file)
			{
				this.console = console;
				this.file = file;
			}

			public override Encoding Encoding
			{
				get { return console.Encoding; }
			}

			public override void Write(char value)
			{
				console.Write(value);
				file.Write(value);
			}

			public override void Write(string? value)
			{
				console.Write(value);
				file.Write(value);
			}

			public override void Write(char[] buffer, int index, int count)
			{
				console.Write(buffer, index, count);
				file.Write(buffer, index, count);
			}

			public override void WriteLine(string? value)
			{
				console.WriteLine(value);
				file.WriteLine(value);
			}

			public override void Flush()
			{
				console.Flush();
				file.Flush();
			}
		}
	}
}
