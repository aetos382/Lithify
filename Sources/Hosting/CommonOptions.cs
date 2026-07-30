using System.CommandLine;

namespace Lithify.Hosting;

/// <summary>
/// すべてのサブコマンドで共有されるオプション。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Option.Recursive"/> を立ててルート コマンドに 1 度だけ追加する。
/// サブコマンドごとに同じオプションを定義すると、<c>serve</c> のような
/// 別パッケージのコマンドが同じ定義を再現しなければならなくなる。
/// </para>
/// <para>
/// <see langword="static"/> なインスタンスを共有できるのは、
/// <see cref="Option{T}"/> が解析結果を保持せず、値が
/// <see cref="ParseResult"/> 側に載るためである。
/// </para>
/// </remarks>
internal static class CommonOptions
{
    public static Option<string?> SourceRoot { get; } =
        new("--source", "-s")
        {
            Description = "The root directory of the site sources.",
            HelpName = "directory",
            Recursive = true,
        };

    public static Option<string?> OutputRoot { get; } =
        new("--output", "-o")
        {
            Description = "The directory the generated site is written to.",
            HelpName = "directory",
            Recursive = true,
        };

    public static Option<string?> CacheRoot { get; } =
        new("--cache")
        {
            Description = "The directory the incremental build cache is kept in.",
            HelpName = "directory",
            Recursive = true,
        };

    /// <summary>
    /// キャッシュを無視して全出力を書き直すオプションを取得する。
    /// </summary>
    /// <remarks>
    /// 出力ディレクトリの手編集はサポートしないため、キャッシュを信頼できなくなった場合の
    /// 逃げ道はこれだけである。
    /// </remarks>
    public static Option<bool> Force { get; } =
        new("--force")
        {
            Description = "Ignore the build cache and rewrite every output.",
            Recursive = true,
        };

    /// <summary>
    /// ルート コマンドに共通オプションを追加する。
    /// </summary>
    /// <param name="command">ルート コマンド。</param>
    public static void AddTo(
        Command command)
    {
        command.Add(SourceRoot);
        command.Add(OutputRoot);
        command.Add(CacheRoot);
        command.Add(Force);
    }

    /// <summary>
    /// 解析結果から <see cref="CommandLineOverrides"/> を埋める。
    /// </summary>
    /// <param name="parseResult">解析結果。</param>
    /// <param name="overrides">埋める対象。</param>
    public static void ApplyTo(
        ParseResult parseResult,
        CommandLineOverrides overrides)
    {
        overrides.SourceRoot = parseResult.GetValue(SourceRoot);
        overrides.OutputRoot = parseResult.GetValue(OutputRoot);
        overrides.CacheRoot = parseResult.GetValue(CacheRoot);

        // 既定値が false なので、指定されなかったことと false の指定を区別する必要がある。
        // 区別しないと --force を指定しないビルドが構成側の Force = true を打ち消す。
        overrides.Force = parseResult.GetResult(Force) is null
            ? null
            : parseResult.GetValue(Force);
    }
}
