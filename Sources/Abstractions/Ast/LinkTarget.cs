using System;

namespace Lithify.Abstractions.Ast;

/// <summary>
/// リンクの参照先。
/// </summary>
/// <remarks>
/// <para>
/// パーサーはリンクを<em>解決しない</em>。サイト全体を知らないため、解決できるのは
/// 「外部 URI か、サイト内の相対パスか、識別子による参照か」の区別までである。
/// 実際の出力 URL への写像はサイト全体を知っている段階で行われる。
/// </para>
/// <para>
/// 双方向リンク（R7）はこの型が鍵になる。全 AST から <see cref="Internal"/> と
/// <see cref="Reference"/> を走査して逆インデックスを構築するので、
/// 逆引きは記法に依存しない（Markdown の <c>[](../foo.md)</c> と AsciiDoc の
/// <c>xref:foo[]</c> が同じ仕組みで扱える）。
/// </para>
/// </remarks>
// 派生型を入れ子にすることで閉じた階層を表現している。外に出すと利用者が任意の派生型を
// 追加できてしまい、網羅的なパターン マッチが成立しなくなる。
#pragma warning disable CA1034 // Nested types should not be visible
public abstract record LinkTarget
{
    private LinkTarget()
    {
    }

    /// <summary>
    /// サイト外への絶対 URI。
    /// </summary>
    /// <param name="Uri">参照先の URI。</param>
    public sealed record External(Uri Uri) : LinkTarget;

    /// <summary>
    /// サイト内のコンテンツへの参照。
    /// </summary>
    /// <param name="Path">参照先のコンテンツ パス。</param>
    /// <param name="Fragment">参照先の文書内アンカー。指定がない場合は <see langword="null"/>。</param>
    /// <remarks>
    /// Markdown の相対リンクに対応する。<see cref="Path"/> はソース ファイルのパスであって
    /// 出力パスではない（permalink の規則を知っているのはサイト全体を知る段階のみ）。
    /// </remarks>
    // CA1724 は System.Deployment.Internal 名前空間との競合を指摘するが、
    // これは LinkTarget の入れ子型であり、実際に曖昧になる場面はない。
#pragma warning disable CA1724 // Type names should not match namespaces
    public sealed record Internal(ContentPath Path, string? Fragment) : LinkTarget;
#pragma warning restore CA1724

    /// <summary>
    /// 識別子によるサイト内参照。
    /// </summary>
    /// <param name="Id">参照先の識別子。</param>
    /// <param name="Fragment">参照先の文書内アンカー。指定がない場合は <see langword="null"/>。</param>
    /// <remarks>
    /// AsciiDoc の <c>xref:</c> や wiki 風のリンクに対応する。
    /// パスではなく識別子で参照するので、ファイルを移動しても壊れない。
    /// </remarks>
    public sealed record Reference(string Id, string? Fragment) : LinkTarget;

    /// <summary>
    /// 解決できなかった参照。
    /// </summary>
    /// <param name="Raw">元の記述。</param>
    /// <remarks>
    /// パーサーが分類できなかった参照をそのまま保持する。
    /// 破棄せず残すのは、レンダラーが元の記述をそのまま出力に通せるようにするためと、
    /// リンク切れの診断で元の記述を提示できるようにするためである。
    /// </remarks>
    public sealed record Unresolved(string Raw) : LinkTarget;
}
#pragma warning restore CA1034
