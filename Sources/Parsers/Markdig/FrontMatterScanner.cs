using System;

using JetBrains.Annotations;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// 文書先頭の YAML フロント マターの範囲を切り出す。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Lithify.Abstractions.IContentParser.ParseMetadataAsync"/> のための軽量パスである。
/// 本文を Markdig に通さずに済ませるために独立して存在する。ここを完全パースに委ねると
/// 「1 ページ表示するために全記事を完全パースする」ことになり、オンデマンド ビルドの利点が消える。
/// </para>
/// <para>
/// <strong>Markdig の <c>YamlFrontMatterParser</c> と同じ境界を切らなければならない。</strong>
/// 食い違うと軽量パスと完全パースでメタデータが一致せず、契約テストが落ちる。
/// 実測して合わせた規則は次のとおり。
/// </para>
/// <list type="bullet">
///   <item>開始行は行頭から <c>---</c> の 3 文字。字下げは許されず、4 文字以上の <c>-</c> も開始行にならない</item>
///   <item>開始行の <c>---</c> の後に空白（タブを含む）が続いてもよいが、空白以外の文字が続くと開始行にならない</item>
///   <item>終了行は <c>---</c> または <c>...</c> の 3 文字で、開始行と同じ規則（字下げ不可、4 文字以上は不可、後続は空白のみ）</item>
///   <item>終了行が現れないまま入力が尽きた場合、フロント マターは<em>成立しない</em></item>
///   <item>開始行の直後が終了行でも成立し、その場合の YAML 本体は空になる</item>
///   <item>BOM が先頭にあると開始行と見なされない（Markdig が BOM を行頭の文字として扱うため）</item>
/// </list>
/// <para>
/// <strong>境界が Markdig と食い違うのは、YAML 本体が空になる場合だけである。</strong>
/// 開始行の直後に終了行が来る <c>---\n---\n</c> を Markdig は 2 つの
/// <c>ThematicBreak</c> にするが、このスキャナは空のフロント マターとして受ける。
/// 開始行・本体・終了行・改行・後続の組み合わせ 2352 通りで照合し、
/// <em>YAML 本体が空でない場合の不一致は 0 件</em>であることを確認している。
/// 残る差は 9 件あるが、いずれも本体が空か空白・コメントのみで、
/// YAML として読むと文書が 0 個になるため <see cref="Lithify.Abstractions.DocumentMetadata"/> は
/// どちらの経路でも空になる。したがって
/// <see cref="Lithify.Abstractions.IContentParser.ParseMetadataAsync"/> と
/// <c>ParseAsync</c> の一致という契約は破れない。
/// </para>
/// <para>
/// この差を消しに行かないのは、消すには「終了行の直後が本文として何になるか」という
/// Markdig の内部規則まで写す必要があり、写した規則が Markdig の更新で
/// 静かにずれるほうが危ういからである。観測できない差は残す。
/// </para>
/// </remarks>
internal static class FrontMatterScanner
{
    /// <summary>
    /// フロント マターの境界。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="FrontMatter.FirstYamlLine"/> は
    /// <see cref="Lithify.Abstractions.MetadataProvenance"/> に入れる位置を
    /// 文書全体の行番号に直すために要る。YAML パーサーが返す行番号は
    /// YAML 本体の中での相対値なので、これを足して文書中の位置にする。
    /// 開始行が 1 行目なら YAML 本体は 2 行目から始まるので、値は常に 2 以上になる。
    /// </para>
    /// <para>
    /// <see cref="Yaml"/> を <see cref="string"/> にせず <see cref="ReadOnlySpan{T}"/> のままにするため
    /// <see langword="ref struct"/> にしている。切り出した時点で文字列を作ると、
    /// メタデータだけを読む軽量パスが必ず 1 回の割り当てを払うことになる。
    /// 呼び出し側は <see langword="out"/> で受けてその場で使うだけなので制約にならない。
    /// </para>
    /// </remarks>
    public readonly ref struct FrontMatter
    {
        /// <summary>
        /// <see cref="FrontMatter"/> を生成する。
        /// </summary>
        /// <param name="yaml">YAML 本体。</param>
        /// <param name="firstYamlLine">YAML 本体の最初の行の 1 起算の行番号。</param>
        public FrontMatter(
            ReadOnlySpan<char> yaml,
            int firstYamlLine)
        {
            this.Yaml = yaml;
            this.FirstYamlLine = firstYamlLine;
        }

        /// <summary>
        /// YAML 本体を取得する。開始行と終了行は含まない。
        /// </summary>
        public ReadOnlySpan<char> Yaml { get; }

        /// <summary>
        /// YAML 本体の最初の行の 1 起算の行番号を取得する。
        /// </summary>
        public int FirstYamlLine { get; }
    }

