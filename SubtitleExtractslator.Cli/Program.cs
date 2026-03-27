using SubtitleExtractslator.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;

var options = AppOptions.Parse(args);

var cliLogsEnabled = true;
if (options.Arguments.TryGetValue("quiet", out var quietArg)
	&& bool.TryParse(quietArg, out var quiet)
	&& quiet)
{
	cliLogsEnabled = false;
}

var logEnv = Environment.GetEnvironmentVariable("SUBTITLEEXTRACTSLATOR_CLI_LOG");
if (!string.IsNullOrWhiteSpace(logEnv)
	&& bool.TryParse(logEnv, out var envEnabled))
{
	cliLogsEnabled = envEnabled;
}

var mcpLogsEnabled = true;
var mcpLogEnv = Environment.GetEnvironmentVariable("SUBTITLEEXTRACTSLATOR_MCP_LOG");
if (!string.IsNullOrWhiteSpace(mcpLogEnv)
	&& bool.TryParse(mcpLogEnv, out var mcpEnvEnabled))
{
	mcpLogsEnabled = mcpEnvEnabled;
}

CliRuntimeLog.Configure(options.Mode switch
{
	AppMode.Cli => cliLogsEnabled,
	AppMode.Mcp => mcpLogsEnabled,
	_ => false
});

if (options.Mode == AppMode.Mcp)
{
	CliRuntimeLog.Info("mcp", "Connection state: Starting (server bootstrap)");
	var hostBuilder = Host.CreateApplicationBuilder(args);
	hostBuilder.Logging.ClearProviders();
	hostBuilder.Logging.SetMinimumLevel(LogLevel.None);
	hostBuilder.Services.AddSingleton(new WorkflowOrchestrator(ModeContext.Mcp));
	hostBuilder.Services
		.AddMcpServer()
		.WithStdioServerTransport()
		.WithTools<SubtitleMcpTools>();

	using var host = hostBuilder.Build();
	CliRuntimeLog.Info("mcp", "Connection state: Starting (stdio transport ready)");
	await host.RunAsync();
	return;
}

if (string.IsNullOrWhiteSpace(options.Command))
{
	Console.WriteLine(AppOptions.HelpText);
	return;
}

var orchestrator = new WorkflowOrchestrator(ModeContext.Cli);
try
{
	var result = await CliCommandRunner.RunAsync(orchestrator, options);
	Console.WriteLine(result);
}
catch (Exception ex)
{
	var snapshotPath = ErrorSnapshotWriter.Write(
		"cli-unhandled-error",
		new Dictionary<string, string?>
		{
			["mode"] = options.Mode.ToString(),
			["command"] = options.Command,
			["arguments"] = string.Join(" ", args),
			["time"] = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture),
			["exception"] = ex.ToString()
		});
	Console.Error.WriteLine(ex.Message);
	Console.Error.WriteLine($"Detailed error log: {snapshotPath}");
	Environment.ExitCode = 1;
}
