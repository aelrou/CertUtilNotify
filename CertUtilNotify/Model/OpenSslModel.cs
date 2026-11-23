using System.Globalization;

namespace CertUtilNotify.Model
{
	class OpenSslModel
	{
		// openssl x509 -noout prints one certificate, for example:
		// notBefore=Nov  3 08:29:37 2026 GMT
		// notAfter=Jan 26 08:29:36 2027 GMT
		// sha1 Fingerprint=DD:EA:EE:BA:71:C4:96:AE:6C:BC:EA:43:BC:34:AB:64:AF:13:3D:AF
		// subject=CN=www.google.com
		internal HashSet<OpenSslModel> ParseAllLines(string[] lines)
		{
			HashSet<OpenSslModel> openSslTextSet = new HashSet<OpenSslModel>();
			if (lines.Length == 0)
			{
				return openSslTextSet;
			}

			OpenSslModel cert = ParseLines(lines);
			if (cert.NotBefore != default || cert.NotAfter != default || cert.Thumbprint != null || cert.Subject2_CN != null)
			{
				openSslTextSet.Add(cert);
			}
			return openSslTextSet;
		}

		OpenSslModel ParseLines(string[] lines)
		{
			OpenSslModel cert = new OpenSslModel();
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				int split = line.IndexOf('=');
				if (split < 0)
				{
					continue;
				}

				string label = line.Substring(0, split).Trim();
				string value = line.Substring(split + 1).Trim();
				if (label.Equals("notBefore", StringComparison.OrdinalIgnoreCase))
				{
					cert.NotBefore = ParseGmt(value);
				}
				else if (label.Equals("notAfter", StringComparison.OrdinalIgnoreCase))
				{
					cert.NotAfter = ParseGmt(value);
				}
				else if (label.EndsWith("Fingerprint", StringComparison.OrdinalIgnoreCase))
				{
					cert.Thumbprint = value;
				}
				else if (label.Equals("subject", StringComparison.OrdinalIgnoreCase))
				{
					cert.Subject2_CN = ReadCommonName(value);
				}
			}
			return cert;
		}

		static DateTime ParseGmt(string value)
		{
			string[] parts = value.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			string collapsed = string.Join(" ", parts);
			return DateTime.ParseExact(
				collapsed,
				Program.DateTimeOpenSslFormat,
				CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
			);
		}

		static string? ReadCommonName(string subject)
		{
			string[] parts = subject.Split(new char[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string part in parts)
			{
				string attribute = part.Trim();
				if (attribute.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
				{
					return attribute.Substring(3).Trim();
				}
				if (attribute.StartsWith("CN =", StringComparison.OrdinalIgnoreCase))
				{
					return attribute.Substring(4).Trim();
				}
			}
			return null;
		}

		internal string? NoticeName()
		{
			if (string.IsNullOrWhiteSpace(Domain))
			{
				return Subject2_CN;
			}
			if (string.IsNullOrWhiteSpace(Subject2_CN) || Subject2_CN.Equals(Domain, StringComparison.OrdinalIgnoreCase))
			{
				return Domain;
			}
			return Domain + " (" + Subject2_CN + ")";
		}

		internal string? Domain;
		internal string? Subject2_CN;
		internal DateTime NotBefore;
		internal DateTime NotAfter;
		internal string? Thumbprint;
	}
}
