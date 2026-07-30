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
    /// サイト全体の既定メタデータを個々の文書に重ねるために用いる。
    /// 入れ子の <see cref="MetadataValue.Mapping"/> は再帰的に合成せず、まるごと置き換える。
    /// 部分的な合成は「どちらの階層の既定が効いているのか」が追えなくなるためである。
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

        return this with { Entries = defaults.Entries.SetItems(this.Entries) };
    }

    /// <summary>
    /// 指定した項目を追加または上書きしたメタデータを返す。
    /// </summary>
    /// <param name="key">キー。</param>
    /// <param name="value">値。</param>
    /// <returns>項目が設定されたメタデータ。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// パーサーが自形式のネイティブな名前を <see cref="WellKnownMetadata"/> のキーに写すときに用いる。
    /// </remarks>
    [Pure]
    public DocumentMetadata SetItem(
        MetadataKey key,
        MetadataValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return this with { Entries = this.Entries.SetItem(key, value) };
    }

    /// <summary>
    /// キーと値の並びから <see cref="DocumentMetadata"/> を生成する。
    /// </summary>
    /// <param name="entries">キーと値の並び。同じキーが複数ある場合は後のものが勝つ。</param>
    /// <returns>生成されたメタデータ。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> が <see langword="null"/> である。</exception>
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
}
