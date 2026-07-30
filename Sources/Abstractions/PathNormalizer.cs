using System;
using System.Collections.Generic;
using System.Text;

using JetBrains.Annotations;

namespace Lithify.Abstractions;

/// <summary>
/// 正規化されるパスの種類。エラー メッセージの選択に用いる。
/// </summary>
internal enum PathKind
{
    /// <summary>
    /// サイト ルートからの相対パス（<see cref="ContentPath"/>）。
    /// </summary>
    Content,

    /// <summary>
    /// 出力ルートからの相対パス（<see cref="OutputPath"/>）。
    /// </summary>
    Output,
}

/// <summary>
/// <see cref="ContentPath"/> と <see cref="OutputPath"/> が共有するパス正規化ロジック。
/// </summary>
/// <remarks>
/// <para>
/// 正規化の内容:
/// </para>
/// <list type="bullet">
///   <item><description><c>\</c> を <c>/</c> に変換する。</description></item>
///   <item><description>連続する区切りを 1 つに畳む。</description></item>
///   <item><description>先頭と末尾の区切りを取り除く。</description></item>
///   <item><description><c>.</c> セグメントを取り除く。</description></item>
///   <item><description><c>..</c> セグメントを解決する。ルートより上に遡る場合は例外にする。</description></item>
/// </list>
/// <para>
/// すべて純粋関数なので、ファイル システムを触らずに検証できる。
/// </para>
/// </remarks>
internal static class PathNormalizer
{
    /// <summary>
    /// パスの区切り文字。
    /// </summary>
    internal const char Separator = '/';

    private const char AlternateSeparator = '\\';

    private const string CurrentDirectorySegment = ".";

    private const string ParentDirectorySegment = "..";

    /// <summary>
    /// パスを正規化する。
    /// </summary>
    /// <param name="value">正規化するパス。</param>
    /// <param name="kind">パスの種類。エラー メッセージの選択に用いる。</param>
    /// <param name="parameterName">例外に添える引数名。</param>
    /// <returns>正規化されたパス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> が空である、絶対パスである、またはルートより上に遡っている。
    /// </exception>
    [Pure]
    internal static string Normalize(
        string value,
        PathKind kind,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value.Length == 0)
        {
            throw new ArgumentException(EmptyMessage(kind), parameterName);
        }

        if (IsRooted(value))
        {
            throw new ArgumentException(RootedMessage(kind, value), parameterName);
        }

        var segments = new List<Range>();

