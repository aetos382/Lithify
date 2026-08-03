using System;

using Lithify.Abstractions;

namespace Lithify.Core.Metadata;

/// <summary>
/// 形式ごとのネイティブな名前を <see cref="WellKnownMetadata"/> のキーに写す。
/// </summary>
/// <remarks>
/// <para>
/// <strong>この写しはパーサーの責務である。</strong> 消費者（<c>Lithify.Blog</c> 等）側で
/// 「<c>date</c> か <c>revdate</c> か」を分岐させると、
/// 消費者が各形式の語彙を知る必要が生じる。
/// </para>
/// <para>
/// <strong>写しの規則はここに 1 つ持ち、語彙は <see cref="MetadataAliasTable"/> で受ける。</strong>
/// 規則（明示的に書かれた値が別名に負けない、先に並んだ別名が勝つ）は
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
    /// <returns>well-known キーが生えたメタデータ。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="metadata"/> または <paramref name="aliases"/> が
    /// <see langword="null"/> である。
    /// </exception>
    /// <remarks>
    /// <para>
    /// ネイティブ名が well-known キーと一致するもの（Markdown の <c>title</c>、
    /// AsciiDoc の <c>:description:</c>）は、正規化の時点で既に一致しているので
    /// 別名の表に載せる必要がない（<see cref="MetadataProvenance.Declared"/> のまま残る）。
    /// </para>
    /// <para>
    /// <strong>診断を出さない。</strong> この写しで起こりうる曖昧さ（複数の候補が書かれている、
    /// 別名と写し先の両方が書かれている）は、いずれも候補列に宣言された優先順で決まっており、
    /// 書いた人が何かを直さなくても結果は正しい。それを報告する診断は
    /// 「直さなくてもよいことを報告する」ものになる。
    /// </para>
    /// </remarks>
    public static DocumentMetadata Map(
        DocumentMetadata metadata,
        MetadataAliasTable aliases,
        ContentFormat format)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(aliases);

        var result = metadata;

        foreach (var (target, candidates) in aliases.Targets)
        {
            // 写し先が明示的に書かれている場合は写さない。書かれたものが別名に負けてはならない。
            // これは候補列の外側にある暗黙の第 0 位として振る舞うので、
            // 候補列に写し先自身を並べてその位置で優先順に参加させることはできない
            // （表に恒等写像を明示するかどうかは未決。docs/open-questions.md の項目 1）。
            if (metadata.Entries.ContainsKey(target))
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                if (!metadata.TryGetValue(candidate, out var value))
                {
                    continue;
                }

                // 位置は写し元の位置。診断は「その別名を書いた行」に向けるべきである。
                result = result.SetItem(
                    target,
                    value,
                    MetadataProvenance.Mapped(candidate, metadata.GetProvenance(candidate).Location));

                // 先に並んだ候補が勝つ。後の候補は見ない。候補列に優先順が宣言されている以上、
                // 複数書かれていても結果は決まっており、書いた人が直さなくても正しい。
                break;
            }
        }

        return SetSourceFormat(result, format);
    }

    /// <summary>
    /// 元のコンテンツ形式を記録する。
    /// </summary>
    /// <param name="metadata">記録先のメタデータ。</param>
    /// <param name="format">このコンテンツの形式。</param>
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
    /// 事実と違う値が入っていると誰も原因を追えない。
    /// </para>
    /// <para>
    /// このキーは別名の表を通さない。写しではなくパーサーが知っている事実の記録であり、
    /// 利用者が別名を設定する対象ではない。
    /// </para>
    /// </remarks>
    private static DocumentMetadata SetSourceFormat(
        DocumentMetadata metadata,
        ContentFormat format)
    {
        if (format.IsEmpty)
        {
            // 形式が分からないなら記録しない。空文字列を書くと
            // 「形式が markdown ではない」と読めてしまう。
            return metadata;
        }

        return metadata.SetItem(
            WellKnownMetadata.SourceFormat,
            new MetadataValue.Scalar(format.Value),
            MetadataProvenance.FromPath);
    }
}
