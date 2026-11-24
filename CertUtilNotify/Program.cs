using CertUtilNotify.Method;
using CertUtilNotify.Model;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

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
		public static readonly string AppHistoryFile = "apphistory.json";
		public static IConfigurationRoot AppSettings;

		public static string LogsDirectory;
		public static string OpenSSLPath;
		public static List<string> Domains = new List<string>();
		public static int DaysToWarnExpire;
		public static int DaysToNotifyNew;
		public static string EmailSubject;
		public static string ToAddressList;
		public static string SMTP_Host;
		public static int SMTP_Port;
		public static string FromAddress;
		public static string SMTP_Username;
		public static string SMTP_Password;

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
					SMTP_Host = AppSettings.GetSection("SMTP_Host").Value;
					SMTP_Port = int.Parse(AppSettings.GetSection("SMTP_Port").Value);
					FromAddress = AppSettings.GetSection("FromAddress").Value;
					SMTP_Username = AppSettings.GetSection("SMTP_Username").Value;
					SMTP_Password = AppSettings.GetSection("SMTP_Password").Value;
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message + nl + ex.StackTrace);
					Environment.Exit(1);
				}
			}

			StartLog();

			HistorySession session = new HistorySession();
			session.StartedAt = DateTime.UtcNow;

			//Environment.Exit(0);

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

			Console.WriteLine(string.Concat("Populating Dictionary<HistoryListModel>"));
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

			bool notify = false;
			DateTime now = DateTime.UtcNow;
			string message = EmailSubject + " " + now.ToString(DateFormat) + "." + nl + nl;
			foreach (CertUtilModel c in certUtilSet)
			{
				AppendCertificateNotice(ref message, ref notify, now, c.Subject2_CN, c.NotAfter, c.Thumbprint, HistoryAction.Skipped);
			}
			foreach (OpenSslModel c in openSslSet)
			{
				HistoryAction previous = LatestDomainAction(historyDocument, c.Domain);
				HistoryModel history = new HistoryModel();
				history.Domain = c.Domain;
				history.Expiration = c.NotAfter;
				history.Action = AppendCertificateNotice(ref message, ref notify, now, c.NoticeName(), c.NotAfter, c.Thumbprint, previous);
				session.Entries.Add(history);
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

			//foreach (KeyValuePair<string, HistorySession> entry in historyDocument.HistoryList)
			//{
			//	if (entry.Value == null)
			//	{
			//		continue;
			//	}
			//	foreach (HistoryModel item in entry.Value.Entries)
			//	{
			//		Console.WriteLine(entry.Key);
			//		Console.WriteLine(entry.Value.StartedAt.ToString(DateTimeFormat));
			//		Console.WriteLine(item.Domain);
			//		Console.WriteLine(item.Expiration.ToString(DateTimeFormat));
			//		Console.WriteLine(item.Action.ToString());
			//	}
			//}

			if (notify)
			{
				Console.Write(message);
				EmailService emailService = new EmailService();
				emailService.SendEmail(
					subject: EmailSubject + " " + now.ToString(DateFormat),
					toAddressList: ToAddressList,
					smtpHost: SMTP_Host,
					smtpPort: SMTP_Port,
					fromAddress: FromAddress,
					smtpUsername: SMTP_Username,
					smtpPassword: SMTP_Password,
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

		static HistoryAction LatestDomainAction(HistoryListModel history, string? domain)
		{
			if (string.IsNullOrWhiteSpace(domain) || history.HistoryList == null)
			{
				return HistoryAction.Skipped;
			}

			bool found = false;
			DateTime newestStarted = DateTime.MinValue;
			string newestKey = "";
			HistoryAction newestAction = HistoryAction.Skipped;
			foreach (KeyValuePair<string, HistorySession> entry in history.HistoryList)
			{
				if (entry.Value == null || entry.Value.Entries == null)
				{
					continue;
				}

				HistoryAction? entryAction = null;
				foreach (HistoryModel item in entry.Value.Entries)
				{
					if (string.Equals(item.Domain, domain, StringComparison.OrdinalIgnoreCase))
					{
						entryAction = item.Action;
					}
				}
				if (entryAction == null)
				{
					continue;
				}

				DateTime started = entry.Value.StartedAt;
				if (!found || started > newestStarted || (started == newestStarted && string.CompareOrdinal(entry.Key, newestKey) > 0))
				{
					found = true;
					newestStarted = started;
					newestKey = entry.Key;
					newestAction = entryAction.Value;
				}
			}
			return newestAction;
		}

		static HistoryAction AppendCertificateNotice(
			ref string message,
			ref bool notify,
			DateTime now,
			string? name,
			DateTime notAfter,
			string? thumbprint,
			HistoryAction previousAction)
		{
			if (notAfter < now.AddDays(DaysToWarnExpire) && notAfter > now)
			{
				notify = true;
				message = message + "-------- Expiring --------" + nl;
				message = message + name + " expires on " + notAfter.ToString(DateFormat) + "." + nl;
				message = message + (notAfter - now).TotalDays.ToString("0") + " days remaining." + nl;
				message = message + "Thumbprint " + thumbprint + nl;
				message = message + nl;
				return HistoryAction.ExpirationNotice;
			}

			// The previous run warned that this domain was inside the window. It has since moved past that window.
			if (previousAction == HistoryAction.ExpirationNotice && notAfter >= now.AddDays(DaysToWarnExpire))
			{
				notify = true;
				message = message + "-------- Renewed --------" + nl;
				message = message + name + " was renewed and expires on " + notAfter.ToString(DateFormat) + "." + nl;
				message = message + (notAfter - now).TotalDays.ToString("0") + " days remaining." + nl;
				message = message + "Thumbprint " + thumbprint + nl;
				message = message + nl;
				return HistoryAction.RenewalNotice;
			}

			return HistoryAction.Skipped;
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
