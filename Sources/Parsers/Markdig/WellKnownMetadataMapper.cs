using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Lithify.Abstractions;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// フロント マターのネイティブな名前を <see cref="WellKnownMetadata"/> のキーに写す。
/// </summary>
/// <remarks>
/// <para>
/// <strong>この写しはパーサーの責務である。</strong> 消費者（<c>Lithify.Blog</c> 等）側で
/// 「<c>lastmod</c> か <c>last_modified_at</c> か <c>updated</c> か」を分岐させると、
/// 消費者が各形式・各既存ジェネレーターの語彙を知る必要が生じる。
/// </para>
/// <para>
/// AsciiDoc と違い、Markdown のフロント マターには<em>仕様が定めたネイティブ名</em>が無い
/// （フロント マター自体が仕様外の拡張である）。したがってここで写すのは、
/// 既存の静的サイト ジェネレーターが広く使っている別名だけである。
/// 意味がずれるもの（Jekyll の <c>published</c> は <see cref="WellKnownMetadata.Draft"/> の否定、
/// Hugo の <c>categories</c> は <see cref="WellKnownMetadata.Tags"/> とは別の分類軸）は写さない。
/// 推測で写すと、書いた覚えのない値が効いている状態になる。
/// </para>
/// <para>
/// <strong>元の名前は残る。</strong> 写しは <see cref="DocumentMetadata.SetItem(MetadataKey, MetadataValue, MetadataProvenance)"/>
/// による<em>追加</em>であり、写し元の項目はそのまま <see cref="DocumentMetadata.Entries"/> に残る。
/// 写した項目には <see cref="MetadataProvenance.Mapped"/> が付き、写し元のキーと位置を辿れる。
/// </para>
/// </remarks>
internal static class WellKnownMetadataMapper
{
    /// <summary>
    /// 別名と、その別名が写される well-known キーの対応。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>この配列の並び順が優先順の宣言である。</strong> 同じ well-known キーに写せる別名が
    /// 複数書かれていた場合、ここで先に並んでいる別名が採用される
    /// （<c>lastmod</c> を <c>last_modified_at</c> より前に置いているのは、Hugo の綴りを優先するという判断）。
    /// フロント マターに書かれた順で決める選択肢もあるが、それだと YAML のキーを並べ替えるだけで
    /// 効く値が変わることになり、フロント マターのキーの順序に意味が無いことと矛盾する。
    /// </para>
    /// <para>
    /// キーは <see cref="MetadataKey.Create"/> による正規化を経ているので、
    /// Jekyll の <c>last_modified_at</c> は <c>last-modified-at</c> として一致する。
    /// </para>
    /// </remarks>
    private static readonly ImmutableArray<(MetadataKey Alias, MetadataKey Target)> Aliases =
    [
        // Hugo / Jekyll の最終更新日。
        (MetadataKey.Create("lastmod"), WellKnownMetadata.LastModified),
        (MetadataKey.Create("last_modified_at"), WellKnownMetadata.LastModified),
        (MetadataKey.Create("updated"), WellKnownMetadata.LastModified),

        // Hugo の概要。
        (MetadataKey.Create("summary"), WellKnownMetadata.Description),
        (MetadataKey.Create("excerpt"), WellKnownMetadata.Description),

        // 複数著者。MetadataValue.Sequence のまま写る。
        (MetadataKey.Create("authors"), WellKnownMetadata.Author),

        // well-known キーは lang なので、綴りきったものを写す。
        (MetadataKey.Create("language"), WellKnownMetadata.Language),

        // テンプレートの指定。
        (MetadataKey.Create("template"), WellKnownMetadata.Layout),
    ];

