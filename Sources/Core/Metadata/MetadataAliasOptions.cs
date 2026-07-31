using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using Lithify.Abstractions;

namespace Lithify.Core.Metadata;

/// <summary>
/// well-known キーに写す別名についての、利用者による設定。
/// </summary>
/// <remarks>
/// <para>
/// <strong>写し先ごとの<em>置き換え</em>だけを受け付ける。</strong> 既定の候補に足したり
/// 特定の候補だけ取り除いたりする操作は無い。差分の指定は既定の並びを暗黙の基準にするので、
/// Lithify が既定の候補を増やしたり綴りを変えたりしただけで、利用者が何も変えていないのに
/// 挙動が変わる。置き換えなら、書いた候補列がそのまま結果である。
/// </para>
/// <para>
/// 既定を基準にしたい場合は <see cref="MetadataAliasCandidate.Defaults"/> を候補列の中に
/// <em>明示的に書く</em>。書いた位置に既定の候補列が展開されるので、末尾に置けば
/// 「まず自分の語彙、無ければ Lithify が知っている綴り」になる。暗黙の差分と違い、
/// 既定に依存していることが設定を読めば分かる。
/// </para>
/// <para>
/// <strong>言及しなかった写し先は各パーサーの既定のままになる。</strong> 既定は形式ごとに違う
/// （Markdown の <c>lastmod</c>、AsciiDoc の <c>revdate</c>）が、突き合わせは各パーサーが
/// <see cref="Apply"/> で行うので、利用者が「どの形式に対する設定か」を指定する必要はない。
/// </para>
/// <para>
/// <strong>設定は形式に依らない。</strong> 全パーサーが同じ上書きを見る。
/// 「同じ綴りが形式によって別の意味を持つ」ことが避けられないなら形式ごとの設定が要るが、
/// 実際にはほぼ起こらない。AsciiDoc の形式固有の属性（<c>:toc:</c> / <c>:icons:</c> /
/// <c>:imagesdir:</c> / <c>:stylesheet:</c>）は<em>描画指令</em>であって、
/// <see cref="WellKnownMetadata"/> に対応する写し先を持たない。
/// 一方でメタデータらしい属性（<c>description</c> / <c>keywords</c> / <c>author</c>）は
/// 既存ジェネレーターの語彙と意味が一致する。衝突するのは語彙の層が違うもの同士ではなく、
/// 同じ層の同じ概念に別の綴りが当たっている場合であり、それは並びの順で決まる。
/// </para>
/// <para>
/// 例外的に形式ごとの判断が要る場合（AsciiDoc の <c>:title:</c> は doctitle とは別物である）は、
/// <em>パーサーが</em>決めることであって利用者の設定で直すことではない。
/// Lithify が形式の語彙を取り違えているなら、利用者に形式ごとの設定を書かせて
/// 回避させるのではなく、パーサーの既定を直すべきである。
/// </para>
/// <para>
/// それでも形式ごとの段が必要になった場合は、形式を受ける入れ子の設定を足せばよい。
/// 上書きが写し先ごとの置き換えなので、形式ごとの層は共通の層の後に同じ演算で重なるだけであり、
/// 既存の設定の意味は変わらない。
/// </para>
/// <example>
/// <code>
/// builder.ConfigureMetadataAliases(a =>
/// {
///     // abstract があればそれ、無ければ summary、どちらも無ければ写さない。
///     a.Description = ["abstract", "summary"];
///
///     // 自分の語彙を優先し、無ければ Lithify の既定に任せる。
///     a.LastModified = ["modified-on", MetadataAliasCandidate.Defaults];
///
///     // tags への写しを止める。文書に tags を直接書いた場合のみ値を持つ。
///     a.Tags = [];
///
///     // well-known 以外のキーも写し先にできる。
///     a[MetadataKey.Create("series")] = ["book"];
/// });
/// </code>
/// </example>
/// </remarks>
public sealed class MetadataAliasOptions
{
    // 写し先ごとに 1 つの候補列を持つ。順序を保つのは、同じ設定から常に同じ順で
    // 診断が出るようにするためである（Dictionary の列挙順に依存させない）。
    private readonly List<MetadataKey> _order = [];

