using System.Diagnostics;
using System.Text;
using api_backend.Models.Backup;
using Npgsql;

namespace api_backend.Services.Backup;

/// <summary>Writes and loads database dumps. Production: <see cref="PgDumper"/> (the Postgres client tools).</summary>
public interface IDatabaseDumper
{
	/// <summary>
	/// Custom-format dump of the database as of <paramref name="snapshot"/> (from <c>pg_export_snapshot()</c>, so the
	/// caller can read other data from the same point in time). Rows of <see cref="BackupTables.DataExcluded"/> are left out.
	/// </summary>
	Task DumpAsync(string connectionString, string snapshot, string targetFile, CancellationToken ct);

	/// <summary>Replaces the whole <c>public</c> schema with the dump's, in one transaction: all or nothing.</summary>
	Task ReplaceAsync(string connectionString, string dumpFile, CancellationToken ct);
}

/// <summary>
/// Runs <c>pg_dump</c> / <c>pg_restore</c> / <c>psql</c> (postgresql-client-18 in the API image; the client must not be
/// older than the server). Credentials go through <c>PG*</c> environment variables, never the command line.
/// </summary>
public sealed class PgDumper : IDatabaseDumper
{
	// not `pg_restore --clean`: it only drops what the dump contains, so tables of newer migrations would survive and
	// the migrate after the restore would fail on them
	private const string ResetSchema = "DROP SCHEMA public CASCADE;\nCREATE SCHEMA public;\n";

	public async Task DumpAsync(string connectionString, string snapshot, string targetFile, CancellationToken ct)
	{
		List<string> args = ["--format=custom", "--no-owner", "--no-acl", $"--snapshot={snapshot}", $"--file={targetFile}"];
		args.AddRange(BackupTables.DataExcluded.Select(t => $"--exclude-table-data={t}"));
		using var dump = Start("pg_dump", args, connectionString, redirectIn: false, redirectOut: false);
		await WaitAsync(dump, ct);
	}

	public async Task ReplaceAsync(string connectionString, string dumpFile, CancellationToken ct)
	{
		// psql -1 wraps the schema reset and the whole reload in one transaction
		using var psql = Start("psql", ["-X", "-q", "-v", "ON_ERROR_STOP=1", "--single-transaction", "-f", "-"],
			connectionString, redirectIn: true, redirectOut: false);
		using var restore = Start("pg_restore", ["--no-owner", "--no-acl", "-f", "-", dumpFile],
			connectionString, redirectIn: false, redirectOut: true);
		var input = psql.Process.StandardInput.BaseStream;
		try
		{
			await input.WriteAsync(Encoding.UTF8.GetBytes(ResetSchema), ct);
			await restore.Process.StandardOutput.BaseStream.CopyToAsync(input, ct);
		}
		catch (IOException)
		{
			// psql stopped reading (ON_ERROR_STOP): its exit code and stderr say why
			await WaitAsync(psql, ct);
			throw new BackupFailedException("psql stopped reading the restore.");
		}
		// end of input commits: only after pg_restore produced the whole dump. Any throw before this kills psql
		// (Tool.Dispose), its connection drops and the transaction rolls back.
		await WaitAsync(restore, ct);
		input.Close();
		await WaitAsync(psql, ct);
	}

	private sealed class Tool(Process process, string name, StringBuilder stderr) : IDisposable
	{
		public Process Process { get; } = process;
		public string Name { get; } = name;
		public StringBuilder Stderr { get; } = stderr;

		public void Dispose()
		{
			if (!Process.HasExited)
				Process.Kill(entireProcessTree: true);
			Process.Dispose();
		}
	}

	private static Tool Start(string fileName, IEnumerable<string> args, string connectionString, bool redirectIn, bool redirectOut)
	{
		var info = new ProcessStartInfo(fileName)
		{
			RedirectStandardInput = redirectIn,
			RedirectStandardOutput = redirectOut,
			RedirectStandardError = true,
			UseShellExecute = false,
		};
		foreach (var arg in args)
			info.ArgumentList.Add(arg);

		var cs = new NpgsqlConnectionStringBuilder(connectionString);
		info.Environment["PGHOST"] = cs.Host;
		info.Environment["PGPORT"] = cs.Port.ToString();
		info.Environment["PGDATABASE"] = cs.Database;
		info.Environment["PGUSER"] = cs.Username;
		info.Environment["PGPASSWORD"] = cs.Password;
		if (cs.SslMode != SslMode.Prefer)
			info.Environment["PGSSLMODE"] = cs.SslMode.ToString().ToLowerInvariant().Replace("verifyca", "verify-ca").Replace("verifyfull", "verify-full");

		var stderr = new StringBuilder();
		var process = new Process { StartInfo = info };
		process.ErrorDataReceived += (_, e) =>
		{
			if (e.Data is null)
				return;
			lock (stderr)
				stderr.AppendLine(e.Data);
		};
		try
		{
			process.Start();
		}
		catch (System.ComponentModel.Win32Exception e)
		{
			process.Dispose();
			throw new BackupFailedException($"{fileName} is not available ({e.Message}). The API image installs postgresql-client-18.");
		}
		process.BeginErrorReadLine();
		return new Tool(process, fileName, stderr);
	}

	private static async Task WaitAsync(Tool tool, CancellationToken ct)
	{
		await tool.Process.WaitForExitAsync(ct);
		if (tool.Process.ExitCode == 0)
			return;
		string message;
		lock (tool.Stderr)
			message = tool.Stderr.ToString().Trim();
		if (message.Length > 1500)
			message = "…" + message[^1500..];
		throw new BackupFailedException($"{tool.Name} failed (exit {tool.Process.ExitCode}): {message}");
	}
}

/// <summary>A backup or restore step failed; the message is shown to the super admin.</summary>
public sealed class BackupFailedException(string message, Exception? inner = null) : Exception(message, inner);
