namespace CertUtilNotify.Model
{
	public class HistorySession
	{
		public DateTime StartedAt { get; set; }
		public List<HistoryModel> Entries { get; set; } = new List<HistoryModel>();
	}
}
