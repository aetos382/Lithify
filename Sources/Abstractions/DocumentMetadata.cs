using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

using JetBrains.Annotations;

namespace Lithify.Abstractions;

/// <summary>
/// 文書に付随するメタデータ。
/// </summary>
/// <remarks>
/// <para>
/// フロント マターという具体を抽象化していないことに注意。フロント マターは CommonMark 仕様にはなく
/// Markdig の拡張であり、AsciiDoc には存在しない（document attributes を使う）。
/// したがって抽象はメタデータの<em>モデル</em>に置き、供給手段は各パーサーの責務とする。
/// </para>
/// <para>
/// 各パーサーは自形式のネイティブな名前（AsciiDoc の <c>doctitle</c> / <c>revdate</c>）を
/// <see cref="WellKnownMetadata"/> のキーに写す責務を負う。元の名前も保持したまま
/// well-known キーを追加で生やすので、情報は失われない。
/// </para>
/// <para>
/// 各項目の出所（<see cref="MetadataProvenance"/>）は <see cref="Entries"/> と対になる
/// <em>疎な</em>副表 <see cref="Origins"/> に持つ。値そのものに持たせない理由は
/// <see cref="MetadataProvenance"/> に記す。出所の記録は任意であり、
/// 記録しない項目は <see cref="MetadataOrigin.Unknown"/> として読める。
/// </para>
/// <para>
/// <strong>等価性は 2 つの辞書の内容で決まる。</strong>
/// <see cref="ImmutableDictionary{TKey, TValue}"/> の既定の等価性は参照比較なので、
/// <c>record</c> が生成する <c>Equals</c> のままでは、同じフロント マターから
/// 別々に組み立てた 2 つの <see cref="DocumentMetadata"/> が等しくならない。
/// <see cref="IContentParser.ParseMetadataAsync"/> の結果が
/// <c>ParseAsync(...).Document.Metadata</c> と一致するという契約は等価性で検証されるので、
/// 内容で比較しなければ契約が表現できない。
/// </para>
/// </remarks>
public sealed record DocumentMetadata
{
    /// <summary>
    /// 空のメタデータ。
    /// </summary>
    public static DocumentMetadata Empty { get; } = new();

    /// <summary>
    /// キーと値の対応を取得する。
    /// </summary>
    public ImmutableDictionary<MetadataKey, MetadataValue> Entries { get; init; } = [];

    /// <summary>
    /// 各項目の出所を取得する。
    /// </summary>
    /// <remarks>
    /// <see cref="Entries"/> に対する疎な副表である。出所が記録されていない項目はここに現れない。
    /// <see cref="Entries"/> に無いキーがここにあってはならないが、型では強制していない
    /// （出所は診断の補助であり、不整合があっても値の読み取りは壊れないため）。
    /// </remarks>
    public ImmutableDictionary<MetadataKey, MetadataProvenance> Origins { get; init; } = [];

