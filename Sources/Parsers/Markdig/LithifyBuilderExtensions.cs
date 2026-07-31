using System;

using Lithify.Abstractions;
using Lithify.Hosting;
using Lithify.Markdown.Abstractions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// <see cref="ILithifyBuilder"/> に Markdig による Markdown のパーサーを追加する拡張メソッド。
/// </summary>
public static class LithifyBuilderExtensions
{
    /// <summary>
    /// Markdig による Markdown のパーサーを登録する。
    /// </summary>
    /// <param name="builder">Lithify のビルダー。</param>
    /// <returns><paramref name="builder"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// <para>
    /// 複数回呼び出しても登録は 1 つである（<c>TryAddEnumerable</c> が実装型で重複を排除する）。
    /// <see cref="ContentFormat.Markdown"/> を扱うパーサーが 2 つ登録されると
    /// <see cref="IContentFormatRegistry"/> が競合として診断を出すので、冪等でなければならない。
    /// </para>
    /// <para>
    /// <see cref="MarkdownOptions"/> と <see cref="MarkdigOptions"/> は
    /// <c>Lithify:Markdown</c> と <c>Lithify:Markdig</c> のセクションから束縛される。
    /// <c>Lithify</c> の下に置くのは、サイトの構成ファイルの最上位に
    /// エンジンの名前が並ぶのを避けるためである。
    /// </para>
    /// <para>
    /// <strong>形式の設定とエンジンの設定を別のセクションにしている。</strong>
    /// 同じセクションに混ぜると、Markdig を差し替えたときに
    /// 「どの設定が残り、どれが意味を失うか」が構成ファイルから読み取れなくなる。
    /// </para>
    /// </remarks>
    public static ILithifyBuilder UseMarkdig(
        this ILithifyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        services.AddOptions<MarkdownOptions>()
            .Bind(builder.Configuration.GetSection(MarkdownSectionName));

        services.AddOptions<MarkdigOptions>()
            .Bind(builder.Configuration.GetSection(MarkdigSectionName));

        // ファクトリーを明示するのは、実装型を型引数だけで渡すと
        // CA1812（インスタンス化されない internal クラス）が立つためである。
        // TryAddEnumerable の重複排除には実装型が要るので、型引数も省けない。
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IContentParser, MarkdigContentParser>(
                static provider => new MarkdigContentParser(
                    provider.GetRequiredService<IOptions<MarkdownOptions>>(),
                    provider.GetRequiredService<IOptions<MarkdigOptions>>())));

        return builder;
    }

    /// <summary>
    /// <see cref="MarkdownOptions"/> を束縛する構成のセクション名。
    /// </summary>
    private const string MarkdownSectionName = "Lithify:Markdown";

    /// <summary>
    /// <see cref="MarkdigOptions"/> を束縛する構成のセクション名。
    /// </summary>
    private const string MarkdigSectionName = "Lithify:Markdig";
}
