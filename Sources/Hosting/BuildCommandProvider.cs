using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

using Lithify.Core.Building;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lithify.Hosting;

/// <summary>
/// <c>build</c> コマンドを提供する。
/// </summary>
internal sealed class BuildCommandProvider :
    ILithifyCommandProvider
{
    private readonly IServiceProvider _services;

    public BuildCommandProvider(
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        this._services = services;
    }

    public Command CreateCommand()
    {
        var command = new Command("build", "Generate the site.");

        command.SetAction(this.ExecuteAsync);

        return command;
    }

    private async Task<int> ExecuteAsync(
        ParseResult parseResult,
        CancellationToken cancellationToken)
    {
        // IOptions<LithifyOptions> を解決する前に上書きを適用しなければならない。
        // Options は初回解決時に値を確定させるので、順序を逆にすると無視される。
        CommonOptions.ApplyTo(parseResult, this._services.GetRequiredService<CommandLineOverrides>());

        var builder = this._services.GetRequiredPlugin<ISiteBuilder>();
        var report = await builder.BuildAsync(cancellationToken).ConfigureAwait(false);

        var logger = this._services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Lithify.Build");

        DiagnosticReporter.Report(logger, report.Diagnostics);

        return report.HasErrors
            ? ExitCodes.DiagnosticError
            : ExitCodes.Success;
    }
}
