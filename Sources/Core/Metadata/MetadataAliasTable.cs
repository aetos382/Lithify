using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using JetBrains.Annotations;

using Lithify.Abstractions;

namespace Lithify.Core.Metadata;

/// <summary>
/// well-known キーごとに、その値を供給できる別名を優先順に並べた表。
/// </summary>
/// <remarks>
/// <para>
/// <strong>写し先ごとに候補を並べる形にしている。</strong> 逆向き（別名 → 写し先の対応）でも
/// 同じ情報を表せるが、それだと「<see cref="WellKnownMetadata.Description"/> は何から来るのか」を
/// 知るために表全体を走査することになる。利用者が設定を書くときも、診断の文面を組み立てるときも、
/// 問いは常に写し先の側から立つ。
/// </para>
/// <para>
/// <strong>候補の並び順が優先順である。</strong> 先に並んでいる候補が文書に書かれていれば、
/// それが採用される。フロント マターに書かれた順で決める選択肢もあるが、それだと
/// キーを並べ替えるだけで効く値が変わることになり、フロント マターがキーの順序に
/// 意味を持たないことと矛盾する。
/// </para>
/// <para>
/// <strong>更新は写し先ごとの<em>置き換え</em>だけである。</strong>「既存の候補の前に足す」
/// 「特定の候補だけ取り除く」といった差分の操作は持たない。差分は適用先の並びを暗黙の基準にするので、
/// 基準が変われば結果も変わる。Lithify が既定の候補を 1 つ増やしただけで
/// 利用者の設定の意味が変わるような API にしてはならない。
/// 既定を基準にしたい場合は、利用者が <see cref="MetadataAliasCandidate.Defaults"/> を
/// <em>明示的に書く</em>（<see cref="MetadataAliasOptions"/>）。
/// </para>
/// <para>
/// この型は<em>値</em>であり、パーサーの登録とは別の寿命を持つ（<see cref="Content.ContentFormatMap"/>
/// と同じ理由）。組み立てと合成の規則はパーサーを 1 つも用意せずに検証できる。
/// </para>
/// <para>
/// <strong>既定の候補はこの型に持たせない。</strong> ネイティブな名前の語彙は形式ごとに違い
/// （Markdown の <c>lastmod</c> は Hugo の綴りであって Markdown 仕様の語彙ではなく、
/// AsciiDoc には <c>revdate</c> がある）、それを知っているのは各パーサーである。
/// <see cref="Empty"/> から組み立てた表を各パーサーが持ち、
/// 利用者の <see cref="MetadataAliasOptions"/> をそれに重ねる。
/// </para>
/// </remarks>
public sealed class MetadataAliasTable
{
    private readonly ImmutableArray<Entry> _entries;

    private MetadataAliasTable(
        ImmutableArray<Entry> entries)
    {
        this._entries = entries;
    }

    /// <summary>
    /// 写し先を 1 つも持たない表を取得する。
    /// </summary>
    public static MetadataAliasTable Empty { get; } = new([]);

    /// <summary>
    /// 写し先と、その候補の対を列挙する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 列挙順は写し先が表に現れた順である。写し先ごとの写しは互いに独立なので、
    /// 順序は結果を変えない。それでも順序を定めているのは、同じ設定から常に同じ順で
    /// 診断が出るようにするためである（順序が揺れると診断の比較ができない）。
    /// </para>
    /// <para>
    /// 候補が空の写し先も列挙される。写しは起きないが、
    /// 「候補を空にした」ことと「言及されていない」ことは区別して保持される。
    /// </para>
    /// </remarks>
    public IEnumerable<KeyValuePair<MetadataKey, ImmutableArray<MetadataKey>>> Targets
    {
        get
        {
            foreach (var entry in this._entries)
            {
                yield return new(entry.Target, entry.Aliases);
            }
        }
    }

    /// <summary>
    /// 指定した写し先の候補を優先順に取得する。
    /// </summary>
    /// <param name="target">写し先の well-known キー。</param>
    /// <returns>候補。写し先が表に無い場合と候補が空の場合はどちらも空。</returns>
    [Pure]
    public ImmutableArray<MetadataKey> GetAliases(
        MetadataKey target)
    {
        var index = this.IndexOf(target);

        return index < 0 ? [] : this._entries[index].Aliases;
    }

