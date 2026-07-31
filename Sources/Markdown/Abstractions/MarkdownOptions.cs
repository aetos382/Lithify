namespace Lithify.Markdown.Abstractions;

/// <summary>
/// Markdown の解釈に関する設定。
/// </summary>
/// <remarks>
/// <para>
/// ここに置くのは Markdown という<b>形式の仕様</b>が定める語彙だけである。
/// エンジン固有の都合（Markdig のどの拡張を有効にするか、どのパイプライン設定を使うか）は
/// 実装パッケージ側のオプションに置く。判断の基準は「仕様が定める語彙か、エンジンの都合か」である。
/// </para>
/// <para>
/// この分離があるため、Markdig を参照しない共有テーマ パッケージが
/// <c>Configure&lt;MarkdownOptions&gt;(o =&gt; o.Footnotes = true)</c> のように
/// 形式の挙動だけを設定できる。エンジンを差し替えても設定は書き換えなくてよい。
/// </para>
/// </remarks>
public sealed class MarkdownOptions
{
    /// <summary>
    /// 方言を取得または設定する。
    /// </summary>
    /// <remarks>
    /// 既定は <see cref="MarkdownFlavor.GitHubFlavored"/>。
    /// ブログの記事として書かれた Markdown はほぼ GFM を前提としており、
    /// 表や取り消し線が動かないほうが驚きが大きい。
    /// </remarks>
    public MarkdownFlavor Flavor { get; set; } = MarkdownFlavor.GitHubFlavored;

    /// <summary>
    /// GFM の表を有効にするかどうかを取得または設定する。
    /// </summary>
    public bool Tables { get; set; } = true;

    /// <summary>
    /// GFM の取り消し線（<c>~~text~~</c>）を有効にするかどうかを取得または設定する。
    /// </summary>
    public bool Strikethrough { get; set; } = true;

    /// <summary>
    /// GFM のタスク リスト（<c>- [ ]</c>）を有効にするかどうかを取得または設定する。
    /// </summary>
    public bool TaskLists { get; set; } = true;

    /// <summary>
    /// GFM の自動リンクを有効にするかどうかを取得または設定する。
    /// </summary>
    public bool Autolinks { get; set; } = true;

    /// <summary>
    /// 脚注を有効にするかどうかを取得または設定する。
    /// </summary>
    /// <remarks>
    /// 既定は無効。脚注は CommonMark にも GFM にも含まれない拡張であり、
    /// 記法が実装によって異なる。有効化を明示的な選択にしている。
    /// </remarks>
    public bool Footnotes { get; set; }

    /// <summary>
    /// 単一の改行を <c>&lt;br&gt;</c> として扱うかどうかを取得または設定する。
    /// </summary>
    /// <remarks>
    /// 既定は無効（仕様どおり単一の改行は空白になる）。
    /// </remarks>
    public bool HardLineBreaks { get; set; }

    /// <summary>
    /// 属性記法（<c>## 見出し {#anchor}</c>）を有効にするかどうかを取得または設定する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 既定は無効。<see cref="Footnotes"/> と同じく CommonMark にも GFM にも含まれない拡張であり、
    /// 記法が実装によって異なる（Pandoc の <c>{#id}</c> と kramdown の <c>{: #id}</c>）。
    /// </para>
    /// <para>
    /// <strong>これが無効の間、Markdown の見出しは明示的なアンカーを持てない。</strong>
    /// <c>{#anchor}</c> は見出しの文字列の一部として読まれ、
    /// <see cref="Lithify.Abstractions.Ast.SectionNode.AnchorId"/> は
    /// 常に <see langword="null"/> になる（アンカーはレンダラーが見出し文字列から生成する）。
    /// AsciiDoc の <c>[[id]]</c> に相当するものを Markdown で書きたい場合に有効にする。
    /// </para>
    /// </remarks>
    public bool Attributes { get; set; }
}