    /// <summary>
    /// 項目と出所の内容が等しいかどうかを判定する。
    /// </summary>
    /// <param name="other">比較対象。</param>
    /// <returns>等しい場合は <see langword="true"/>。</returns>
    public bool Equals(
        DocumentMetadata? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null &&
            DictionaryEquality.Equals(this.Entries, other.Entries) &&
            DictionaryEquality.Equals(this.Origins, other.Origins);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            DictionaryEquality.GetHashCode(this.Entries),
            DictionaryEquality.GetHashCode(this.Origins));
    }

    /// <summary>
    /// 指定したキーの値を取得する。
    /// </summary>
    /// <param name="key">キー。</param>
    /// <param name="value">見つかった値。</param>
    /// <returns>キーが存在する場合は <see langword="true"/>。</returns>
    [Pure]
    public bool TryGetValue(
        MetadataKey key,
        [MaybeNullWhen(false)] out MetadataValue value)
    {
        return this.Entries.TryGetValue(key, out value);
    }

    /// <summary>
    /// 指定したキーの項目の出所を取得する。
    /// </summary>
    /// <param name="key">キー。</param>
    /// <returns>
    /// 記録されている出所。記録されていない場合と、キー自体が存在しない場合は
    /// <see cref="MetadataProvenance.Unknown"/>。
    /// </returns>
    /// <remarks>
    /// キーの不在と出所の未記録を区別しないのは、出所が診断の補助でしかなく、
    /// 「値はあるが出所が分からない」と「値が無い」で呼び出し側の処理が変わらないためである。
    /// 値の有無は <see cref="TryGetValue"/> で判断する。
    /// </remarks>
    [Pure]
    public MetadataProvenance GetProvenance(
        MetadataKey key)
    {
        return this.Origins.TryGetValue(key, out var provenance)
            ? provenance
            : MetadataProvenance.Unknown;
    }

    /// <summary>
    /// 指定したキーの値をスカラーの文字列として取得する。
    /// </summary>
    /// <param name="key">キー。</param>
    /// <returns>スカラーの文字列表現。キーが存在しない場合、またはスカラーとして読めない場合は <see langword="null"/>。</returns>
    /// <remarks>
    /// 存在しないキーと読めない値をどちらも <see langword="null"/> にしているのは、
    /// メタデータの読み取りが本質的に best-effort であり、
    /// 個々のキーの型を厳格に検査する意味が薄いためである（検証は消費者が行う）。
    /// </remarks>
    [Pure]
    public string? GetScalarOrDefault(
        MetadataKey key)
    {
        if (!this.Entries.TryGetValue(key, out var value))
        {
            return null;
        }

        return value.IsScalar ? value.AsScalar() : null;
    }

    /// <summary>
    /// 指定したキーの値を文字列の並びとして取得する。
    /// </summary>
    /// <param name="key">キー。</param>
    /// <returns>
    /// 文字列の並び。キーが存在しない場合は空。
    /// スカラーとして読めない要素は除かれる。
    /// </returns>
    /// <remarks>
    /// <c>tags</c> の読み取りに用いる。単一の値も長さ 1 の並びとして読める。
    /// </remarks>
    [Pure]
    public ImmutableArray<string> GetScalarSequence(
        MetadataKey key)
    {
        if (!this.Entries.TryGetValue(key, out var value))
        {
            return [];
        }

        var items = value.AsSequence();
        var builder = ImmutableArray.CreateBuilder<string>(items.Length);

        foreach (var item in items)
        {
            if (item.IsScalar)
            {
                builder.Add(item.AsScalar());
            }
        }

        return builder.DrainToImmutable();
    }

    /// <summary>
    /// 既定値を下敷きにしたメタデータを返す。
    /// </summary>
    /// <param name="defaults">下敷きにする既定値。</param>
    /// <returns>このメタデータの項目が <paramref name="defaults"/> の同名の項目を上書きしたもの。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="defaults"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// <para>
    /// サイト全体の既定メタデータを個々の文書に重ねるために用いる。
    /// 入れ子の <see cref="MetadataValue.Mapping"/> は再帰的に合成せず、まるごと置き換える。
    /// 部分的な合成は「どちらの階層の既定が効いているのか」が追えなくなるためである。
    /// </para>
    /// <para>
    /// 出所は値と一緒に運ばれる。<paramref name="defaults"/> から採られた項目には
    /// <paramref name="defaults"/> が記録していた出所が付く。ここで
    /// <see cref="MetadataOrigin.Defaults"/> に書き換えたりはしない。
    /// 既定値であることの記録は既定値を組み立てる側の責務であり
    /// （<see cref="MetadataProvenance.FromDefaults"/> を stamp する）、
    /// 合成側が出所を解釈し始めると「既定値の既定値」で意味が崩れる。
    /// </para>
    /// <para>
    /// ディレクトリごとの既定値は、この演算を外側から内側へ繰り返すことで表す
    /// （<c>doc.WithFallback(postsDefaults).WithFallback(siteDefaults)</c>）。
    /// 各層が自分の <see cref="MetadataProvenance.FromDefaults"/> を stamp してから重ねるので、
    /// 合成後も「どの層の既定が効いているか」が項目ごとに残る。
    /// この演算自体は層の数を知らないので、層の構成は消費者側の関心に留まる。
    /// </para>
    /// </remarks>
    [Pure]
    public DocumentMetadata WithFallback(
        DocumentMetadata defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        if (defaults.Entries.IsEmpty)
        {
            return this;
        }

        if (this.Entries.IsEmpty)
        {
            return defaults;
        }

        return this with
        {
            Entries = defaults.Entries.SetItems(this.Entries),
            Origins = MergeOrigins(defaults.Origins, this.Entries, this.Origins),
        };
    }

    /// <summary>
    /// 下敷きの出所に上書き側の出所を重ねる。
    /// </summary>
    /// <param name="baseOrigins">下敷きの出所。</param>
    /// <param name="overrideEntries">上書き側の項目。</param>
    /// <param name="overrideOrigins">上書き側の出所。</param>
    /// <returns>合成された出所。</returns>
    /// <remarks>
    /// 出所は疎なので、単に <c>SetItems</c> で重ねると
    /// 「上書き側が値を差し替えたが出所を記録していない」キーに下敷きの出所が残り、
    /// 誤った出所を指すことになる。値を差し替えたキーの出所は先に落とす。
    /// </remarks>
    private static ImmutableDictionary<MetadataKey, MetadataProvenance> MergeOrigins(
        ImmutableDictionary<MetadataKey, MetadataProvenance> baseOrigins,
        ImmutableDictionary<MetadataKey, MetadataValue> overrideEntries,
        ImmutableDictionary<MetadataKey, MetadataProvenance> overrideOrigins)
    {
        if (baseOrigins.IsEmpty)
        {
            return overrideOrigins;
        }

        var builder = baseOrigins.ToBuilder();

        foreach (var entry in overrideEntries)
        {
            if (!overrideOrigins.ContainsKey(entry.Key))
            {
                builder.Remove(entry.Key);
            }
        }

        foreach (var origin in overrideOrigins)
        {
            builder[origin.Key] = origin.Value;
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// 指定した項目を追加または上書きしたメタデータを返す。
    /// </summary>
    /// <param name="key">キー。</param>
    /// <param name="value">値。</param>
    /// <returns>項目が設定されたメタデータ。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// 出所は記録されない。既に記録があった場合は<em>取り除かれる</em>。
    /// 値を差し替えたのに古い出所が残ると、実際とは違う場所を指す出所になるためである。
    /// 出所を記録するには <see cref="SetItem(MetadataKey, MetadataValue, MetadataProvenance)"/> を使う。
    /// </remarks>
    [Pure]
    public DocumentMetadata SetItem(
        MetadataKey key,
        MetadataValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return this with
        {
            Entries = this.Entries.SetItem(key, value),
            Origins = this.Origins.Remove(key),
        };
    }

    /// <summary>
    /// 指定した項目を出所とともに追加または上書きしたメタデータを返す。
    /// </summary>
    /// <param name="key">キー。</param>
    /// <param name="value">値。</param>
    /// <param name="provenance">この項目の出所。</param>
    /// <returns>項目が設定されたメタデータ。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// パーサーが自形式のネイティブな名前を <see cref="WellKnownMetadata"/> のキーに写すときに用いる。
    /// その場合の出所は <see cref="MetadataProvenance.Mapped"/> で、写し元のネイティブ名を渡す。
    /// </remarks>
    [Pure]
    public DocumentMetadata SetItem(
        MetadataKey key,
        MetadataValue value,
        MetadataProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(value);

        return this with
        {
            Entries = this.Entries.SetItem(key, value),
            Origins = provenance.IsUnknown
                ? this.Origins.Remove(key)
                : this.Origins.SetItem(key, provenance),
        };
    }

    /// <summary>
    /// キーと値の並びから <see cref="DocumentMetadata"/> を生成する。
    /// </summary>
    /// <param name="entries">キーと値の並び。同じキーが複数ある場合は後のものが勝つ。</param>
    /// <returns>生成されたメタデータ。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// 出所は記録されない。記録するには
    /// <see cref="Create(IEnumerable{MetadataEntry})"/> を使う。
    /// </remarks>
    [Pure]
    public static DocumentMetadata Create(
        IEnumerable<KeyValuePair<MetadataKey, MetadataValue>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var builder = ImmutableDictionary.CreateBuilder<MetadataKey, MetadataValue>();

        foreach (var entry in entries)
        {
            builder[entry.Key] = entry.Value;
        }

        return builder.Count == 0
            ? Empty
            : new DocumentMetadata { Entries = builder.ToImmutable() };
    }

    /// <summary>
    /// 出所を伴う項目の並びから <see cref="DocumentMetadata"/> を生成する。
    /// </summary>
    /// <param name="entries">項目の並び。同じキーが複数ある場合は後のものが勝つ（出所も後のものになる）。</param>
    /// <returns>生成されたメタデータ。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entries"/> が <see langword="null"/> である、
    /// または <see cref="MetadataEntry.Value"/> が <see langword="null"/> の項目を含む。
    /// </exception>
    /// <remarks>
    /// パーサーがフロント マターや document attributes を読みながら組み立てる正規の入口である。
    /// </remarks>
    [Pure]
    public static DocumentMetadata Create(
        IEnumerable<MetadataEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var values = ImmutableDictionary.CreateBuilder<MetadataKey, MetadataValue>();
        var origins = ImmutableDictionary.CreateBuilder<MetadataKey, MetadataProvenance>();

        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry.Value, nameof(entries));

            values[entry.Key] = entry.Value;

            if (entry.Provenance.IsUnknown)
            {
                origins.Remove(entry.Key);
            }
            else
            {
                origins[entry.Key] = entry.Provenance;
            }
        }

        return values.Count == 0
            ? Empty
            : new DocumentMetadata
            {
                Entries = values.ToImmutable(),
                Origins = origins.ToImmutable(),
            };
    }
}

