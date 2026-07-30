using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;

using Lithify.Core.Building;

using Microsoft.Extensions.DependencyInjection;

namespace Lithify.Hosting;

/// <summary>
/// <c>clean</c> コマンドを提供する。
/// </summary>
internal sealed class CleanCommandProvider :
    ILithifyCommandProvider
{
    private readonly IServiceProvider _services;

    public CleanCommandProvider(
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        this._services = services;
    }

    public Command CreateCommand()
    {
        var command = new Command("clean", "Delete the generated site and the build cache.");

        command.SetAction(this.ExecuteAsync);

        return command;
    }

    private async Task<int> ExecuteAsync(
        ParseResult parseResult,
        CancellationToken cancellationToken)
    {
        CommonOptions.ApplyTo(parseResult, this._services.GetRequiredService<CommandLineOverrides>());

        var cleaner = this._services.GetRequiredPlugin<ISiteCleaner>();

        _ = await cleaner.CleanAsync(cancellationToken).ConfigureAwait(false);

        return ExitCodes.Success;
    }
}