    /// <summary>
    /// 文書先頭のフロント マターを切り出す。
    /// </summary>
    /// <param name="text">文書全体。</param>
    /// <param name="frontMatter">切り出されたフロント マター。</param>
    /// <returns>フロント マターが成立した場合は <see langword="true"/>。</returns>
    [Pure]
    public static bool TryScan(
        ReadOnlySpan<char> text,
        out FrontMatter frontMatter)
    {
        frontMatter = default;

        if (!IsFence(GetLine(text, 0, out var afterOpening), '-'))
        {
            return false;
        }

        var yamlStart = afterOpening;
        var line = 2;

        while (afterOpening < text.Length)
        {
            var current = GetLine(text, afterOpening, out var next);

            if (IsFence(current, '-') || IsFence(current, '.'))
            {
                // 終了行の直前までが YAML 本体。開始行の直後が終了行なら空になる。
                var yaml = text[yamlStart..GetLineContentEnd(text, yamlStart, afterOpening)];

                frontMatter = new FrontMatter(yaml, line);

                return true;
            }

            afterOpening = next;
            ++line;
        }

        // 終了行が無いまま尽きた。Markdig もこの場合はフロント マターにしない。
        return false;
    }

    /// <summary>
    /// 指定した位置から 1 行を取り出す。
    /// </summary>
    /// <param name="text">対象の文字列。</param>
    /// <param name="start">行の開始位置。</param>
    /// <param name="next">次の行の開始位置。</param>
    /// <returns>改行を含まない行の内容。</returns>
    [Pure]
    private static ReadOnlySpan<char> GetLine(
        ReadOnlySpan<char> text,
        int start,
        out int next)
    {
        if (start >= text.Length)
        {
            next = text.Length;

            return default;
        }

        var rest = text[start..];
        var breakIndex = rest.IndexOfAny('\r', '\n');

        if (breakIndex < 0)
        {
            next = text.Length;

            return rest;
        }

        var lineBreakLength =
            rest[breakIndex] == '\r' &&
            breakIndex + 1 < rest.Length &&
            rest[breakIndex + 1] == '\n'
                ? 2
                : 1;

        next = start + breakIndex + lineBreakLength;

        return rest[..breakIndex];
    }

    /// <summary>
    /// YAML 本体の末尾（終了行の直前の改行を除いた位置）を求める。
    /// </summary>
    /// <param name="text">対象の文字列。</param>
    /// <param name="yamlStart">YAML 本体の開始位置。</param>
    /// <param name="closingStart">終了行の開始位置。</param>
    /// <returns>YAML 本体の終端位置。</returns>
    /// <remarks>
    /// 終了行の直前の改行を残すと YAML の末尾に空行が付く。
    /// 意味は変わらないが、行番号の対応がずれないよう落としておく。
    /// </remarks>
    [Pure]
    private static int GetLineContentEnd(
        ReadOnlySpan<char> text,
        int yamlStart,
        int closingStart)
    {
        var end = closingStart;

        if (end > yamlStart && text[end - 1] == '\n')
        {
            --end;
        }

        if (end > yamlStart && text[end - 1] == '\r')
        {
            --end;
        }

        return end;
    }

    /// <summary>
    /// 行が区切り（<c>---</c> / <c>...</c>）かどうかを判定する。
    /// </summary>
    /// <param name="line">判定する行。</param>
    /// <param name="marker">区切りを構成する文字。</param>
    /// <returns>区切りである場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// ちょうど 3 文字であることを要求する。<c>----</c> は Markdig では
    /// フロント マターの区切りにならず <c>ThematicBreak</c> になる。
    /// </remarks>
    [Pure]
    private static bool IsFence(
        ReadOnlySpan<char> line,
        char marker)
    {
        if (line.Length < 3)
        {
            return false;
        }

        if (line[0] != marker || line[1] != marker || line[2] != marker)
        {
            return false;
        }

        var rest = line[3..];

        foreach (var c in rest)
        {
            if (c is not (' ' or '\t'))
            {
                return false;
            }
        }

        return true;
    }
}
