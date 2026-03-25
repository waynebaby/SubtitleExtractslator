using SubtitleExtractslator.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var options = AppOptions.Parse(args);

if (options.Mode == AppMode.Mcp)
{
	var hostBuilder = Host.CreateApplicationBuilder(args);
	hostBuilder.Services.AddSingleton(new WorkflowOrchestrator(ModeContext.Mcp));
	hostBuilder.Services
		.AddMcpServer()
		.WithStdioServerTransport()
		.WithTools<SubtitleMcpTools>();

	using var host = hostBuilder.Build();
	await host.RunAsync();
	return;
}

if (string.IsNullOrWhiteSpace(options.Command))
{
	Console.WriteLine(AppOptions.HelpText);
	return;
}

var orchestrator = new WorkflowOrchestrator(ModeContext.Cli);
var result = await CliCommandRunner.RunAsync(orchestrator, options);
Console.WriteLine(result);
