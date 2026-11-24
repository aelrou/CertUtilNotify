# AGENTS.md

This file guides coding agents working in this repository. It is the project brief: how to build, how a run fits together, and which behavior to leave alone.

## Project

CertUtilNotify is a Windows console program that scans certificates and sends one plain-text email when a certificate is inside the expiration window. It is a single-shot executable, meant to be started on a schedule. `README.md` only names the .NET 8 desktop runtime. Runtime details live here.

## Commands

Build:

```
dotnet build CertutilNotify.sln
```

Release build:

```
dotnet build CertUtilNotify\CertUtilNotify.csproj -c Release
```

There is no test project and no linter. A source change is verified when `dotnet build CertutilNotify.sln` succeeds. Say that only after the build output shows it.

This workspace has .NET SDK 11.0.100. The target framework is `net8.0-windows10.0.17763.0`. Package references are `Microsoft.Extensions.Configuration.Json` 11.0.0 and `System.Text.Json` 11.0.0. Leave the target framework and package versions as they are unless the task is to change them.

Run the program only when the user asks. If `appsettings.json` is missing from the working directory, `Main` creates one from `AppSettingsModel` and then contacts every configured domain and the SMTP server.

## Layout

| Path | Role |
| --- | --- |
| `CertutilNotify.sln` | Solution. Its project entry is `CertutilNotify\CertUtilNotify.csproj`. The directory on disk is `CertUtilNotify\`. |
| `CertUtilNotify\Program.cs` | Entry point, config load, notice text, console-to-file tee. |
| `CertUtilNotify\Method\` | `certutil`, OpenSSL, and SMTP. |
| `CertUtilNotify\Model\` | Settings object and the two certificate parsers. |
| `CertUtilNotify\appsettings.json.sample` | Tracked config template. |
| `CertUtilNotify-Win64.7z` | Committed Win64 build snapshot. It is not source. |

`bin\`, `obj\`, `*.log`, and `/tmp` are gitignored. Leave the archive, build outputs, and log files out of a source change.

## How a run works

1. `Main` looks for `appsettings.json` in the process working directory, not beside the executable. A missing file is created from `AppSettingsModel` and the run continues. A file that cannot be read or parsed prints the exception and exits 1.
2. `StartLog` creates `LogsDirectory` and tees `Console.Out` to a new `yyyy-MM-dd_HHmmss.log`. An empty `LogsDirectory` keeps console-only output. A log failure is printed and the run continues. Settings errors happen before the tee, so they reach the original console only.
3. `CertUtil.Query("My")` runs `C:\Windows\System32\certutil.exe -store "My"` (local-machine Personal store). `CertUtilModel` parses that text.
4. Each non-blank `Domains` entry goes to `OpenSslUtil.Query`. That runs `openssl s_client -connect <domain>:443 -servername <domain>`, keeps the first PEM block, then runs `openssl x509 -noout -startdate -enddate -fingerprint -sha1 -subject`. A failed domain is logged and skipped.
5. `AppendCertificateNotice` appends an `Expiring` section when `now < NotAfter < now + DaysToWarnExpire` and sets `notify`. It appends a `New` section when `now - DaysToNotifyNew < NotBefore < now` and leaves `notify` unchanged.
6. `EmailService.SendEmail` runs only when `notify` is true. It uses `System.Net.Mail.SmtpClient` with SSL, a 10 second timeout, and `ServicePointManager.ServerCertificateValidationCallback` accepting every certificate until the send finishes. SMTP errors are printed. The process still exits 0.

Report dates use `yyyy-MM-dd`. Comparisons use `DateTime.UtcNow`. The "days remaining" line formats `TotalDays` with `"0"`, which rounds. The `HashSet` values use reference equality, so certificates are not collapsed by thumbprint.

## Configuration

`AppSettingsModel` is what gets serialized when the file is created. Property names are the JSON keys. Change that class and `appsettings.json.sample` together.

| Key | Sample default | Meaning |
| --- | --- | --- |
| `LogsDirectory` | `C:\Users\Public\CertutilNotify\Logs` | Log folder. Empty or whitespace skips the file. |
| `OpenSSLPath` | `C:\Program Files\OpenSSL-Win64\bin\openssl.exe` | OpenSSL executable. Empty skips every domain. |
| `Domains` | `www.google.com`, `www.cloudflare.com` | Host names. The port is always 443. |
| `DaysToWarnExpire` | `3` | Expiration window, in days. |
| `DaysToNotifyNew` | `2` | Recently issued window, in days. |
| `EmailSubject` | `MyServer security certificate status` | Subject prefix. The UTC date is appended. |
| `ToAddressList` | comma-separated addresses | Passed as one string to `MailMessage.To.Add`. |
| `FromAddress`, `SMTP_Username`, `SMTP_Password`, `SMTP_Host` | placeholders | Mail identity and relay. |
| `SMTP_Port` | `587` | Use 587. The comment in `EmailService` records that `SmtpClient` does not handle implicit TLS on 465. |

Never commit `appsettings.json`. `.gitignore` does not ignore that file name, so a copy next to the project can be staged by accident. A debug copy under `bin\` is ignored and may hold local hosts and domains. Leave it out of source, the sample, and commits. The sample password is the placeholder `password`.

## Conventions

- Indent C# with tabs. The project file stays on the SDK's two-space XML.
- `OpenSslUtil.cs` and `OpenSslModel.cs` use LF. The other C# files use CRLF. There is no `.gitattributes` and no `.editorconfig`. Match the line endings of the file you edit. A line-ending-only edit is not a change to make.
- Nullable reference types and implicit usings are on. New code should compile clean under nullable.
- Most types use the block namespace `CertUtilNotify`. `CertString` is in `CertutilNotify.Method`, and `CertUtilModel` imports that namespace. Leave that casing unless the task is to rename it.
- `EmailService` and `CertString` use file-scoped namespaces. The other files use block namespaces. Match the file you are editing.
- Certificate models use `internal` fields. `AppSettingsModel` uses public properties because it is the JSON object.
- Helpers are static classes. There is no generic host and no dependency injection. Add one only when the task asks for it.
- Match the string style of the file you edit. Leave unrelated lines alone.
- Keep comments that explain a process, a pipe, or the SMTP port. `OpenSslUtil` explains why stdin is closed and why a broken pipe can be ignored. Skip comments that restate the next statement.
- New process launches should follow `OpenSslUtil.Run`: `ArgumentList`, async stdout and stderr, a timeout, and disposal. `CertUtil.Query` is the existing local-store call.

## Behavior to preserve

Change this only when the task is about that behavior.

- Config is read and created in the working directory.
- The only local query is the machine `My` store. `WebHosting`, CA stores, and the current-user store are unread.
- Remote checks take the first certificate from port 443. The subject alternative name is not compared with the configured domain.
- A certificate with `NotAfter <= now` adds no section and no mail.
- `New` text is added to the body. Mail goes out only when some certificate is inside the expiration window, because only that branch sets `notify`.
- Both windows are exclusive on each end.
- A certutil thumbprint is stored after `Replace("Cert Hash(", "(")`, so the value still contains the algorithm label. An OpenSSL thumbprint stays a colon-separated SHA-1 fingerprint.
- OpenSSL dates use `Program.DateTimeOpenSslFormat` (`MMM d HH:mm:ss yyyy 'GMT'`), invariant culture, and universal time. certutil dates use `DateTime.Parse(...).ToUniversalTime()`, which follows the current culture.
- `CertString.Eval` splits a distinguished name on commas outside quotes and honors backslash escapes. Quote characters stay in the token.
- One unreadable local certificate throws from `CertUtilModel` and skips the domain loop and the email. The local query runs first.
- A failed domain query or a failed send does not change the exit code. Exit code 0 means the process finished. It does not mean mail was accepted.
- SMTP server-certificate validation is disabled for the send and restored in `finally`. The callback is process-global.

## Machine requirements

The program runs on Windows. `certutil.exe` is loaded from `C:\Windows\System32\certutil.exe`. OpenSSL is whatever path `OpenSSLPath` names. Building the `net8.0-windows10.0.17763.0` target needs the .NET 8 Windows targeting pack. Running the built exe needs the .NET 8 desktop runtime.