    private readonly Dictionary<MetadataKey, ImmutableArray<MetadataAliasCandidate>> _overrides = [];

    /// <summary>
    /// 指定した写し先の候補を取得または設定する。
    /// </summary>
    /// <param name="target">写し先のキー。</param>
    /// <value>
    /// <para>
    /// 候補。渡した順が優先順になる。空の並びを設定すると、その写し先への写しを行わなくなる。
    /// <see langword="default"/>（<see cref="ImmutableArray{T}.IsDefault"/>）を設定すると
    /// 設定そのものを取り消し、パーサーの既定に戻る。
    /// </para>
    /// <para>
    /// 取得した値が <see langword="default"/> の場合、この写し先について何も設定されていない。
    /// 既定の候補は<em>返らない</em>。既定は形式ごとに違うので、この型からは見えない。
    /// </para>
    /// </value>
    /// <exception cref="ArgumentException"><paramref name="target"/> が <see langword="default"/> である。</exception>
    // インデクサーの引数を string にはしない（CA1043）。写し先はキーであり、
    // キーは正規化を経て初めてキーになる（MetadataKey.Create）。string を受けると
    // 正規化前の綴りが写し先として通り、"Page_Title" と "page-title" が
    // 別の写し先になりうる。
#pragma warning disable CA1043 // Use integral or string argument for indexers
    public ImmutableArray<MetadataAliasCandidate> this[MetadataKey target]
    {
        get =>
            this._overrides.TryGetValue(target, out var candidates) ? candidates : default;

        set
        {
            if (target.IsEmpty)
            {
                throw new ArgumentException(Messages.MetadataAliasTargetMustNotBeEmpty, nameof(target));
            }

            if (value.IsDefault)
            {
                if (this._overrides.Remove(target))
                {
                    this._order.Remove(target);
                }

                return;
            }

            if (this._overrides.TryAdd(target, value))
            {
                this._order.Add(target);
            }
            else
            {
                this._overrides[target] = value;
            }
        }
    }
#pragma warning restore CA1043

    /// <summary>
    /// <see cref="WellKnownMetadata.Title"/> の候補を取得または設定する。
    /// </summary>
    /// <value>候補。意味は <see cref="this[MetadataKey]"/> と同じ。</value>
    public ImmutableArray<MetadataAliasCandidate> Title
    {
        get => this[WellKnownMetadata.Title];
        set => this[WellKnownMetadata.Title] = value;
    }

    /// <summary>
    /// <see cref="WellKnownMetadata.Date"/> の候補を取得または設定する。
    /// </summary>
    /// <value>候補。意味は <see cref="this[MetadataKey]"/> と同じ。</value>
    public ImmutableArray<MetadataAliasCandidate> Date
    {
        get => this[WellKnownMetadata.Date];
        set => this[WellKnownMetadata.Date] = value;
    }

    /// <summary>
    /// <see cref="WellKnownMetadata.LastModified"/> の候補を取得または設定する。
    /// </summary>
    /// <value>候補。意味は <see cref="this[MetadataKey]"/> と同じ。</value>
    public ImmutableArray<MetadataAliasCandidate> LastModified
    {
        get => this[WellKnownMetadata.LastModified];
        set => this[WellKnownMetadata.LastModified] = value;
    }

    /// <summary>
    /// <see cref="WellKnownMetadata.Author"/> の候補を取得または設定する。
    /// </summary>
    /// <value>候補。意味は <see cref="this[MetadataKey]"/> と同じ。</value>
    public ImmutableArray<MetadataAliasCandidate> Author
    {
        get => this[WellKnownMetadata.Author];
        set => this[WellKnownMetadata.Author] = value;
    }

    /// <summary>
    /// <see cref="WellKnownMetadata.Tags"/> の候補を取得または設定する。
    /// </summary>
    /// <value>候補。意味は <see cref="this[MetadataKey]"/> と同じ。</value>
    public ImmutableArray<MetadataAliasCandidate> Tags
    {
        get => this[WellKnownMetadata.Tags];
        set => this[WellKnownMetadata.Tags] = value;
    }

