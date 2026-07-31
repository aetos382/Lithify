using System;

using JetBrains.Annotations;

using Lithify.Markdown.Abstractions;

using Markdig;
using Markdig.Extensions.EmphasisExtras;

namespace Lithify.Parsers.Markdig;

/// <summary>
/// <see cref="MarkdownOptions"/> から Markdig のパイプラインを組み立てる。
/// </summary>
/// <remarks>
/// <para>
/// 写像を <see cref="MarkdigContentParser"/> の中に置かず独立させているのは、
/// これが<em>純粋関数</em>であり、パーサーを動かさずに検証できるためである。
/// 「<c>Footnotes = true</c> にしたとき脚注の拡張が入るか」は
/// パイプラインを組み立てるだけで確かめられる。
/// </para>
/// <para>
/// <see cref="MarkdownFlavor"/> と個別のフラグの関係は<strong>方言が下敷き、フラグが上書き</strong>である。
/// 方言で個別フラグの<em>既定</em>を決めてしまうと
/// 「<c>CommonMark</c> にしたら明示的に立てた <c>Tables</c> が消える」ことになり、
/// <see cref="MarkdownOptions"/> の各プロパティが持つ既定値と矛盾する。
/// 逆に方言を無視すると <see cref="MarkdownFlavor.CommonMark"/> の意味が無くなるので、
/// 方言は<em>フラグで表現されない差</em>だけを担う。
/// </para>
/// </remarks>
internal static class MarkdownPipelineFactory
{
    /// <summary>
    /// パイプラインを組み立てる。
    /// </summary>
    /// <param name="options">形式の設定。</param>
    /// <param name="engineOptions">エンジン固有の設定。</param>
    /// <returns>組み立てられたパイプライン。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> または <paramref name="engineOptions"/> が <see langword="null"/> である。
    /// </exception>
    [Pure]
    public static MarkdownPipeline Create(
        MarkdownOptions options,
        MarkdigOptions engineOptions)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(engineOptions);

        var builder = new MarkdownPipelineBuilder();

        // フロント マターは常に有効にする。無効にすると Markdig が先頭の --- を
        // ThematicBreak と解釈し、メタデータが本文の水平線と見出しに化ける。
        // 「フロント マターを使わない」ことは書かなければ済むので、設定にする必要はない。
        builder.UseYamlFrontMatter();

        if (engineOptions.AllowFrontMatterInMiddleOfDocument)
        {
            ConfigureFrontMatterInMiddleOfDocument(builder);
        }

        // 位置情報は常に取る。診断とメタデータの出所（MetadataProvenance.Declared）が
        // 位置を持てるかどうかがこれで決まり、後から足せる類のものではない。
        builder.UsePreciseSourceLocation();

        if (options.Tables)
        {
            builder.UsePipeTables();
        }

        if (options.TaskLists)
        {
            builder.UseTaskLists();
        }

        if (options.Autolinks)
        {
            builder.UseAutoLinks();
        }

        if (options.Footnotes)
        {
            builder.UseFootnotes();
        }

        if (options.HardLineBreaks)
        {
            builder.UseSoftlineBreakAsHardlineBreak();
        }

        if (options.Strikethrough)
        {
            // 取り消し線だけを入れる。UseEmphasisExtras() を引数なしで呼ぶと
            // 下付き・上付き・挿入・強調表示まで一括で有効になり、
            // MarkdownOptions が個別に持たない記法が黙って通る。
            builder.UseEmphasisExtras(EmphasisExtraOptions.Strikethrough);
        }

        if (options.Attributes)
        {
            // これが無いと `## 見出し {#anchor}` の `{#anchor}` は
            // 見出しの文字列の一部として読まれ、明示的なアンカーを取り出せない。
            builder.UseGenericAttributes();
        }

        if (engineOptions.AutoIdentifiers)
        {
            builder.UseAutoIdentifiers();
        }

        if (engineOptions.MaximumNestingDepth is { } depth)
        {
            builder.MaximumNestingDepth = depth;
        }

        // MarkdownFlavor で分岐しないのは、CommonMark と GFM の差が
        // 上のフラグ（表・取り消し線・タスク リスト・自動リンク）で尽きているためである。
        // 方言はそれらの既定値を決める役であり、既定値は MarkdownOptions 側が持っている。
        // ここで方言を見て再度上書きすると、明示的に立てたフラグが方言に負ける。
        _ = options.Flavor;

        return builder.Build();
    }

    /// <summary>
    /// フロント マターを文書の途中でも認識するように設定する。
    /// </summary>
    /// <param name="builder">設定するビルダー。</param>
    /// <remarks>
    /// <c>UseYamlFrontMatter()</c> は拡張を冪等に追加するので、
    /// 追加済みの拡張のプロパティを書き換える形になる。
    /// </remarks>
    private static void ConfigureFrontMatterInMiddleOfDocument(
        MarkdownPipelineBuilder builder)
    {
        foreach (var extension in builder.Extensions)
        {
            if (extension is global::Markdig.Extensions.Yaml.YamlFrontMatterExtension yaml)
            {
                yaml.AllowInMiddleOfDocument = true;
            }
        }
    }
}
