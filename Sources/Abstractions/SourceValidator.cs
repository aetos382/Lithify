using System;
using System.Collections.Immutable;

namespace Lithify.Abstractions;

/// <summary>
/// 「取り直す必要があるか」を判断するための、プロバイダごとの不透明なトークン。
/// </summary>
/// <remarks>
/// <para>
/// <strong>これは <see cref="Fingerprint"/> ではない。混同してはならない。</strong>
/// <see cref="Fingerprint"/> は内容バイト列のハッシュであり内容の同一性を表すが、
/// こちらは「取り直す必要があるか」の代理にすぎない。HTTP の ETag は
/// nginx の inode 由来の値・ミラーの切り替え・CDN による表現の変化のいずれでも、
/// <em>同じバイト列に別の値</em>が付く。これを early cutoff の根拠に据えると
/// 増分ビルドの正しさが壊れる。ゆえに 2 つの別概念にしてある。
/// </para>
/// <para>
/// <strong>中身はプロバイダの私事であり、他の誰も解釈してはならない。</strong>
/// 抽象に ETag や 304 を出さないのは、HTTP を特別扱いする根拠が中核側に無いからである。
/// HTTP なら ETag / Last-Modified、FTP なら <c>MDTM</c> と <c>SIZE</c>、
/// git なら commit SHA、ローカル ファイルなら更新時刻とサイズ。
/// <em>ローカルがこの抽象の特殊例になる</em>のが実際の見返りである。
/// </para>
/// <para>
/// <see cref="ProviderId"/> を持つのは、他のプロバイダのトークンを誤解釈しないためである。
/// プロバイダの構成が変わると、前回のトークンが別のプロバイダのものになりうる。
/// これは<em>検証</em>の関心なのでここが正しい置き場所であり、
/// <see cref="ContentPath"/> に持ち上げてはならない（同一性が壊れる）。
/// </para>
/// </remarks>
public readonly record struct SourceValidator
{
    private readonly ImmutableArray<byte> _token;

    /// <summary>
    /// <see cref="SourceValidator"/> を生成する。
    /// </summary>
    /// <param name="providerId">このトークンを発行したプロバイダの識別子。</param>
    /// <param name="token">トークンの中身。</param>
    /// <exception cref="ArgumentNullException"><paramref name="providerId"/> が <see langword="null"/> である。</exception>
    public SourceValidator(
        string providerId,
        ImmutableArray<byte> token)
    {
        ArgumentNullException.ThrowIfNull(providerId);

        this.ProviderId = providerId;
        this._token = token;
    }

    /// <summary>
    /// このトークンを発行したプロバイダの識別子を取得する。
    /// </summary>
    public string ProviderId { get; }

    /// <summary>
    /// トークンの中身を取得する。
    /// </summary>
    /// <remarks>
    /// 発行したプロバイダ以外がこの内容を解釈してはならない。
    /// </remarks>
    public ImmutableArray<byte> Token =>
        this._token.IsDefault ? [] : this._token;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// トークンは<em>内容</em>で比較する。<see cref="ImmutableArray{T}"/> の既定の等価性は
    /// 基になる配列の参照比較なので、コンパイラ生成の実装をそのまま使うと、
    /// 永続化して読み直したトークンが元のものと等しくならない。
    /// この型はビルドを跨いで保存されるので、それでは意味がない。
    /// </para>
    /// <para>
    /// <see cref="ProviderId"/> はオーディナル比較である。値はコードが決めるものであり、
    /// 利用者の入力ではないので、文化圏に依存する比較の余地はない。
    /// </para>
    /// </remarks>
    public bool Equals(
        SourceValidator other)
    {
        return string.Equals(this.ProviderId, other.ProviderId, StringComparison.Ordinal) &&
            this.Token.AsSpan().SequenceEqual(other.Token.AsSpan());
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();

        hash.Add(this.ProviderId, StringComparer.Ordinal);
        hash.AddBytes(this.Token.AsSpan());

        return hash.ToHashCode();
    }
}