        foreach (var segment in EnumerateSegments(value))
        {
            var text = value.AsSpan()[segment];

            if (text.SequenceEqual(CurrentDirectorySegment))
            {
                continue;
            }

            if (text.SequenceEqual(ParentDirectorySegment))
            {
                if (segments.Count == 0)
                {
                    throw new ArgumentException(EscapedMessage(kind, value), parameterName);
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        if (segments.Count == 0)
        {
            throw new ArgumentException(EmptyMessage(kind), parameterName);
        }

        return Join(value, segments);
    }

    private static string EmptyMessage(
        PathKind kind)
    {
        return kind == PathKind.Content
            ? Messages.ContentPathMustNotBeEmpty
            : Messages.OutputPathMustNotBeEmpty;
    }

    private static string RootedMessage(
        PathKind kind,
        string value)
    {
        return kind == PathKind.Content
            ? Messages.FormatContentPathMustBeRelative(value)
            : Messages.FormatOutputPathMustBeRelative(value);
    }

    private static string EscapedMessage(
        PathKind kind,
        string value)
    {
        return kind == PathKind.Content
            ? Messages.FormatContentPathMustNotEscapeRoot(value)
            : Messages.FormatOutputPathMustNotEscapeRoot(value);
    }

    /// <summary>
    /// パスが絶対パスかどうかを判定する。
    /// </summary>
    /// <param name="value">判定するパス。空でないこと。</param>
    /// <returns>絶対パスの場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// プラットフォームに依存しない判定を行う。Windows のドライブ指定と UNC パスは、
    /// Linux 上で評価されたときも絶対パスとして拒否する。そうしないと、
    /// Linux でビルドが通ったサイトが Windows でパスの意味を変えてしまう。
    /// </remarks>
    [Pure]
    private static bool IsRooted(
        string value)
    {
        var span = value.AsSpan();

        if (span[0] is Separator or AlternateSeparator)
        {
            return true;
        }

        // ドライブ指定 (C: / C:\ / C:/)
        return span.Length >= 2 && span[1] == ':' && char.IsAsciiLetter(span[0]);
    }

    /// <summary>
    /// パスを区切り文字で分割し、空でないセグメントの範囲を列挙する。
    /// </summary>
    /// <param name="value">分割するパス。</param>
    /// <returns>セグメントの範囲。</returns>
    [Pure]
    private static IEnumerable<Range> EnumerateSegments(
        string value)
    {
        var start = 0;

        for (var i = 0; i <= value.Length; ++i)
        {
            if (i < value.Length && value[i] is not (Separator or AlternateSeparator))
            {
                continue;
            }

            if (i > start)
            {
                yield return new Range(start, i);
            }

            start = i + 1;
        }
    }

    /// <summary>
    /// セグメントを区切り文字で連結する。
    /// </summary>
    /// <param name="value">元のパス。</param>
    /// <param name="segments">連結するセグメントの範囲。1 つ以上あること。</param>
    /// <returns>連結された文字列。元の文字列がすでに正規形の場合は元の文字列自身。</returns>
    [Pure]
    private static string Join(
        string value,
        List<Range> segments)
    {
        if (IsCanonical(value, segments))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);

        foreach (var segment in segments)
        {
            if (builder.Length > 0)
            {
                builder.Append(Separator);
            }

            builder.Append(value.AsSpan()[segment]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 元の文字列がすでに正規形かどうかを判定する。
    /// </summary>
    /// <param name="value">元のパス。</param>
    /// <param name="segments">抽出されたセグメントの範囲。1 つ以上あること。</param>
    /// <returns>正規形の場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// 大半のパスは最初から正規形なので、その場合に部分文字列の生成を省くための判定。
    /// セグメントが元の文字列を隙間なく（区切り 1 文字ずつを挟んで）覆っており、
    /// かつ代替区切り文字を含まなければ、連結結果は元の文字列と一致する。
    /// </remarks>
    [Pure]
    private static bool IsCanonical(
        string value,
        List<Range> segments)
    {
        if (segments[0].Start.Value != 0 || segments[^1].End.Value != value.Length)
        {
            return false;
        }

        var expected = 0;

        foreach (var segment in segments)
        {
            if (segment.Start.Value != expected)
            {
                return false;
            }

            expected = segment.End.Value + 1;
        }

        return !value.Contains(AlternateSeparator, StringComparison.Ordinal);
    }

    /// <summary>
    /// 正規化済みパスからファイル名を取り出す。
    /// </summary>
    /// <param name="value">正規化済みのパス。</param>
    /// <returns>最後の区切り以降の部分。</returns>
    [Pure]
    internal static ReadOnlySpan<char> GetFileName(
        string value)
    {
        var span = value.AsSpan();
        var separator = span.LastIndexOf(Separator);

        return separator < 0 ? span : span[(separator + 1)..];
    }

    /// <summary>
    /// 正規化済みパスから拡張子を取り出す。
    /// </summary>
    /// <param name="value">正規化済みのパス。</param>
    /// <returns>先頭の <c>.</c> を含む拡張子。拡張子がない場合は空。</returns>
    /// <remarks>
    /// ファイル名の先頭のドットは拡張子ではなく隠しファイルの印とみなす（<c>.gitignore</c>）。
    /// </remarks>
    [Pure]
    internal static ReadOnlySpan<char> GetExtension(
        string value)
    {
        var fileName = GetFileName(value);
        var dot = fileName.LastIndexOf('.');

        return dot <= 0 ? [] : fileName[dot..];
    }

    /// <summary>
    /// 正規化済みパスから拡張子を除いたファイル名を取り出す。
    /// </summary>
    /// <param name="value">正規化済みのパス。</param>
    /// <returns>拡張子を除いたファイル名。</returns>
    [Pure]
    internal static ReadOnlySpan<char> GetFileNameWithoutExtension(
        string value)
    {
        var fileName = GetFileName(value);
        var dot = fileName.LastIndexOf('.');

        return dot <= 0 ? fileName : fileName[..dot];
    }

    /// <summary>
    /// 正規化済みパスから親ディレクトリを取り出す。
    /// </summary>
    /// <param name="value">正規化済みのパス。</param>
    /// <returns>親ディレクトリのパス。親がない場合は <see langword="null"/>。</returns>
    [Pure]
    internal static string? GetDirectory(
        string value)
    {
        var separator = value.LastIndexOf(Separator);

        return separator < 0 ? null : value[..separator];
    }

    /// <summary>
    /// 正規化済みパスの拡張子を差し替える。
    /// </summary>
    /// <param name="value">正規化済みのパス。</param>
    /// <param name="extension">新しい拡張子。先頭の <c>.</c> は省略できる。空文字列なら拡張子を取り除く。</param>
    /// <returns>拡張子が差し替えられたパス。</returns>
    [Pure]
    internal static string ReplaceExtension(
        string value,
        string extension)
    {
        var current = GetExtension(value);
        var stem = value.AsSpan()[..^current.Length];

        if (extension.Length == 0)
        {
            return stem.ToString();
        }

        return extension[0] == '.'
            ? string.Concat(stem, extension)
            : string.Concat(stem, ".", extension);
    }
}