    /// <summary>
    /// well-known キーを生やしたメタデータを返す。
    /// </summary>
    /// <param name="metadata">フロント マターから読まれたメタデータ。</param>
    /// <param name="format">このコンテンツの形式。</param>
    /// <param name="path">診断に添えるコンテンツのパス。</param>
    /// <param name="diagnostics">診断の収集先。</param>
    /// <returns>well-known キーが生えたメタデータ。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="metadata"/> または <paramref name="diagnostics"/> が <see langword="null"/> である。
    /// </exception>
    /// <remarks>
    /// <see cref="WellKnownMetadata.Title"/> や <see cref="WellKnownMetadata.Date"/> のように
    /// ネイティブ名が well-known キーと一致するものは、正規化の時点で既に一致しているので
    /// ここでは何もしない（<see cref="MetadataProvenance.Declared"/> のまま残る）。
    /// </remarks>
    public static DocumentMetadata Map(
        DocumentMetadata metadata,
        ContentFormat format,
        ContentPath path,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var result = metadata;

        // 写した well-known キーと、それを供給した別名。重複の診断に使う。
        var suppliers = new Dictionary<MetadataKey, MetadataKey>();

        foreach (var (alias, target) in Aliases)
        {
            if (!metadata.TryGetValue(alias, out var value))
            {
                continue;
            }

            // 写し先が明示的に書かれている場合は写さない。書かれたものが別名に負けてはならない。
            // この場合は競合ではないので診断も出さない（別名を書くのは自然なことである）。
            if (metadata.Entries.ContainsKey(target))
            {
                continue;
            }

            if (suppliers.TryGetValue(target, out var winner))
            {
                diagnostics.Add(new Diagnostic(
                    DiagnosticIds.WellKnownKeyAmbiguous,
                    DiagnosticSeverity.Warning,
                    Messages.FormatWellKnownKeyAmbiguous(winner.Value, alias.Value, target.Value),
                    path,
                    metadata.GetProvenance(alias).Location));

                continue;
            }

            suppliers.Add(target, alias);

            // 位置は写し元の位置。診断は「その別名を書いた行」に向けるべきである。
            result = result.SetItem(
                target,
                value,
                MetadataProvenance.Mapped(alias, metadata.GetProvenance(alias).Location));
        }

        return SetSourceFormat(result, format, path, diagnostics);
    }

    /// <summary>
    /// 元のコンテンツ形式を記録する。
    /// </summary>
    /// <param name="metadata">記録先のメタデータ。</param>
    /// <param name="format">このコンテンツの形式。</param>
    /// <param name="path">診断に添えるコンテンツのパス。</param>
    /// <param name="diagnostics">診断の収集先。</param>
    /// <returns>形式が記録されたメタデータ。</returns>
    /// <remarks>
    /// <para>
    /// 出所は <see cref="MetadataProvenance.FromPath"/> である。形式はファイルの拡張子から
    /// 決まっており（<see cref="IContentFormatRegistry.TryGetFormat"/>）、内容には書かれていない。
    /// </para>
    /// <para>
    /// <strong>フロント マターに書かれた値があっても、パーサーが判定した形式で置き換える。</strong>
    /// <c>.md</c> のファイルに <c>source-format: asciidoc</c> と書かれていても、
    /// それは事実に反する。混在サイト（R1）でテンプレートがこのキーを見て表示を変える用途では、
    /// 事実と違う値が入っていると誰も原因を追えない。ただし黙って置き換えると
    /// 「書いたのに効かない」になるので記録する。
    /// </para>
    /// </remarks>
    private static DocumentMetadata SetSourceFormat(
        DocumentMetadata metadata,
        ContentFormat format,
        ContentPath path,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (format.IsEmpty)
        {
            // 形式が分からないなら記録しない。空文字列を書くと
            // 「形式が markdown ではない」と読めてしまう。
            return metadata;
        }

        var key = WellKnownMetadata.SourceFormat;

        if (metadata.TryGetValue(key, out var declared) &&
            !IsSameFormat(declared, format))
        {
            diagnostics.Add(new Diagnostic(
                DiagnosticIds.SourceFormatOverwritten,
                DiagnosticSeverity.Information,
                Messages.FormatSourceFormatOverwritten(
                    key.Value,
                    declared.IsScalar ? declared.AsScalar() : declared.Kind,
                    format.Value),
                path,
                metadata.GetProvenance(key).Location));
        }

        return metadata.SetItem(
            key,
            new MetadataValue.Scalar(format.Value),
            MetadataProvenance.FromPath);
    }

    /// <summary>
    /// 書かれていた値が実際の形式と一致するかどうかを判定する。
    /// </summary>
    /// <param name="declared">書かれていた値。</param>
    /// <param name="format">実際の形式。</param>
    /// <returns>一致する場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 一致する場合は置き換えても内容が変わらないので診断を出さない。
    /// 比較は <see cref="ContentFormat"/> と同じく <see cref="StringComparison.Ordinal"/> である。
    /// </remarks>
    private static bool IsSameFormat(
        MetadataValue declared,
        ContentFormat format)
    {
        return declared.IsScalar &&
            string.Equals(declared.AsScalar(), format.Value, StringComparison.Ordinal);
    }
}
