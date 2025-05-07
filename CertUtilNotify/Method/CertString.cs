using System.Text;
namespace CertutilNotify.Method;

static class CertString
{
	internal static List<string> Eval(string cert)
	{
		List<string> attributes = new List<string>();
		StringBuilder current = new StringBuilder();
		bool inQuotes = false;
		bool escapeNext = false;

		foreach (char c in cert)
		{
			if (escapeNext)
			{
				current.Append(c);
				escapeNext = false;
			}
			else if (c == '\\')
			{
				escapeNext = true; // Next character is escaped
			}
			else if (c == '"')
			{
				inQuotes = !inQuotes; // Toggle quote state
				current.Append(c);
			}
			else if (c == ',' && !inQuotes)
			{
				attributes.Add(current.ToString().Trim());
				current.Clear();
			}
			else
			{
				current.Append(c);
			}
		}

		if (current.Length > 0)
		{
			attributes.Add(current.ToString().Trim());
		}

		return attributes;
	}
}
