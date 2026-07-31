using Lithify.Abstractions;
using Lithify.Core.Metadata;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// Markdown のフロント マターで広く使われている別名の既定。
/// </summary>
/// <remarks>
/// <para>
/// <strong>AsciiDoc と違い、これは仕様ではない。</strong> Markdown のフロント マターには
/// 仕様が定めたネイティブ名が無く（フロント マター自体が仕様外の拡張である）、
/// ここに並ぶのは既存の静的サイト ジェネレーターが広く使っている綴りにすぎない。
/// AsciiDoc の <c>doctitle</c> / <c>revdate</c> が「その形式ではそう書く」という事実であるのに対し、
/// こちらは慣行の借用である。
/// </para>
/// <para>
/// その違いは<em>利用者が置き換えたとき</em>に表れる。AsciiDoc の既定を置き換えるのは
/// 仕様が定めた属性を読まないという宣言だが、Markdown の既定を置き換えるのは
/// 借りてきた慣行を降ろすだけである。
/// </para>
/// <para>
/// <strong>綴りが衝突したら AsciiDoc が勝つ。</strong> Lithify は Hugo を踏襲すると決めていない。
/// 同じ綴りが両形式で別の意味を持つなら、仕様が定めているほうを採り、
/// こちらは別の綴りに譲る（<c>WellKnownMetadata</c> のキーの綴り自体を見直す場合もある）。
/// 慣行の借用が仕様に勝つ理由は無い。
/// </para>
/// <para>
/// <strong>意味がずれるものは並べない。</strong> Jekyll の <c>published</c> は
/// <see cref="WellKnownMetadata.Draft"/> の否定であり、Hugo の <c>categories</c> は
/// <see cref="WellKnownMetadata.Tags"/> とは別の分類軸である。
/// 推測で写すと、書いた覚えのない値が効いている状態になる。
/// </para>
/// </remarks>
internal static class MarkdownAliasDefaults
{
    /// <summary>
    /// 既定の別名の表を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 各写し先の候補は優先順に並ぶ。<c>lastmod</c> を <c>last_modified_at</c> より前に置いているのは
    /// Hugo の綴りを優先するという判断である。
    /// </para>
    /// <para>
    /// <c>title</c> や <c>date</c> のように綴りが well-known キーと一致するものは並べない。
    /// <see cref="MetadataKey.Create"/> の正規化を経た時点で既に一致しており、
    /// 写す必要がないためである（<see cref="MetadataProvenance.Declared"/> のまま残る）。
    /// キーの正規化は <c>_</c> を <c>-</c> にするので、Jekyll の <c>last_modified_at</c> は
    /// <c>last-modified-at</c> として一致する。
    /// </para>
    /// </remarks>
    public static MetadataAliasTable Table { get; } = MetadataAliasTable.Empty
        .Set(WellKnownMetadata.LastModified, "lastmod", "last_modified_at", "updated")
        .Set(WellKnownMetadata.Description, "summary", "excerpt")
        .Set(WellKnownMetadata.Author, "authors")
        .Set(WellKnownMetadata.Language, "language")
        .Set(WellKnownMetadata.Layout, "template");
}
