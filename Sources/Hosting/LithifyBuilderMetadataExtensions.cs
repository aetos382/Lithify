using System;

using Lithify.Core.Metadata;

using Microsoft.Extensions.DependencyInjection;

namespace Lithify.Hosting;

/// <summary>
/// <see cref="ILithifyBuilder"/> にメタデータの設定を行う拡張メソッド。
/// </summary>
public static class LithifyBuilderMetadataExtensions
{
    /// <summary>
    /// well-known キーに写す別名を設定する。
    /// </summary>
    /// <param name="builder">Lithify のビルダー。</param>
    /// <param name="configure">設定を行うデリゲート。</param>
    /// <returns><paramref name="builder"/>。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> または <paramref name="configure"/> が <see langword="null"/> である。
    /// </exception>
    /// <remarks>
    /// <para>
    /// 設定は<em>すべてのパーサーに</em>適用される。写し先は形式を跨いだ共通の語彙なので、
    /// 「このサイトでは <c>abstract</c> が概要である」という宣言は Markdown でも AsciiDoc でも成り立つ。
    /// 各パーサーが自分の既定にこの設定を重ねる（<see cref="MetadataAliasOptions.Apply"/>）。
    /// </para>
    /// <para>
    /// 構成ファイルからは束縛しない。候補の並びは優先順という意味を持ち、
    /// 空の並びと未設定を区別する必要がある（前者は写しを止める指示、後者は既定のまま）。
    /// 構成の束縛はどちらの区別も表現できない。
    /// </para>
    /// <para>
    /// 複数回呼び出した場合は呼ばれた順に適用される。同じ写し先を 2 度設定すれば後の設定が残る
    /// （設定は写し先ごとの置き換えである）。
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// builder.ConfigureMetadataAliases(a =>
    /// {
    ///     a.Description = ["abstract", "summary"];
    ///     a.LastModified = ["modified-on", MetadataAliasCandidate.Defaults];
    ///     a.Tags = [];
    /// });
    /// </code>
    /// </example>
    public static ILithifyBuilder ConfigureMetadataAliases(
        this ILithifyBuilder builder,
        Action<MetadataAliasOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        return builder;
    }
}
