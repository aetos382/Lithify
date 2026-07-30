using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using JetBrains.Annotations;

using Lithify.Abstractions;
using Lithify.Abstractions.Ast;

namespace Lithify.Core.Site;

/// <summary>
/// 内部リンクの逆引き索引。
/// </summary>
/// <remarks>
/// <para>
/// 「どのページからこのページへリンクが張られているか」を引く。これが R7 の題材である
/// 双方向リンクの実体である。新しい記事から古い記事へリンクが張られると、
/// 古い記事のページはバックリンク一覧を含むフラグメントを再描画するが、
/// 本文フラグメントは変わらないため、合成結果が同一なら出力は書き換わらない。
/// </para>
/// <para>
/// 索引は AST の <see cref="LinkNode"/> を走査して構築する。
/// Markdown の <c>[text](path)</c> と AsciiDoc の <c>xref:</c> はどちらも
/// <see cref="LinkTarget"/> に写っているため、この処理は記法に依存しない。
/// </para>
/// </remarks>
public sealed class BacklinkIndex
{
    private readonly ImmutableDictionary<ContentPath, ImmutableArray<ContentPath>> _incoming;

    private BacklinkIndex(
        ImmutableDictionary<ContentPath, ImmutableArray<ContentPath>> incoming)
    {
        this._incoming = incoming;
    }

    /// <summary>
    /// 空の索引。
    /// </summary>
    public static BacklinkIndex Empty { get; } = new([]);

    /// <summary>
    /// 指定したページへリンクしているページを取得する。
    /// </summary>
    /// <param name="target">リンク先のページ。</param>
    /// <returns>リンク元のページ。パスの辞書順に並ぶ。</returns>
    /// <remarks>
    /// 並び順を安定させるのは、バックリンク一覧のフラグメントのフィンガープリントが
    /// 走査順に依存しないようにするためである。順序が揺れると内容が同じでも
    /// 出力が書き換わってしまい R7 が壊れる。
    /// </remarks>
    [Pure]
    public ImmutableArray<ContentPath> GetIncoming(
        ContentPath target)
    {
        return this._incoming.TryGetValue(target, out var sources)
            ? sources
            : [];
    }

    /// <summary>
    /// 逆引き索引を構築する。
    /// </summary>
    /// <param name="outgoing">各ページから出ているリンク先。</param>
    /// <returns>構築された索引。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="outgoing"/> が <see langword="null"/> である。</exception>
    [Pure]
    public static BacklinkIndex Build(
        IEnumerable<KeyValuePair<ContentPath, ImmutableArray<ContentPath>>> outgoing)
    {
        ArgumentNullException.ThrowIfNull(outgoing);

        var builder = new Dictionary<ContentPath, SortedSet<ContentPath>>();

        foreach (var (source, targets) in outgoing)
        {
            foreach (var target in targets)
            {
                if (!builder.TryGetValue(target, out var sources))
                {
                    sources = [];
                    builder.Add(target, sources);
                }

                sources.Add(source);
            }
        }

        var incoming = ImmutableDictionary.CreateBuilder<ContentPath, ImmutableArray<ContentPath>>();

        foreach (var (target, sources) in builder)
        {
            incoming.Add(target, [.. sources]);
        }

        return new BacklinkIndex(incoming.ToImmutable());
    }
}
