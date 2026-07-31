using System;

using Lithify.Abstractions;

namespace Lithify.Core.Metadata;

/// <summary>
/// 別名の設定に現れる 1 つの候補。具体的なキーか、パーサーの既定の候補列を指す標のいずれかである。
/// </summary>
/// <remarks>
/// <para>
/// <strong><see cref="Defaults"/> のために型を設けている。</strong>「ここに既定の候補列が入る」を
/// <c>":default"</c> のような綴りで表すこともできるが、それは実在しうるキー名と衝突する
/// （<see cref="MetadataKey.Create"/> は <c>:</c> を含む名前を拒否しない）。
/// 特別な意味を持つ値を、通常の値と同じ型の中の特定の綴りに割り当ててはならない。
/// </para>
/// <para>
/// <see cref="string"/> からの暗黙の変換があるので、設定は
/// <c>["abstract", MetadataAliasCandidate.Defaults]</c> のようにコレクション式で書ける。
/// </para>
/// </remarks>
// 暗黙の変換の代替は Create である。CA2225 が求める FromString / FromMetadataKey には
// しない。この型の生成の入口は MetadataKey.Create と同じ綴りで揃っているべきで、
// 変換元の型名を綴りに含めると 2 つの Create の関係が読めなくなる。
#pragma warning disable CA2225 // Operator overloads have named alternates
public readonly record struct MetadataAliasCandidate
{
    private readonly MetadataKey _key;

    private MetadataAliasCandidate(
        MetadataKey key)
    {
        this._key = key;
    }

    /// <summary>
    /// パーサーの既定の候補列を指す標を取得する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 設定の中でこの標が現れた位置に、そのパーサーの既定の候補が順序を保って展開される。
    /// 末尾に置けば「まず自分の語彙、それが無ければ Lithify が知っている綴り」になる。
    /// </para>
    /// <para>
    /// <strong>既定への依存が設定に書かれていることが要点である。</strong> 別名の設定は
    /// 写し先ごとの置き換えであり、既定の中身を暗黙に基準にはしない
    /// （<see cref="MetadataAliasOptions"/> にその理由を記す）。この標はその例外だが、
    /// 依存していることが設定を読めば分かるので、暗黙の差分とは性質が違う。
    /// 既定が変われば結果も変わるが、それはこの標を書いた人が求めたことである。
    /// </para>
    /// </remarks>
    public static MetadataAliasCandidate Defaults { get; }

    /// <summary>
    /// この候補が <see cref="Defaults"/> であるかどうかを示す値を取得する。
    /// </summary>
    public bool IsDefaults =>
        this._key.IsEmpty;

    /// <summary>
    /// この候補が指すキーを取得する。
    /// </summary>
    /// <exception cref="InvalidOperationException">この候補が <see cref="Defaults"/> である。</exception>
    public MetadataKey Key =>
        this.IsDefaults
            ? throw new InvalidOperationException(Messages.MetadataAliasCandidateIsDefaults)
            : this._key;

    /// <summary>
    /// 生のキー名を正規化して候補を生成する。
    /// </summary>
    /// <param name="raw">正規化前のキー名。</param>
    /// <returns>候補。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="raw"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException"><paramref name="raw"/> が空または空白のみである。</exception>
    public static MetadataAliasCandidate Create(
        string raw)
    {
        return new MetadataAliasCandidate(MetadataKey.Create(raw));
    }

    /// <summary>
    /// キーから候補を生成する。
    /// </summary>
    /// <param name="key">キー。</param>
    /// <returns>候補。</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> が <see langword="default"/> である。</exception>
    public static MetadataAliasCandidate Create(
        MetadataKey key)
    {
        if (key.IsEmpty)
        {
            throw new ArgumentException(Messages.MetadataAliasCandidateMustNotBeEmpty, nameof(key));
        }

        return new MetadataAliasCandidate(key);
    }

    /// <summary>
    /// キー名を候補に変換する。
    /// </summary>
    /// <param name="raw">正規化前のキー名。</param>
    /// <exception cref="ArgumentNullException"><paramref name="raw"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException"><paramref name="raw"/> が空または空白のみである。</exception>
    public static implicit operator MetadataAliasCandidate(
        string raw)
    {
        return Create(raw);
    }

    /// <summary>
    /// キーを候補に変換する。
    /// </summary>
    /// <param name="key">キー。</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> が <see langword="default"/> である。</exception>
    public static implicit operator MetadataAliasCandidate(
        MetadataKey key)
    {
        return Create(key);
    }

    /// <summary>
    /// この候補を表す文字列を返す。
    /// </summary>
    /// <returns>キー名、または <see cref="Defaults"/> を表す文字列。</returns>
    public override string ToString()
    {
        return this.IsDefaults ? "(defaults)" : this._key.Value;
    }
}
#pragma warning restore CA2225
