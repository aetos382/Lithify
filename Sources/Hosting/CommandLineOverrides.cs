using System;

using Microsoft.Extensions.Options;

namespace Lithify.Hosting;

/// <summary>
/// コマンドライン引数で指定された値を <see cref="LithifyOptions"/> に上書きする。
/// </summary>
/// <remarks>
/// <para>
/// コマンドラインの解釈は <c>Build()</c> の<b>後</b>（<c>RunLithifyAsync()</c> の中）で起きるので、
/// 解釈結果を <c>IConfiguration</c> に流し込むことはできない。そこで
/// <see cref="IPostConfigureOptions{TOptions}"/> として最後に適用する。
/// これにより優先順位が「コマンドライン &gt; 環境変数 &gt; appsettings.json」と明示される。
/// </para>
/// <para>
/// <see langword="null"/> のプロパティは「指定されなかった」ことを表し、
/// 構成側の値をそのまま残す。<see cref="LithifyOptions"/> と同じ非 null の既定値を
/// ここに持たせると、常に構成を上書きしてしまう。
/// </para>
/// <para>
/// <c>IOptions&lt;LithifyOptions&gt;</c> は初回解決時に値を確定させるので、
/// コマンドのアクションはオプションを解決する<b>前</b>にこの型を埋めなければならない。
/// </para>
/// </remarks>
internal sealed class CommandLineOverrides :
    IPostConfigureOptions<LithifyOptions>
{
    public string? SourceRoot { get; set; }

    public string? OutputRoot { get; set; }

    public string? CacheRoot { get; set; }

    public bool? Force { get; set; }

    public void PostConfigure(
        string? name,
        LithifyOptions options)
    {
        // 名前付きオプションには適用しない。コマンドライン引数はサイト全体に効くものであり、
        // 名前付きの構成は利用者が別の目的で使っている可能性がある。
        if (name is not null &&
            !string.Equals(name, Options.DefaultName, StringComparison.Ordinal))
        {
            return;
        }

        if (this.SourceRoot is { } sourceRoot)
        {
            options.SourceRoot = sourceRoot;
        }

        if (this.OutputRoot is { } outputRoot)
        {
            options.OutputRoot = outputRoot;
        }

        if (this.CacheRoot is { } cacheRoot)
        {
            options.CacheRoot = cacheRoot;
        }

        if (this.Force is { } force)
        {
            options.Force = force;
        }
    }
}
