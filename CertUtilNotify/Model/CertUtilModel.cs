using CertutilNotify.Method;

namespace CertUtilNotify.Model
{
	class CertUtilModel
	{
		internal HashSet<CertUtilModel> ParseAllLines(string[] lines)
		{
			int certUtilTextStartLine = -1;
			List<string> certUtilTextLines = new List<string>();
			HashSet<CertUtilModel> certUtilTextSet = new HashSet<CertUtilModel>();
			for (int i = 0; i < lines.Length; i++)
			{
				if (lines[i].Trim().StartsWith("========") && lines[i].Trim().EndsWith("========"))
				{
					if (certUtilTextStartLine > -1) // Don't try to parse until after finding the first certificate
					{
						certUtilTextSet.Add(ParseLines(certUtilTextLines));
					}
					certUtilTextStartLine = i;
					certUtilTextLines.Clear();
				}
				certUtilTextLines.Add(lines[i]);
			}

			if (certUtilTextStartLine > -1)
			{
				certUtilTextSet.Add(ParseLines(certUtilTextLines)); // Parse the last cert after the final loop
			}
			return certUtilTextSet;
		}

		CertUtilModel ParseLines(List<string> lines)
		{
			int certTextLine = -1;
			CertUtilModel cert = new CertUtilModel();
			for (int i = 0; i < lines.Count; i++)
			{
				if (lines[i].Trim().StartsWith("====") && lines[i].Trim().EndsWith("===="))
				{
					certTextLine = i;
					cert.Number = int.Parse(lines[i].Replace("=", "").Replace("Certificate", "").Replace(" ", ""));
				}

				if (i > certTextLine && certTextLine > -1)
				{
					switch (lines[i])
					{
						case string e when e.Trim().StartsWith("Serial Number:"):
							cert.Serial = lines[i].Replace("Serial Number:", "").Trim();
							break;
						case string e when e.Trim().StartsWith("Issuer:"):
							
							List<string> issuerList = CertString.Eval(lines[i].Replace("Issuer:", "").Trim());
							foreach (string a in issuerList)
							{
								switch (a)
								{
									case string l when l.Trim().StartsWith("E="):
										cert.Issuer1_E = a.Replace("E=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("CN="):
										cert.Issuer2_CN = a.Replace("CN=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("OU="):
										cert.Issuer3_OU = a.Replace("OU=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("O="):
										cert.Issuer4_O = a.Replace("O=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("L="):
										cert.Issuer5_L = a.Replace("L=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("S="):
										cert.Issuer6_S = a.Replace("S=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("C="):
										cert.Issuer7_C = a.Replace("C=", "").Trim();
										break;
									default:
										if (a.Trim().Equals("")) { }
										else
										{
											cert.ExtraData.Add(string.Concat("UnknownIssuer", i, ": ", a.Trim()));
										}
										break;
								}
							}

							break;
						case string e when e.Trim().StartsWith("NotBefore:"):
							cert.NotBefore = DateTime.Parse(lines[i].Replace("NotBefore:", "").Trim()).ToUniversalTime();
							break;
						case string e when e.Trim().StartsWith("NotAfter:"):
							cert.NotAfter = DateTime.Parse(lines[i].Replace("NotAfter:", "").Trim()).ToUniversalTime();
							break;
						case string e when e.Trim().StartsWith("Subject:"):

							List<string> subjectList = CertString.Eval(lines[i].Replace("Subject:", "").Trim());
							foreach (string a in subjectList)
							{
								switch (a)
								{
									case string l when l.Trim().StartsWith("E="):
										cert.Subject1_E = a.Replace("E=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("CN="):
										cert.Subject2_CN = a.Replace("CN=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("OU="):
										cert.Subject3_OU = a.Replace("OU=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("O="):
										cert.Subject4_O = a.Replace("O=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("L="):
										cert.Subject5_L = a.Replace("L=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("S="):
										cert.Subject6_S = a.Replace("S=", "").Trim();
										break;
									case string l when l.Trim().StartsWith("C="):
										cert.Subject7_C = a.Replace("C=", "").Trim();
										break;
									default:
										if (a.Trim().Equals("")) { }
										else
										{
											cert.ExtraData.Add(string.Concat("UnknownSubject", i, ": ", a.Trim()));
										}
										break;
								}
							}

							break;
						case string e when e.Trim().StartsWith("Cert Hash("):
							cert.Thumbprint = lines[i].Replace("Cert Hash(", "(").Trim();
							break;
						case string e when e.Trim().StartsWith("Key Container ="):
							cert.KeyContainer = lines[i].Replace("Key Container =", "").Trim();
							break;
						case string e when e.Trim().StartsWith("Unique container name:"):
							cert.UniqueContainerName = lines[i].Replace("Unique container name:", "").Trim();
							break;
						case string e when e.Trim().StartsWith("Provider ="):
							cert.ProviderName = lines[i].Replace("Provider =", "").Trim();
							break;
						default:
							if (lines[i].Trim().Equals("")) { }
							else
							{
								if (lines[i].Trim().EndsWith("command completed successfully."))
								{
									// TODO
								}
								else
								{
									cert.ExtraData.Add(string.Concat("UnknownLine", i, ": ", lines[i].Trim()));
								}
							}
							break;
					}
				}
			}
			return cert;
		}

		internal int? Number;
		internal string? Serial;
		internal string? Issuer1_E;
		internal string? Issuer2_CN;
		internal string? Issuer3_OU;
		internal string? Issuer4_O;
		internal string? Issuer5_L;
		internal string? Issuer6_S;
		internal string? Issuer7_C;
		internal DateTime NotBefore;
		internal DateTime NotAfter;
		internal string? Subject1_E;
		internal string? Subject2_CN;
		internal string? Subject3_OU;
		internal string? Subject4_O;
		internal string? Subject5_L;
		internal string? Subject6_S;
		internal string? Subject7_C;
		internal string? Thumbprint;
		internal string? KeyContainer;
		internal string? UniqueContainerName;
		internal string? ProviderName;
		internal readonly HashSet<string> ExtraData = new HashSet<string>();
	}
}
