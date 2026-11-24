using System.Globalization;
using System.Text.Json;

namespace CertUtilNotify.Model
{
	public class HistoryListModel
	{
		public const int RetainSessions = 10;

		public Dictionary<string, HistorySession> HistoryList { get; set; } = new Dictionary<string, HistorySession>();

		public static HistoryListModel Load(string path)
		{
			if (!File.Exists(path))
			{
				return new HistoryListModel();
			}

			string json = File.ReadAllText(path);
			HistoryListModel? document = JsonSerializer.Deserialize<HistoryListModel>(json, CreateJsonOptions());
			if (document == null || document.HistoryList == null)
			{
				return new HistoryListModel();
			}

			foreach (KeyValuePair<string, HistorySession> entry in document.HistoryList)
			{
				if (entry.Value != null && entry.Value.Entries == null)
				{
					entry.Value.Entries = new List<HistoryModel>();
				}
			}
			return document;
		}

		public void Save(string path)
		{
			string json = JsonSerializer.Serialize(this, CreateJsonOptions());
			File.WriteAllText(path, json);
		}

		public void Add(HistorySession session)
		{
			if (HistoryList == null)
			{
				HistoryList = new Dictionary<string, HistorySession>();
			}
			if (session.Entries == null)
			{
				session.Entries = new List<HistoryModel>();
			}
			if (session.StartedAt.Kind != DateTimeKind.Utc)
			{
				session.StartedAt = session.StartedAt.ToUniversalTime();
			}

			string stem = session.StartedAt.ToString(Program.DateTimeFilenameFormat, CultureInfo.InvariantCulture);
			string key = stem;
			int collision = 0;
			// A second session in the same UTC second is stem_2, then stem_3.
			while (HistoryList.ContainsKey(key))
			{
				collision++;
				key = stem + "_" + (collision + 1).ToString(CultureInfo.InvariantCulture);
			}
			HistoryList.Add(key, session);
			Trim();
		}

		public void Trim()
		{
			if (HistoryList == null)
			{
				HistoryList = new Dictionary<string, HistorySession>();
				return;
			}

			while (HistoryList.Count > RetainSessions)
			{
				string oldestKey = "";
				DateTime oldestStarted = DateTime.MaxValue;
				bool found = false;
				foreach (KeyValuePair<string, HistorySession> entry in HistoryList)
				{
					DateTime started = entry.Value == null ? DateTime.MinValue : entry.Value.StartedAt;
					if (!found || started < oldestStarted || (started == oldestStarted && string.CompareOrdinal(entry.Key, oldestKey) < 0))
					{
						found = true;
						oldestKey = entry.Key;
						oldestStarted = started;
					}
				}
				if (!found)
				{
					break;
				}
				HistoryList.Remove(oldestKey);
			}
		}

		static JsonSerializerOptions CreateJsonOptions()
		{
			JsonSerializerOptions options = new JsonSerializerOptions();
			options.WriteIndented = true;
			return options;
		}
	}
}
