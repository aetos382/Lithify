using System.Collections.Immutable;

namespace Lithify.Abstractions.Ast;

/// <summary>
/// リストの種類。
/// </summary>
public enum ListKind
{
    /// <summary>
    /// 箇条書き。
    /// </summary>
    Unordered,

    /// <summary>
    /// 番号付きリスト。
    /// </summary>
    Ordered,
}

/// <summary>
/// 強調の種類。
/// </summary>
public enum EmphasisKind
{
    /// <summary>
    /// 弱い強調。HTML の <c>em</c> に対応する。
    /// </summary>
    Emphasis,

    /// <summary>
    /// 強い強調。HTML の <c>strong</c> に対応する。
    /// </summary>
    Strong,

    /// <summary>
    /// 取り消し線。
    /// </summary>
    Strikethrough,

    /// <summary>
    /// 下付き。
    /// </summary>
    Subscript,

    /// <summary>
    /// 上付き。
    /// </summary>
    Superscript,

    /// <summary>
    /// 目立たせる強調。HTML の <c>mark</c> に対応する。
    /// </summary>
    Mark,
}

/// <summary>
/// 注意喚起の種類。
/// </summary>
/// <remarks>
/// AsciiDoc が定める 5 種類。GitHub の alert（<c>NOTE</c> / <c>TIP</c> /
/// <c>IMPORTANT</c> / <c>WARNING</c> / <c>CAUTION</c>）と一致するので、
/// 両形式をこの列挙で表現できる。
/// </remarks>
public enum AdmonitionKind
{
    /// <summary>
    /// 補足。
    /// </summary>
    Note,

    /// <summary>
    /// 助言。
    /// </summary>
    Tip,

    /// <summary>
    /// 重要事項。
    /// </summary>
    Important,

    /// <summary>
    /// 警告。
    /// </summary>
    Warning,

    /// <summary>
    /// 注意。
    /// </summary>
    Caution,
}

/// <summary>
/// 表の列の水平方向の配置。
/// </summary>
public enum ColumnAlignment
{
    /// <summary>
    /// 指定なし。
    /// </summary>
    None,

    /// <summary>
    /// 左寄せ。
    /// </summary>
    Left,

    /// <summary>
    /// 中央揃え。
    /// </summary>
    Center,

    /// <summary>
    /// 右寄せ。
    /// </summary>
    Right,
}

/// <summary>
/// 表の列の定義。
/// </summary>
/// <param name="Alignment">水平方向の配置。</param>
/// <param name="WidthRatio">
/// 列幅の比。指定がない場合は <see langword="null"/>。
/// AsciiDoc の <c>cols</c> 指定に対応する。
/// </param>
public readonly record struct TableColumn(
    ColumnAlignment Alignment,
    int? WidthRatio);

/// <summary>
/// 定義リストの項目。
/// </summary>
/// <param name="Terms">
/// 見出し語。複数持てるのは AsciiDoc と HTML がどちらも
/// 1 つの記述に複数の語を対応付けられるためである。
/// </param>
/// <param name="Blocks">記述の内容。</param>
public sealed record DescriptionListEntry(
    ImmutableArray<ImmutableArray<InlineNode>> Terms,
    ImmutableArray<BlockNode> Blocks);

/// <summary>
/// 引用の出典。
/// </summary>
/// <param name="Author">引用元の著者。</param>
/// <param name="Source">引用元の作品名や出典。指定がない場合は <see langword="null"/>。</param>
public sealed record Attribution(
    string Author,
    string? Source);

/// <summary>
/// 形式固有の追加属性。
/// </summary>
/// <param name="Name">属性の名前。</param>
/// <param name="Value">属性の値。値を持たない属性では <see langword="null"/>。</param>
/// <remarks>
/// 共通 AST に昇格させるほど汎用でない指定（コード ブロックの強調行、
/// AsciiDoc のブロック属性など）を保持する。レンダラーは解釈できない属性を無視する。
/// </remarks>
public readonly record struct AttributeEntry(
    string Name,
    string? Value);