    /// <summary>
    /// <see cref="WellKnownMetadata.Draft"/> の候補を取得または設定する。
    /// </summary>
    /// <value>候補。意味は <see cref="this[MetadataKey]"/> と同じ。</value>
    public ImmutableArray<MetadataAliasCandidate> Draft
    {
        get => this[WellKnownMetadata.Draft];
        set => this[WellKnownMetadata.Draft] = value;
    }

    /// <summary>
    /// <see cref="WellKnownMetadata.Slug"/> の候補を取得または設定する。
    /// </summary>
    /// <value>候補。意味は <see cref="this[MetadataKey]"/> と同じ。</value>
    public ImmutableArray<MetadataAliasCandidate> Slug
    {
        get => this[WellKnownMetadata.Slug];
        set => this[WellKnownMetadata.Slug] = value;
    }

    /// <summary>
    /// <see cref="WellKnownMetadata.Layout"/> の候補を取得または設定する。
    /// </summary>
    /// <value>候補。意味は <see cref="this[MetadataKey]"/> と同じ。</value>
    public ImmutableArray<MetadataAliasCandidate> Layout
    {
        get => this[WellKnownMetadata.Layout];
        set => this[WellKnownMetadata.Layout] = value;
    }

    /// <summary>
    /// <see cref="WellKnownMetadata.Description"/> の候補を取得または設定する。
    /// </summary>
    /// <value>候補。意味は <see cref="this[MetadataKey]"/> と同じ。</value>
    public ImmutableArray<MetadataAliasCandidate> Description
    {
        get => this[WellKnownMetadata.Description];
        set => this[WellKnownMetadata.Description] = value;
    }

    /// <summary>
    /// <see cref="WellKnownMetadata.Language"/> の候補を取得または設定する。
    /// </summary>
    /// <value>候補。意味は <see cref="this[MetadataKey]"/> と同じ。</value>
    public ImmutableArray<MetadataAliasCandidate> Language
    {
        get => this[WellKnownMetadata.Language];
        set => this[WellKnownMetadata.Language] = value;
    }

    // SourceFormat のプロパティは無い。あのキーはパーサーが判定した形式の記録であり、
    // 別名から写す対象ではない（WellKnownMetadataMapper が表を通さずに設定する）。

    /// <summary>
    /// この設定を既定の表に重ねる。
    /// </summary>
    /// <param name="defaults">パーサーが持つ既定の候補。</param>
    /// <returns>設定が重なった表。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="defaults"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// <para>
    /// 各パーサーが構築時に 1 度だけ呼ぶ。純粋関数なので、同じ設定と同じ既定からは常に同じ表が得られる。
    /// </para>
    /// <para>
    /// 重ね合わせは写し先ごとの置き換えである。設定に現れる写し先はその候補列に置き換わり、
    /// 現れない写し先は既定のままになる。<see cref="MetadataAliasCandidate.Defaults"/> は
    /// この時点で <paramref name="defaults"/> の候補列に展開される。
    /// </para>
    /// </remarks>
    public MetadataAliasTable Apply(
        MetadataAliasTable defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        var table = defaults;

        foreach (var target in this._order)
        {
            table = table.Set(target, Expand(this._overrides[target], defaults.GetAliases(target)));
        }

        return table;
    }

    /// <summary>
    /// 候補列の <see cref="MetadataAliasCandidate.Defaults"/> を既定の候補に展開する。
    /// </summary>
    /// <param name="candidates">利用者が書いた候補列。</param>
    /// <param name="defaults">その写し先についての既定の候補。</param>
    /// <returns>展開された候補列。</returns>
    /// <remarks>
    /// 重複の除去は行わない。<see cref="MetadataAliasTable.Set(MetadataKey, ImmutableArray{MetadataKey})"/>
    /// が写し先自身と重複を落とすので、ここで落とすと同じ規則が 2 箇所に書かれることになる。
    /// </remarks>
    private static ImmutableArray<MetadataKey> Expand(
        ImmutableArray<MetadataAliasCandidate> candidates,
        ImmutableArray<MetadataKey> defaults)
    {
        var keys = ImmutableArray.CreateBuilder<MetadataKey>(candidates.Length);

        foreach (var candidate in candidates)
        {
            if (candidate.IsDefaults)
            {
                keys.AddRange(defaults);
            }
            else
            {
                keys.Add(candidate.Key);
            }
        }

        return keys.DrainToImmutable();
    }
}
