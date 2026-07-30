using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Lithify.Abstractions.Ast;

namespace Lithify.Abstractions;

/// <summary>
/// コンテンツを共通 AST に変換する。
/// </summary>
/// <remarks>
/// <para>
/// フロントマターという<em>具体</em>はこの抽象に現れない。フロントマターは CommonMark 仕様に無く
/// Markdig の拡張であり、AsciiDoc には存在しない（document attributes を使う）。
/// したがって抽象はメタデータの<em>モデル</em>（<see cref="DocumentMetadata"/>）に置き、
/// 供給手段は各パーサーの責務とする。
/// </para>
/// <para>
/// well-known メタデータの形式差は<em>各パーサーが吸収する</em>。
/// YAML の <c>date</c> に対し AsciiDoc は <c>revdate</c>、<c>title</c> に対し <c>doctitle</c> である。
/// これを <c>Lithify.Blog</c> 側で分岐させると Blog が AsciiDoc の語彙を知る必要が生じるので、
/// 各パーサーが自形式のネイティブ名を <see cref="WellKnownMetadata"/> のキーに写す。
/// 元の名前も保持したまま追加で生やすので情報は失われない。
/// </para>
/// </remarks>
public interface IContentParser
{
    /// <summary>
    /// このパーサーが扱えるコンテンツ形式を取得する。
    /// </summary>
    /// <remarks>
    /// 単数の <c>Format</c> にしないのは、1 パーサー = 1 形式を強制すると
    /// 複数形式を扱うパーサー（Pandoc ブリッジ等）が表現できなくなるためである。
    /// </remarks>
    ImmutableArray<ContentFormat> SupportedFormats { get; }

    /// <summary>
    /// 文書先頭のヘッダーのみを読み、メタデータを得る。
    /// </summary>
    /// <param name="source">対象のコンテンツ。</param>
    /// <param name="cancellationToken">取り消しトークン。</param>
    /// <returns>文書のメタデータ。</returns>
    /// <remarks>
    /// <para>
    /// サイト横断インデックス（タグ一覧 / 月別アーカイブ）の構築に使う軽量パス。
    /// これがないと「1 ページ表示するために全記事を完全パースする」ことになり、
    /// オンデマンド ビルドの利点が消える。どちらの形式も文書先頭だけ読めば済むので安い。
    /// </para>
    /// <para>
    /// 結果は <c>ParseAsync(source).Document.Metadata</c> と<em>必ず一致しなければならない</em>。
    /// これは契約テストで検証される。
    /// </para>
    /// </remarks>
    ValueTask<DocumentMetadata> ParseMetadataAsync(
        ContentSource source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// コンテンツを共通 AST に変換する。
    /// </summary>
    /// <param name="source">対象のコンテンツ。</param>
    /// <param name="cancellationToken">取り消しトークン。</param>
    /// <returns>パース結果。</returns>
    /// <remarks>
    /// 構文の誤りは例外ではなく <see cref="ParseResult.Diagnostics"/> で返す。
    /// 1 つの誤りで止めるより、見つかった誤りをすべて集めて提示したほうが有用である。
    /// </remarks>
    ValueTask<ParseResult> ParseAsync(
        ContentSource source,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// パースの結果。
/// </summary>
/// <param name="Document">生成された共通 AST。</param>
/// <param name="Diagnostics">パース中に報告された診断。</param>
/// <remarks>
/// 診断があっても <see cref="Document"/> は有効である（回復可能な誤りは回復して継続する）。
/// ビルドを中止すべきかどうかは <see cref="Diagnostic.IsError"/> で判断する。
/// </remarks>
public sealed record ParseResult(
    DocumentNode Document,
    ImmutableArray<Diagnostic> Diagnostics)
{
    /// <summary>
    /// 診断のない <see cref="ParseResult"/> を生成する。
    /// </summary>
    /// <param name="document">生成された共通 AST。</param>
    public ParseResult(
        DocumentNode document)
        : this(document, [])
    {
    }
}
