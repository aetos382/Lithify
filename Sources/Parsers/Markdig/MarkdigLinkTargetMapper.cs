using System;

using JetBrains.Annotations;

using Lithify.Abstractions;
using Lithify.Abstractions.Ast;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// Markdown のリンクの記述を <see cref="LinkTarget"/> に分類する。
/// </summary>
/// <remarks>
/// <para>
/// <strong>ここで行うのは分類だけで、解決はしない。</strong> パーサーはサイト全体を知らないので、
/// <c>../x.md</c> が実在するかも、それがどの URL に出力されるかも判断できない。
/// 判断できるのは「外部 URI か、サイト内の相対パスか、同一文書内のアンカーか」までである。
/// </para>
/// <para>
/// 分類が純粋関数として独立しているのは、これが最も規則が細かく、
/// かつパーサーを動かさずに検証できる部分だからである。
/// </para>
/// </remarks>
internal static class MarkdigLinkTargetMapper
{
    /// <summary>
    /// リンクの記述を分類する。
    /// </summary>
    /// <param name="url">リンクに書かれた文字列。</param>
    /// <param name="documentPath">このリンクが書かれている文書のパス。</param>
    /// <returns>分類された参照先。</returns>
    /// <remarks>
    /// <para>
    /// 判定の順序は「空 → 同一文書内アンカー → 絶対 URI → サイト ルート相対 → 文書相対」である。
    /// 絶対 URI をサイト内パスより先に見るのは、<c>https://</c> を含む文字列を
    /// <see cref="ContentPath"/> のコンストラクターに渡すと <c>:</c> を含むディレクトリ名として
    /// 扱われてしまうためである。
    /// </para>
    /// <para>
    /// <c>mailto:</c> や <c>tel:</c> も <see cref="LinkTarget.External"/> になる。
    /// スキームで絞らないのは、<see cref="ContentPath.Remote"/> がスキームを検査しないのと同じ理由
    /// （どのスキームが意味を持つかを判断する根拠がここに無い）である。
    /// </para>
    /// </remarks>
    [Pure]
    public static LinkTarget Map(
        string? url,
        ContentPath documentPath)
    {
        if (string.IsNullOrEmpty(url))
        {
            // `[a]()` は記法としては成立するが参照先が無い。
            // Unresolved にして元の記述（空文字列）を残す。
            return new LinkTarget.Unresolved(string.Empty);
        }

        if (url[0] == '#')
        {
            // 同一文書内のアンカー。参照先の文書はこの文書自身なので、
            // パスは documentPath をそのまま使う。
            return new LinkTarget.Internal(documentPath, url[1..]);
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) &&
            !absolute.IsFile &&
            !absolute.IsUnc)
        {
            return new LinkTarget.External(absolute);
        }

        return MapRelative(url, documentPath);
    }

    /// <summary>
    /// 絶対 URI でない参照先を分類する。
    /// </summary>
    /// <param name="url">リンクに書かれた文字列。</param>
    /// <param name="documentPath">このリンクが書かれている文書のパス。</param>
    /// <returns>分類された参照先。</returns>
    /// <remarks>
    /// <para>
    /// サイト ルート相対（<c>/posts/x.md</c>）と文書相対（<c>../x.md</c>）を分ける。
    /// 前者は先頭の <c>/</c> を落としてそのままパスにし、後者は文書のディレクトリからの結合になる。
    /// </para>
    /// <para>
    /// <strong>文書がローカルでない場合は結合できない。</strong>
    /// リモートの相対参照解決は RFC 3986 の規則であってパス セグメントの結合ではなく、
    /// それを知っているのは <see cref="IContentSourceProvider.TryResolveReference"/> である。
    /// ここでは <see cref="LinkTarget.Unresolved"/> にして元の記述を残し、
    /// サイト全体を知る段階に判断を委ねる。
    /// </para>
    /// </remarks>
    [Pure]
    private static LinkTarget MapRelative(
        string url,
        ContentPath documentPath)
    {
        var (reference, fragment) = SplitFragment(url);

        if (reference.Length == 0)
        {
            // `#` の前が空。`#anchor` は先に処理されているので、
            // ここに来るのは `?query#anchor` のような形だけである。
            return new LinkTarget.Unresolved(url);
        }

        try
        {
            if (reference[0] == '/')
            {
                return new LinkTarget.Internal(new ContentPath(reference[1..]), fragment);
            }

            if (!documentPath.IsLocal)
            {
                return new LinkTarget.Unresolved(url);
            }

            return new LinkTarget.Internal(documentPath.Directory.Combine(reference), fragment);
        }
        catch (ArgumentException)
        {
            // サイト ルートの外に出る参照（`../../../etc/passwd`）や、
            // パスとして成立しない文字列。元の記述を残して診断に使えるようにする。
            return new LinkTarget.Unresolved(url);
        }
    }

    /// <summary>
    /// 参照先をパス部分とフラグメントに分ける。
    /// </summary>
    /// <param name="url">リンクに書かれた文字列。</param>
    /// <returns>パス部分とフラグメント。フラグメントが無い場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// 最初の <c>#</c> で分ける。2 つ目以降の <c>#</c> はフラグメントの一部になる
    /// （RFC 3986 でもフラグメントは <c>#</c> を含みうる）。
    /// </remarks>
    [Pure]
    private static (string Reference, string? Fragment) SplitFragment(
        string url)
    {
        var index = url.IndexOf('#', StringComparison.Ordinal);

        return index < 0
            ? (url, null)
            : (url[..index], url[(index + 1)..]);
    }
}