/// <summary>
/// メタデータの 1 項目を、キー・値・出所の 3 つ組で表す。
/// </summary>
/// <param name="Key">キー。</param>
/// <param name="Value">値。</param>
/// <param name="Provenance">出所。記録しない場合は <see cref="MetadataProvenance.Unknown"/>。</param>
/// <remarks>
/// <see cref="DocumentMetadata.Create(IEnumerable{MetadataEntry})"/> への入力にのみ用いる、
/// 組み立て時の運搬用の型である。組み立て後の保持は
/// <see cref="DocumentMetadata.Entries"/> と <see cref="DocumentMetadata.Origins"/> の 2 つの辞書で行う。
/// 出所は疎なので、この 3 つ組をそのまま辞書の値にすると
/// 出所を持たない大多数の項目にも <see cref="MetadataProvenance"/> の領域が付く。
/// </remarks>
public readonly record struct MetadataEntry(
    MetadataKey Key,
    MetadataValue Value,
    MetadataProvenance Provenance)
{
    /// <summary>
    /// 出所を記録しない項目を生成する。
    /// </summary>
    /// <param name="key">キー。</param>
    /// <param name="value">値。</param>
    public MetadataEntry(
        MetadataKey key,
        MetadataValue value)
        : this(key, value, MetadataProvenance.Unknown)
    {
    }
}
