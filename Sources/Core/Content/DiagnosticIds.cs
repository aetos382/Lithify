namespace Lithify.Core.Content;

/// <summary>
/// <c>Lithify.Core</c> が報告する診断の識別子。
/// </summary>
/// <remarks>
/// <para>
/// 識別子は <c>LI</c> + 4 桁で、上位 1 桁がパッケージの番号帯である。
/// 帯を分けているのは、独立に配布されるパッケージが番号を衝突させずに
/// 診断を追加できるようにするためである。中央の一覧を持つとパッケージの追加が
/// <c>Lithify.Abstractions</c> の改版を要求することになり、
/// 「パーサーは誰でも追加できる」という建前と矛盾する。
/// </para>
/// <list type="table">
///   <listheader><term>帯</term><description>パッケージ</description></listheader>
///   <item><term><c>LI0xxx</c></term><description><c>Lithify.Abstractions</c>（契約の違反）</description></item>
///   <item><term><c>LI1xxx</c></term><description><c>Lithify.Core</c></description></item>
///   <item><term><c>LI2xxx</c></term><description><c>Lithify.Hosting</c></description></item>
///   <item><term><c>LI3xxx</c></term><description>パーサー（<c>Markdig</c> は <c>LI31xx</c>、<c>AdocNet</c> は <c>LI32xx</c>）</description></item>
///   <item><term><c>LI4xxx</c></term><description>レンダラー</description></item>
///   <item><term><c>LI5xxx</c></term><description>テンプレート エンジン</description></item>
///   <item><term><c>LI6xxx</c></term><description><c>Lithify.Blog</c></description></item>
///   <item><term><c>LI7xxx</c></term><description>ソース プロバイダ</description></item>
/// </list>
/// <para>
/// <see langword="internal"/> にしているのは、識別子を<em>抑制の鍵として</em>参照する機構
/// （MSBuild の <c>NoWarn</c> 相当）がまだ無いためである。それを入れる時点で公開する。
/// 利用者にとっての契約は識別子の文字列そのもの（ログに出る <c>LI1001</c>）であり、
/// 定数が公開されているかどうかとは別である。
/// </para>
/// </remarks>
internal static class DiagnosticIds
{
    /// <summary>
    /// 同じコンテンツ形式を扱うパーサーが複数登録され、後のものが先のものを置き換えた。
    /// </summary>
    public const string ParserOverridden = "LI1001";

    /// <summary>
    /// パーサーが <see langword="default"/> のコンテンツ形式を扱えると主張した。
    /// </summary>
    public const string ParserDeclaredEmptyFormat = "LI1002";
}
