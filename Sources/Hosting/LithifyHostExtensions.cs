using System;
using System.CommandLine;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Lithify.Hosting;

/// <summary>
/// <see cref="IHost"/> 上で Lithify の CLI を実行する拡張メソッド。
/// </summary>
public static class LithifyHostExtensions
{
    /// <summary>
    /// コマンドライン引数を解釈して Lithify を実行し、終了コードを返す。
    /// </summary>
    /// <param name="host">構築済みのホスト。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>プロセスの終了コード。0 は成功。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> が <see langword="null"/> である。</exception>
    /// <exception cref="InvalidOperationException">
    /// <c>UseLithify()</c> が呼ばれていない。
    /// </exception>
    /// <remarks>
    /// <para>
    /// サブコマンドは <see cref="ILithifyCommandProvider"/> の登録から組み立てる。
    /// <c>serve</c> は <c>AddDevelopmentServer()</c> が呼ばれている場合のみ現れるので、
    /// <c>Lithify.Serve</c> を参照していないプロジェクトのヘルプには出ない。
    /// </para>
    /// <para>
    /// 例外ではなく終了コードを返すのは、CLI の失敗が「プログラムの誤り」ではなく
    /// 「入力の誤り」であるためである。文書の誤りは <c>Diagnostic</c> として報告され、
    /// エラーが 1 件以上あれば非ゼロの終了コードになる。
    /// </para>
    /// </remarks>
    public static async Task<int> RunLithifyAsync(
        this IHost host,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        var services = host.Services;
        var command = BuildRootCommand(services);

        var parseResult = command.Parse(GetCommandLineArguments());

        if (parseResult.Errors.Count > 0)
        {
            // 解析エラーの提示は System.CommandLine 側の既定動作に任せる。
            // ここで整形し直すと --help の書式と揃わなくなる。
            _ = await parseResult.InvokeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            return ExitCodes.UsageError;
        }

        return await parseResult.InvokeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 登録されたコマンド プロバイダーからルート コマンドを組み立てる。
    /// </summary>
    /// <param name="services">サービス プロバイダー。</param>
    /// <returns>ルート コマンド。</returns>
    /// <exception cref="InvalidOperationException"><c>UseLithify()</c> が呼ばれていない。</exception>
    private static RootCommand BuildRootCommand(
        IServiceProvider services)
    {
        var providers = services.GetServices<ILithifyCommandProvider>().ToArray();

        if (providers.Length == 0)
        {
            throw new InvalidOperationException(Messages.UseLithifyNotCalled);
        }

        var root = new RootCommand("Lithify — a static site generator.");

        CommonOptions.AddTo(root);

        // 名前の辞書順で追加する。DI の登録順に従うとヘルプの表示順が
        // プラグインを足した順で変わり、再現しない。
        foreach (var subcommand in providers
            .Select(static provider => provider.CreateCommand())
            .OrderBy(static subcommand => subcommand.Name, StringComparer.Ordinal))
        {
            root.Add(subcommand);
        }

        return root;
    }

    /// <summary>
    /// プロセスのコマンドライン引数を取得する。
    /// </summary>
    /// <returns>実行ファイル名を除いた引数。</returns>
    /// <remarks>
    /// <c>Host.CreateApplicationBuilder(args)</c> に渡した <c>args</c> をここでも
    /// 受け取る形にはしない。同じ配列を 2 か所に渡すのは呼び出し側が間違えやすく、
    /// 食い違ったときの症状（構成だけ効いてコマンドが効かない）が分かりにくい。
    /// </remarks>
    private static string[] GetCommandLineArguments()
    {
        var args = Environment.GetCommandLineArgs();

        return args.Length <= 1
            ? []
            : args[1..];
    }
}
