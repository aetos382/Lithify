using System;

using Lithify.Abstractions;

namespace Lithify.Core.Fragments;

/// <summary>
/// 描画済みのフラグメント。
/// </summary>
/// <param name="Id">フラグメントの識別子。</param>
/// <param name="Utf8Html">UTF-8 として符号化された HTML 断片。</param>
/// <param name="Fingerprint">内容から決まるフィンガープリント。</param>
/// <remarks>
/// <para>
/// 内容を <see cref="string"/> ではなく UTF-8 バイト列で保持する。出力・フィンガープリントの計算・
/// HTTP レスポンスへの転送がすべてバイト列であるため、ここで <see cref="string"/> を持つと
/// ページを合成するたびに全フラグメントの再符号化が走り、フラグメントに分割した意義が失われる。
/// </para>
/// <para>
/// このバイト列は不変として扱わなければならない。<see cref="ReadOnlyMemory{T}"/> は
/// 参照先の書き換えを防げないため、生成側は自分だけが保持する配列を渡す責務を持つ。
/// メモ化された値であり、複数のページから同時に参照される。
/// </para>
/// </remarks>
public sealed record RenderedFragment(
    FragmentId Id,
    ReadOnlyMemory<byte> Utf8Html,
    Fingerprint Fingerprint) :
    IFingerprintable
{
    /// <summary>
    /// 指定した内容から、フィンガープリントを計算して <see cref="RenderedFragment"/> を生成する。
    /// </summary>
    /// <param name="id">フラグメントの識別子。</param>
    /// <param name="utf8Html">UTF-8 として符号化された HTML 断片。</param>
    /// <returns>生成されたフラグメント。</returns>
    /// <remarks>
    /// フィンガープリントに <paramref name="id"/> を含める。同じ内容でも役割の違うフラグメントは
    /// 別の値として扱われるべきで、そうしないと空のサイドバーと空の本文が同一視されてしまう。
    /// </remarks>
    public static RenderedFragment Create(
        FragmentId id,
        ReadOnlyMemory<byte> utf8Html)
    {
        var fingerprint = Fingerprint.Combine(
            Fingerprint.OfString(id.Value),
            Fingerprint.OfBytes(utf8Html.Span));

        return new RenderedFragment(id, utf8Html, fingerprint);
    }
}
