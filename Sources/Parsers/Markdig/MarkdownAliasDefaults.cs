using Lithify.Abstractions;
using Lithify.Core.Metadata;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// Markdown のフロント マターについての別名の既定。
/// </summary>
/// <remarks>
/// <para>
/// <strong>空である。</strong> Markdown のフロント マターには仕様が定めたネイティブ名が無く
/// （フロント マター自体が CommonMark 仕様外の拡張である）、
/// <see cref="WellKnownMetadata"/> のキーの綴りがそのまま Lithify での正規の綴りになる。
/// 写すべき「その形式ではそう書く」という事実が存在しない。
/// </para>
/// <para>
/// <strong>他のジェネレーターの綴りは受けない。</strong> 当初は <c>lastmod</c> /
/// <c>last_modified_at</c> / <c>updated</c> / <c>summary</c> / <c>excerpt</c> / <c>authors</c> /
/// <c>language</c> / <c>template</c> を写していたが、撤回した。
/// ブログ用のメタデータ語彙として標準化された仕様は存在せず、あれらは
/// 既存ジェネレーターの慣行を借りただけである。標準があれば従うが、
/// 無いものに慣行で追随する理由は無い。Lithify では <c>last-modified</c> と書く。
/// </para>
/// <para>
/// <strong>それでも表が要るのは、<see cref="WellKnownMetadata"/> の綴りを
/// Lithify 自身の事情で変えたくなった場合のためである。</strong> そのときは新しい綴りを
/// 写し先にし、旧綴りを候補として並べる。過去に書かれたファイルを全て更新させないための機構であり、
/// 移行を促す診断は出さない（変えなくてよいように優先順があるのだから、
/// 変えろと言うなら優先順を設けた意味が無い）。
/// </para>
/// <para>
/// 利用者が自分の横断語彙を作る経路は残っている
/// （<see cref="MetadataAliasOptions"/>。<c>a.Description = ["abstract"]</c> のように書く）。
/// これは Lithify が既定で何を受けるかとは別の話である。
/// </para>
/// <para>
/// <strong>意味がずれるものは並べない。</strong> Jekyll の <c>published</c> は
/// <see cref="WellKnownMetadata.Draft"/> の否定であり、Hugo の <c>categories</c> は
/// <see cref="WellKnownMetadata.Tags"/> とは別の分類軸である。
/// 綴りの互換を追う方針であっても、これらは写せなかった。
/// </para>
/// </remarks>
internal static class MarkdownAliasDefaults
{
    /// <summary>
    /// 既定の別名の表を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 空の表である。<c>title</c> や <c>date</c> のように綴りが well-known キーと一致するものは、
    /// <see cref="MetadataKey.Create"/> の正規化を経た時点で既に一致しているので写す必要がない
    /// （<see cref="MetadataProvenance.Declared"/> のまま残る）。
    /// </para>
    /// <para>
    /// <see cref="MetadataAliasTable.Empty"/> をそのまま返さずこのプロパティを設けているのは、
    /// <see cref="MarkdigContentParser"/> が「このパッケージの既定」を参照する形を保つためである。
    /// 綴りの変更に伴う旧綴りの候補が生じたとき、変更はここだけで済む。
    /// </para>
    /// </remarks>
    public static MetadataAliasTable Table { get; } = MetadataAliasTable.Empty;
}