    /// <summary>
    /// 指定した写し先の候補を、渡された別名だけに置き換えた表を返す。
    /// </summary>
    /// <param name="target">写し先の well-known キー。</param>
    /// <param name="aliases">
    /// 別名。渡した順が優先順になる。1 つも渡さなかった場合、その写し先への写しは行われなくなる。
    /// </param>
    /// <returns>新しい表。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="aliases"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="target"/> が <see langword="default"/> である、
    /// または <paramref name="aliases"/> に空または空白のみの要素が含まれる。
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>元の候補は参照しない。</strong> 既にあった候補は渡された別名に置き換わる。
    /// 「既定に足す」形にしない理由は型全体の <c>remarks</c> に記す。
    /// </para>
    /// <para>
    /// 同じ別名を 2 度渡した場合は先のものだけが残る。後のものは決して選ばれない死んだ候補になる。
    /// 写し先と同じ綴りの別名も落とす。写し先が書かれていれば写しは起きないので、
    /// これも決して選ばれない。
    /// </para>
    /// </remarks>
    [Pure]
    public MetadataAliasTable Set(
        MetadataKey target,
        params string[] aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        var keys = ImmutableArray.CreateBuilder<MetadataKey>(aliases.Length);

        foreach (var alias in aliases)
        {
            keys.Add(MetadataKey.Create(alias));
        }

        return this.Set(target, keys.DrainToImmutable());
    }

    /// <summary>
    /// 指定した写し先の候補を、渡されたキーだけに置き換えた表を返す。
    /// </summary>
    /// <param name="target">写し先の well-known キー。</param>
    /// <param name="aliases">
    /// 別名。渡した順が優先順になる。空の場合、その写し先への写しは行われなくなる。
    /// </param>
    /// <returns>新しい表。</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="target"/> が <see langword="default"/> である、
    /// または <paramref name="aliases"/> に <see langword="default"/> の要素が含まれる。
    /// </exception>
    [Pure]
    public MetadataAliasTable Set(
        MetadataKey target,
        ImmutableArray<MetadataKey> aliases)
    {
        if (target.IsEmpty)
        {
            throw new ArgumentException(Messages.MetadataAliasTargetMustNotBeEmpty, nameof(target));
        }

        var keys = ImmutableArray.CreateBuilder<MetadataKey>(aliases.IsDefault ? 0 : aliases.Length);

        if (!aliases.IsDefault)
        {
            foreach (var alias in aliases)
            {
                if (alias.IsEmpty)
                {
                    throw new ArgumentException(
                        Messages.MetadataAliasCandidateMustNotBeEmpty, nameof(aliases));
                }

                // 写し先と同じ綴りの候補と、2 度目以降の重複は落とす。どちらも決して選ばれない。
                if (alias != target && !keys.Contains(alias))
                {
                    keys.Add(alias);
                }
            }
        }

        var entry = new Entry(target, keys.DrainToImmutable());
        var index = this.IndexOf(target);

        // 候補が空になっても項目自体は残す。取り除くと、候補を空にした写し先と
        // 一度も言及されていない写し先が区別できなくなる。
        return new MetadataAliasTable(
            index < 0
                ? this._entries.Add(entry)
                : this._entries.SetItem(index, entry));
    }

    /// <summary>
    /// 指定した写し先についての項目自体を取り除いた表を返す。
    /// </summary>
    /// <param name="target">写し先の well-known キー。</param>
    /// <returns>新しい表。項目が無かった場合はこのインスタンス。</returns>
    /// <remarks>
    /// 写しを行う側から見た挙動は候補を空にするのと同じで、
    /// 違いは <see cref="Targets"/> にその写し先が現れるかどうかだけである。
    /// </remarks>
    [Pure]
    public MetadataAliasTable Remove(
        MetadataKey target)
    {
        var index = this.IndexOf(target);

        return index < 0 ? this : new MetadataAliasTable(this._entries.RemoveAt(index));
    }

    /// <summary>
    /// 指定した写し先の項目の位置を返す。
    /// </summary>
    /// <param name="target">写し先。</param>
    /// <returns>位置。無い場合は -1。</returns>
    private int IndexOf(
        MetadataKey target)
    {
        for (var i = 0; i < this._entries.Length; ++i)
        {
            if (this._entries[i].Target == target)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 写し先と、その候補の対。
    /// </summary>
    /// <param name="Target">写し先の well-known キー。</param>
    /// <param name="Aliases">候補。優先順に並ぶ。</param>
    private readonly record struct Entry(
        MetadataKey Target,
        ImmutableArray<MetadataKey> Aliases);
}
