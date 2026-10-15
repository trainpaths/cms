using System.Data.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace api_backend.Tests.Integration;

/// <summary>
/// Counts DB commands (round trips) of requests carrying <see cref="Header"/>, so query budgets can be asserted
/// without other requests or background services interfering. Usage: <see cref="Reset"/>, send, read <see cref="Count"/>.
/// </summary>
public sealed class QueryCounter(IHttpContextAccessor http) : DbCommandInterceptor
{
	public const string Header = "X-Count-Queries";

	private int _count;
	private readonly List<string> _commands = [];

	public int Count => Volatile.Read(ref _count);

	/// <summary>SQL of the counted commands, for failure messages.</summary>
	public string Log
	{
		get { lock (_commands) return string.Join("\n---\n", _commands); }
	}

	public void Reset()
	{
		Interlocked.Exchange(ref _count, 0);
		lock (_commands) _commands.Clear();
	}

	private void Record(DbCommand command)
	{
		if (http.HttpContext?.Request.Headers.ContainsKey(Header) != true) return;
		Interlocked.Increment(ref _count);
		lock (_commands) _commands.Add(command.CommandText);
	}

	public override InterceptionResult<DbDataReader> ReaderExecuting(
		DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
	{
		Record(command);
		return result;
	}

	public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
		DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
	{
		Record(command);
		return ValueTask.FromResult(result);
	}

	public override InterceptionResult<int> NonQueryExecuting(
		DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
	{
		Record(command);
		return result;
	}

	public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
		DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
	{
		Record(command);
		return ValueTask.FromResult(result);
	}

	public override InterceptionResult<object> ScalarExecuting(
		DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
	{
		Record(command);
		return result;
	}

	public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
		DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken ct = default)
	{
		Record(command);
		return ValueTask.FromResult(result);
	}
}
