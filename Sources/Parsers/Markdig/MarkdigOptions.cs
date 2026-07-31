using Lithify.Markdown.Abstractions;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// Markdig 固有の設定。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MarkdownOptions"/> との境界は「Markdown という<em>形式の仕様</em>が定める語彙か、
/// エンジンの都合か」である。ここに置くのは後者、すなわち Markdig という実装を
/// 差し替えたら意味を失う設定だけである。
/// </para>
/// <para>
/// このオプションを空にできないかは検討したが、<see cref="MaximumNestingDepth"/> は
/// 実装が持つ再帰の限界であって仕様の語彙ではなく、かつ既定値のままでは
/// 深く入れ子になった入力で <see cref="System.InsufficientExecutionStackException"/> を招く。
/// 形式の語彙に混ぜると Markdig 以外のパーサーが意味を与えられない設定が
/// <c>Lithify.Markdown.Abstractions</c> に残るので、こちらに置く。
/// </para>
/// </remarks>
public sealed class MarkdigOptions
{
    /// <summary>
    /// 入れ子の深さの上限を取得または設定する。
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> の場合は Markdig の既定に従う。
    /// 信頼できない入力を扱う場合に設定する。
    /// </remarks>
    public int? MaximumNestingDepth { get; set; }

    /// <summary>
    /// 見出しに ID を自動採番するかどうかを取得または設定する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 既定は無効。<strong>Lithify では見出し ID の生成規則をレンダラーに一元化する。</strong>
    /// アンカーの生成を各パーサーが行うと、Markdown と AsciiDoc が混在するサイト（R1）で
    /// 同じ見出し文字列に別の ID が付き、目次と本文のリンクが形式によって食い違う。
    /// </para>
    /// <para>
    /// したがって <see cref="Lithify.Abstractions.Ast.SectionNode.AnchorId"/> に値を入れるのは
    /// 明示的なアンカーが書かれていた場合だけである。これを有効にすると
    /// Markdig が自動採番した ID が明示的なアンカーと区別できなくなるので、
    /// 有効化は「レンダラーの規則より Markdig の規則を優先する」という明示的な選択になる。
    /// </para>
    /// </remarks>
    public bool AutoIdentifiers { get; set; }

    /// <summary>
    /// フロント マターを文書の途中でも認識するかどうかを取得または設定する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 既定は無効。<see cref="Lithify.Abstractions.IContentParser.ParseMetadataAsync"/> は
    /// <em>文書先頭のフロント マターだけを読む</em>ことを契約としており、
    /// これを有効にすると軽量パスと完全パースの結果が一致しなくなる。
    /// </para>
    /// <para>
    /// つまりこれは「契約を破ってもよいか」の設定であり、有効にした場合
    /// 文書途中のフロント マターは<em>メタデータとしては読まれない</em>
    /// （Markdig 側でブロックとして認識されるだけで、Lithify は先頭のものしか見ない）。
    /// </para>
    /// </remarks>
    public bool AllowFrontMatterInMiddleOfDocument { get; set; }
}
