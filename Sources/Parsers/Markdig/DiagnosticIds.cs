namespace Lithify.Parsers.Markdig;

/// <summary>
/// <c>Lithify.Parsers.Markdig</c> が報告する診断の識別子。
/// </summary>
/// <remarks>
/// <para>
/// パーサーの番号帯は <c>LI3xxx</c> で、その中の <c>LI31xx</c> がこのパッケージである。
/// 帯を分けているので、パッケージを追加しても <c>Lithify.Abstractions</c> の改版は要らない。
/// </para>
/// <para>
/// <see langword="internal"/> にしている理由は <c>Lithify.Core</c> 側の同名の型と同じで、
/// 識別子を<em>抑制の鍵として</em>参照する機構がまだ無いためである。
/// 利用者にとっての契約はログに出る文字列そのものである。
/// 同じ理由で、その機構が入るまでは番号を詰め直してよい。
/// </para>
/// </remarks>
internal static class DiagnosticIds
{
    /// <summary>
    /// フロント マターが YAML として解釈できなかった。
    /// </summary>
    public const string FrontMatterNotWellFormed = "LI3101";

    /// <summary>
    /// フロント マターの最上位がマッピングではなかった。
    /// </summary>
    public const string FrontMatterNotMapping = "LI3102";

    /// <summary>
    /// フロント マターのキーがスカラーではなかった。
    /// </summary>
    public const string FrontMatterKeyNotScalar = "LI3103";

    /// <summary>
    /// 正規化の結果、フロント マターのキーが衝突した。
    /// </summary>
    public const string FrontMatterDuplicateKey = "LI3104";

    /// <summary>
    /// フロント マターのキーが空だった。
    /// </summary>
    public const string FrontMatterEmptyKey = "LI3105";

    /// <summary>
    /// フロント マターに自身を含む参照があり、値を展開できなかった。
    /// </summary>
    public const string FrontMatterRecursiveAlias = "LI3106";

    /// <summary>
    /// リンクの参照先を URI としてもサイト内のパスとしても解釈できなかった。
    /// </summary>
    public const string LinkTargetNotResolvable = "LI3107";

    /// <summary>
    /// Markdig のブロックを共通 AST に写せなかった。
    /// </summary>
    /// <remarks>
    /// パイプラインを組み立てているのは Lithify 自身なので、通常は起こらない。
    /// 拡張が追加されたときに<em>内容が黙って消えない</em>ようにするための診断である。
    /// </remarks>
    public const string BlockNotRepresentable = "LI3108";
}
