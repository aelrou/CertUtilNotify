using System.Text.Json.Serialization;

namespace CertUtilNotify.Model
{
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public enum HistoryAction
	{
		ExpirationNotice,
		RenewalNotice,
		Skipped
	}

	public class HistoryModel
	{
		public string Domain { get; set; } = "";
		public DateTime Expiration { get; set; }
		public HistoryAction Action { get; set; }
	}
}
