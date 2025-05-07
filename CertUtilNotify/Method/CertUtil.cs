using CertUtilNotify.Model;
using System.Diagnostics;

namespace CertUtilNotify.Method
{
	static class CertUtil
	{
		internal static HashSet<CertUtilModel> Query(string certStoreName)
		{
			Process process = new Process();
			process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			process.StartInfo.FileName = "C:\\Windows\\System32\\certutil.exe";
			process.StartInfo.Arguments = "-store \"" + certStoreName + "\"";
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;
			//process.StartInfo.RedirectStandardInput = true;
			process.Start();
			string ioString = "";
			while (!process.HasExited)
			{
				ioString += process.StandardOutput.ReadToEnd();
			}

			string[] ioLines = ioString.Split(new string[] { Environment.NewLine },
				StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
			);

			CertUtilModel certUtilModel = new CertUtilModel();
			HashSet<CertUtilModel> certUtilSet = certUtilModel.ParseAllLines(ioLines);
			return certUtilSet;
		}
	}
}
