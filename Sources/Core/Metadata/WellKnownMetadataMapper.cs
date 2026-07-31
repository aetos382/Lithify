using System;
using System.Collections.Immutable;

using Lithify.Abstractions;

namespace Lithify.Core.Metadata;

/// <summary>
/// 形式ごとのネイティブな名前を <see cref="WellKnownMetadata"/> のキーに写す。
/// </summary>
/// <remarks>
/// <para>
/// <strong>この写しはパーサーの責務である。</strong> 消費者（<c>Lithify.Blog</c> 等）側で
/// 「<c>lastmod</c> か <c>revdate</c> か」を分岐させると、
/// 消費者が各形式・各既存ジェネレーターの語彙を知る必要が生じる。
/// </para>
/// <para>
/// <strong>写しの規則はここに 1 つ持ち、語彙は <see cref="MetadataAliasTable"/> で受ける。</strong>
/// 規則（明示的に書かれた値が別名に負けない、先に並んだ別名が勝つ、競合を診断する）は
/// 形式に依らないが、語彙は形式ごとに違う。パーサーごとに規則を書くと、
/// Markdown と AsciiDoc で「明示的に書いた値が勝つかどうか」が食い違いうる。
/// </para>
/// <para>
/// <strong>元の名前は残る。</strong> 写しは
/// <see cref="DocumentMetadata.SetItem(MetadataKey, MetadataValue, MetadataProvenance)"/> による<em>追加</em>であり、
/// 写し元の項目はそのまま <see cref="DocumentMetadata.Entries"/> に残る。
/// 写した項目には <see cref="MetadataProvenance.Mapped"/> が付き、写し元のキーと位置を辿れる。
/// </para>
/// </remarks>
public static class WellKnownMetadataMapper
{
    /// <summary>
    /// well-known キーを生やしたメタデータを返す。
    /// </summary>
    /// <param name="metadata">パーサーが読み取ったメタデータ。</param>
    /// <param name="aliases">写しに用いる別名の表。</param>
    /// <param name="format">このコンテンツの形式。</param>
    /// <param name="path">診断に添えるコンテンツのパス。</param>
    /// <param name="diagnostics">診断の収集先。</param>
    /// <returns>well-known キーが生えたメタデータ。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="metadata"/>、<paramref name="aliases"/>、
    /// または <paramref name="diagnostics"/> が <see langword="null"/> である。
    /// </exception>
    /// <remarks>
    /// ネイティブ名が well-known キーと一致するもの（Markdown の <c>title</c>、
    /// AsciiDoc の <c>:description:</c>）は、正規化の時点で既に一致しているので
    /// 別名の表に載せる必要がない（<see cref="MetadataProvenance.Declared"/> のまま残る）。
    /// </remarks>
    public static DocumentMetadata Map(
        DocumentMetadata metadata,
        MetadataAliasTable aliases,
        ContentFormat format,
        ContentPath path,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(aliases);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var result = metadata;

        foreach (var (target, candidates) in aliases.Targets)
        {
            // 写し先が明示的に書かれている場合は写さない。書かれたものが別名に負けてはならない。
            // この場合は競合ではないので診断も出さない（別名を書くのは自然なことである）。
            if (metadata.Entries.ContainsKey(target))
            {
                continue;
            }

            var winner = default(MetadataKey);

            foreach (var candidate in candidates)
            {
                if (!metadata.TryGetValue(candidate, out var value))
                {
                    continue;
                }

                if (!winner.IsEmpty)
                {
                    // 既に採用済みの候補がある。2 つ目以降は写さないが、
                    // 黙って落とすと「書いたのに効かない」になるので記録する。
                    diagnostics.Add(new Diagnostic(
                        DiagnosticIds.WellKnownKeyAmbiguous,
                        DiagnosticSeverity.Warning,
                        Messages.FormatWellKnownKeyAmbiguous(
                            winner.Value,
                            candidate.Value,
                            target.Value),
                        path,
                        metadata.GetProvenance(candidate).Location));

                    continue;
                }

                winner = candidate;

                // 位置は写し元の位置。診断は「その別名を書いた行」に向けるべきである。
                result = result.SetItem(
                    target,
                    value,
                    MetadataProvenance.Mapped(candidate, metadata.GetProvenance(candidate).Location));
            }
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
    /// <strong>内容に書かれた値があっても、パーサーが判定した形式で置き換える。</strong>
    /// <c>.md</c> のファイルに <c>source-format: asciidoc</c> と書かれていても、
    /// それは事実に反する。混在サイト（R1）でテンプレートがこのキーを見て表示を変える用途では、
    /// 事実と違う値が入っていると誰も原因を追えない。ただし黙って置き換えると
    /// 「書いたのに効かない」になるので記録する。
    /// </para>
    /// <para>
    /// このキーは別名の表を通さない。写しではなくパーサーが知っている事実の記録であり、
    /// 利用者が別名を設定する対象ではない。
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
