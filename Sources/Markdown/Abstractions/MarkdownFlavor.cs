namespace Lithify.Markdown.Abstractions;

/// <summary>
/// Markdown の方言。
/// </summary>
/// <remarks>
/// <see langword="enum"/> でよいのは、これが「仕様として存在する方言」の列挙であって
/// 実装が増えるたびに増えるものではないためである。方言そのものが増えることは
/// パーサーの実装が増えることよりはるかに稀で、そのときは <c>Lithify.Markdown.Abstractions</c> の
/// 改版が妥当である。
/// </remarks>
public enum MarkdownFlavor
{
    /// <summary>
    /// CommonMark 仕様に厳密に従う。
    /// </summary>
    CommonMark,

    /// <summary>
    /// GitHub Flavored Markdown 仕様に従う。
    /// </summary>
    GitHubFlavored,
}
