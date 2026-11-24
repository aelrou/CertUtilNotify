using CertUtilNotify.Model;
using System.Diagnostics;

namespace CertUtilNotify.Method
{
	static class CertUtil
	{
		const int QueryTimeoutMs = 30000;

		internal static HashSet<CertUtilModel> Query(string certStoreName)
		{
			using Process process = new Process();
			process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			process.StartInfo.FileName = "C:\\Windows\\System32\\certutil.exe";
			process.StartInfo.ArgumentList.Add("-store");
			process.StartInfo.ArgumentList.Add(certStoreName);
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			process.Start();

			// Read stdout and stderr together. A full stderr pipe would block certutil before it exits.
			Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
			Task<string> stderrTask = process.StandardError.ReadToEndAsync();

			string command = "certutil -store " + certStoreName;
			bool exited = process.WaitForExit(QueryTimeoutMs);
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
				string timedOutError = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : "";
				throw new TimeoutException(command + " timed out." + ReportedError(timedOutError));
			}

			process.WaitForExit();
			string stdout = stdoutTask.Result;
			string stderr = stderrTask.Result;
			if (process.ExitCode != 0)
			{
				throw new InvalidOperationException(
					command + " exited " + process.ExitCode + "." + ReportedError(stderr)
				);
			}

			string[] ioLines = stdout.Split(new string[] { Environment.NewLine },
				StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
			);

			CertUtilModel certUtilModel = new CertUtilModel();
			HashSet<CertUtilModel> certUtilSet = certUtilModel.ParseAllLines(ioLines);
			return certUtilSet;
		}

		static string ReportedError(string stderr)
		{
			if (string.IsNullOrWhiteSpace(stderr))
			{
				return "";
			}
			return Program.nl + stderr.Trim();
		}
	}
}
