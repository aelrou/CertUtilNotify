using CertUtilNotify.Model;
using System.Diagnostics;

namespace CertUtilNotify.Method
{
	static class OpenSslUtil
	{
		const int HandshakeTimeoutMs = 20000;
		const int ParseTimeoutMs = 10000;

		internal static HashSet<OpenSslModel> Query(string domain)
		{
			HashSet<OpenSslModel> empty = new HashSet<OpenSslModel>();
			if (string.IsNullOrWhiteSpace(domain))
			{
				return empty;
			}

			domain = domain.Trim();
			string openSslPath = Program.OpenSSLPath;
			if (string.IsNullOrWhiteSpace(openSslPath))
			{
				Console.WriteLine("OpenSSLPath is not set. Skipping " + domain + ".");
				return empty;
			}

			try
			{
				// s_client | x509 is a shell pipeline. Process does not run a shell, and the
				// default OpenSSL path contains spaces, so run the two commands and pass the
				// certificate text on stdin. Closing stdin is the < NUL that lets s_client exit.
				string handshake = Run(
					openSslPath,
					new string[]
					{
						"s_client",
						"-connect", domain + ":443",
						"-servername", domain
					},
					null,
					HandshakeTimeoutMs,
					out string handshakeError
				);

				if (handshake.IndexOf("-----BEGIN CERTIFICATE-----", StringComparison.Ordinal) < 0)
				{
					Console.WriteLine("OpenSSL query failed for " + domain + ": no certificate returned." + ReportedError(handshakeError));
					return empty;
				}

				// x509 reads the first certificate and then exits. The rest of the s_client
				// transcript is what closed the pipe with "The pipe has been ended."
				string certificate = FirstCertificate(handshake);
				string fields = Run(
					openSslPath,
					new string[]
					{
						"x509",
						"-noout",
						"-startdate",
						"-enddate",
						"-fingerprint",
						"-sha1",
						"-subject"
					},
					certificate,
					ParseTimeoutMs,
					out string fieldsError
				);

				string[] ioLines = fields.Split(new string[] { Environment.NewLine },
					StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
				);

				OpenSslModel openSslModel = new OpenSslModel();
				HashSet<OpenSslModel> openSslSet = openSslModel.ParseAllLines(ioLines);
				if (openSslSet.Count == 0)
				{
					Console.WriteLine("OpenSSL query failed for " + domain + ": certificate fields were not recognized." + ReportedError(fieldsError));
					return empty;
				}
				foreach (OpenSslModel cert in openSslSet)
				{
					cert.Domain = domain;
					if (string.IsNullOrWhiteSpace(cert.Subject2_CN))
					{
						cert.Subject2_CN = domain;
					}
				}
				return openSslSet;
			}
			catch (Exception ex)
			{
				Console.WriteLine("OpenSSL query failed for " + domain + ": " + ex);
				return empty;
			}
		}

		static string ReportedError(string stderr)
		{
			if (string.IsNullOrWhiteSpace(stderr))
			{
				return "";
			}
			return Program.nl + stderr.Trim();
		}

		static string FirstCertificate(string handshake)
		{
			const string begin = "-----BEGIN CERTIFICATE-----";
			const string end = "-----END CERTIFICATE-----";
			int start = handshake.IndexOf(begin, StringComparison.Ordinal);
			int stop = start < 0 ? -1 : handshake.IndexOf(end, start, StringComparison.Ordinal);
			if (start < 0 || stop < start)
			{
				return handshake;
			}
			return handshake.Substring(start, stop + end.Length - start) + Environment.NewLine;
		}

		static string Run(string fileName, string[] args, string? stdin, int timeoutMs, out string stderr)
		{
			stderr = "";
			using Process process = new Process();
			process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			process.StartInfo.FileName = fileName;
			foreach (string arg in args)
			{
				process.StartInfo.ArgumentList.Add(arg);
			}
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.StartInfo.RedirectStandardInput = true;
			process.Start();

			Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
			Task<string> stderrTask = process.StandardError.ReadToEndAsync();
			IOException? pipeError = null;
			if (!string.IsNullOrEmpty(stdin))
			{
				try
				{
					process.StandardInput.Write(stdin);
				}
				catch (IOException ex)
				{
					pipeError = ex;
				}
			}
			try
			{
				process.StandardInput.Close();
			}
			catch (IOException ex)
			{
				if (pipeError == null)
				{
					pipeError = ex;
				}
			}

			string command = string.Join(" ", args);
			bool exited = process.WaitForExit(timeoutMs);
			if (!exited)
			{
				try
				{
					process.Kill(entireProcessTree: true);
				}
				catch (Exception)
				{
				}
				process.WaitForExit(5000);
				stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : "";
				throw new TimeoutException(command + " timed out." + ReportedError(stderr));
			}

			process.WaitForExit();
			string stdout = stdoutTask.Result;
			stderr = stderrTask.Result;
			if (pipeError != null && string.IsNullOrWhiteSpace(stdout))
			{
				throw new IOException(
					command + " closed the input pipe before it wrote a result. Exit code " + process.ExitCode + "." + ReportedError(stderr),
					pipeError
				);
			}
			if (pipeError != null)
			{
				Console.WriteLine(
					"OpenSSL closed the input pipe during " + command + " after writing a result. Exit code " + process.ExitCode + "." + ReportedError(stderr)
				);
			}
			if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
			{
				throw new InvalidOperationException(
					command + " exited " + process.ExitCode + "." + ReportedError(stderr)
				);
			}
			return stdout;
		}
	}
}
