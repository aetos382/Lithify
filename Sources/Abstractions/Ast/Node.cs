namespace Lithify.Abstractions.Ast;

/// <summary>
/// 共通 AST のノード。
/// </summary>
/// <remarks>
/// <para>
/// Pandoc の AST を参考にした言語非依存の中間表現。すべてのパーサーがこの表現を出力し、
/// レンダラーは 1 つだけ存在する。これにより 1 つのサイトの中で Markdown と AsciiDoc を
/// 混在させても、見出し ID の生成規則や脚注の出力が揃う。
/// </para>
/// <para>
/// 設計上の制約として <em>AsciiDoc の構造を表現できること</em>を要求している。
/// <see cref="AdmonitionNode"/> と <see cref="DescriptionListNode"/>、
/// <see cref="LinkTarget.Reference"/>（xref）はそのために存在する。
/// </para>
/// <para>
/// 階層は閉じている（外部で派生型を追加できない）。網羅的なパターン マッチを成立させるためであり、
/// 表現できない構造は <see cref="RawBlockNode"/> / <see cref="RawInlineNode"/> で逃がす。
/// </para>
/// </remarks>
public abstract record Node
{
    internal Node()
    {
    }
}

/// <summary>
/// ブロック レベルのノード。
/// </summary>
public abstract record BlockNode : Node
{
    internal BlockNode()
    {
    }
}

/// <summary>
/// インライン レベルのノード。
/// </summary>
public abstract record InlineNode : Node
{
    internal InlineNode()
    {
    }
}
