using System.Collections.Immutable;

namespace Lithify.Abstractions.Ast;

/// <summary>
/// 素のテキスト。
/// </summary>
/// <param name="Text">テキストの内容。</param>
/// <remarks>
/// エスケープされていない生のテキストを保持する。HTML エスケープはレンダラーの責務。
/// </remarks>
public sealed record TextNode(
    string Text) : InlineNode;

/// <summary>
/// 強調。
/// </summary>
/// <param name="Kind">強調の種類。</param>
/// <param name="Inlines">強調される内容。</param>
public sealed record EmphasisNode(
    EmphasisKind Kind,
    ImmutableArray<InlineNode> Inlines) : InlineNode;

/// <summary>
/// リンク。
/// </summary>
/// <param name="Target">参照先。</param>
/// <param name="Inlines">リンク テキスト。空の場合、レンダラーは参照先から表示文字列を導出する。</param>
/// <param name="Title">補助的な説明。指定がない場合は <see langword="null"/>。</param>
/// <remarks>
/// このノードの <see cref="Target"/> を全 AST から走査することで双方向リンクの
/// 逆インデックスが構築される。記法に依存しない逆引きが成立するのはそのため。
/// </remarks>
public sealed record LinkNode(
    LinkTarget Target,
    ImmutableArray<InlineNode> Inlines,
    string? Title) : InlineNode;

/// <summary>
/// 画像。
/// </summary>
/// <param name="Target">画像の位置。</param>
/// <param name="AltText">代替テキスト。</param>
/// <param name="Title">補助的な説明。指定がない場合は <see langword="null"/>。</param>
/// <remarks>
/// <see cref="AltText"/> を <see langword="null"/> 許容にしていないのは、
/// 代替テキストの欠落をアクセシビリティの診断として報告できるようにするためである
/// （空文字列は「装飾画像であり代替テキスト不要」という明示的な意思表示として扱える）。
/// </remarks>
public sealed record ImageNode(
    LinkTarget Target,
    string AltText,
    string? Title) : InlineNode;

/// <summary>
/// 行内のコード。
/// </summary>
/// <param name="Text">コードの内容。</param>
public sealed record CodeSpanNode(
    string Text) : InlineNode;

/// <summary>
/// 脚注への参照。
/// </summary>
/// <param name="Id">参照する <see cref="FootnoteDefinitionNode"/> の識別子。</param>
public sealed record FootnoteReferenceNode(
    string Id) : InlineNode;

/// <summary>
/// 改行。
/// </summary>
/// <param name="IsHard">
/// 出力に明示的な改行を残すかどうか。偽の場合は単なる空白として扱われる。
/// </param>
public sealed record LineBreakNode(
    bool IsHard) : InlineNode;

/// <summary>
/// 共通 AST で表現できない出力形式固有のインライン要素。
/// </summary>
/// <param name="Format">この内容が意味を持つ出力形式（<c>html</c> 等）。</param>
/// <param name="Text">そのまま出力される本文。</param>
/// <remarks>
/// <see cref="RawBlockNode"/> のインライン版。エスケープされずに出力されるので、
/// 信頼できない入力をここに入れてはならない。
/// </remarks>
public sealed record RawInlineNode(
    string Format,
    string Text) : InlineNode;
