using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.IO.Hashing;
using System.Text;

using JetBrains.Annotations;

namespace Lithify.Abstractions;

/// <summary>
/// 内容の同一性を判定するための 128 ビットのフィンガープリント。
/// </summary>
/// <param name="Value">ハッシュ値。</param>
/// <remarks>
/// <para>
/// 増分ビルドの基礎となる値。ノードの入力フィンガープリントが前回と一致すればそのノードは再計算されず、
/// 再計算しても出力フィンガープリントが前回と一致すれば下流も再計算されない（early cutoff）。
/// </para>
/// <para>
/// 暗号学的ハッシュではない。改竄の検出には使えない。
/// 同じ入力に対して同じ値を返すこと（ビルドを跨いで安定であること）だけを保証する。
/// </para>
/// <para>
/// <see langword="default"/> は「フィンガープリント未算出」を意味する番兵ではない。
/// 値の不在は <see cref="Nullable{T}"/> で表現すること。
/// </para>
/// </remarks>
public readonly record struct Fingerprint(UInt128 Value)
{
    /// <summary>
    /// <see cref="WriteTo"/> が書き出すバイト数。
    /// </summary>
    public const int ByteLength = 16;

    /// <summary>
    /// スタック上に確保する一時バッファーの上限。これを超える入力はプールから借りる。
    /// </summary>
    private const int StackAllocThreshold = 256;

    /// <summary>
    /// バイト列のフィンガープリントを計算する。
    /// </summary>
    /// <param name="bytes">対象のバイト列。</param>
    /// <returns>計算されたフィンガープリント。</returns>
    [Pure]
    public static Fingerprint OfBytes(
        ReadOnlySpan<byte> bytes)
    {
        return new Fingerprint(XxHash128.HashToUInt128(bytes));
    }

    /// <summary>
    /// 分割されたバイト列のフィンガープリントを計算する。
    /// </summary>
    /// <param name="bytes">対象のバイト列。</param>
    /// <returns>計算されたフィンガープリント。</returns>
    /// <remarks>
    /// 同じ内容の連続したバイト列に対する <see cref="OfBytes(ReadOnlySpan{byte})"/> と同じ値を返す。
    /// 断片化したバッファーに書き出されたフラグメントを、連結せずにハッシュするために用いる。
    /// </remarks>
    [Pure]
    public static Fingerprint OfBytes(
        in ReadOnlySequence<byte> bytes)
    {
        if (bytes.IsSingleSegment)
        {
            return OfBytes(bytes.FirstSpan);
        }

        var hash = new XxHash128();

        foreach (var segment in bytes)
        {
            hash.Append(segment.Span);
        }

        return new Fingerprint(hash.GetCurrentHashAsUInt128());
    }

    /// <summary>
    /// 文字列を UTF-8 として符号化したうえでフィンガープリントを計算する。
    /// </summary>
    /// <param name="value">対象の文字列。</param>
    /// <returns>計算されたフィンガープリント。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// UTF-8 を経由するのは、出力・キャッシュ・比較のすべてが UTF-8 バイト列で行われるため、
    /// 同じ内容の文字列とバイト列が同じフィンガープリントになるようにするためである。
    /// </remarks>
    [Pure]
    public static Fingerprint OfString(
        string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var length = Encoding.UTF8.GetByteCount(value);

        if (length <= StackAllocThreshold)
        {
            Span<byte> stack = stackalloc byte[StackAllocThreshold];
            var written = Encoding.UTF8.GetBytes(value, stack);

            return OfBytes(stack[..written]);
        }

        var buffer = ArrayPool<byte>.Shared.Rent(length);

        try
        {
            var written = Encoding.UTF8.GetBytes(value, buffer);

            return OfBytes(buffer.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// 複数のフィンガープリントを 1 つに合成する。
    /// </summary>
    /// <param name="parts">合成するフィンガープリント。</param>
    /// <returns>合成されたフィンガープリント。</returns>
    /// <remarks>
    /// 順序に依存する。<c>Combine(a, b)</c> と <c>Combine(b, a)</c> は異なる値になる。
    /// フラグメントの並びやノードの入力列を合成するには、この性質が必要である。
    /// </remarks>
    [Pure]
    public static Fingerprint Combine(
        params ReadOnlySpan<Fingerprint> parts)
    {
        var hash = new XxHash128();

        Span<byte> buffer = stackalloc byte[ByteLength];

        foreach (var part in parts)
        {
            BinaryPrimitives.WriteUInt128LittleEndian(buffer, part.Value);
            hash.Append(buffer);
        }

        return new Fingerprint(hash.GetCurrentHashAsUInt128());
    }

    /// <summary>
    /// このフィンガープリントをリトル エンディアンのバイト列として書き出す。
    /// </summary>
    /// <param name="destination">書き出し先。<see cref="ByteLength"/> バイト以上の長さが必要。</param>
    /// <returns>書き出したバイト数。常に <see cref="ByteLength"/> と等しい。</returns>
    /// <exception cref="ArgumentException"><paramref name="destination"/> が短すぎる。</exception>
    public int WriteTo(
        Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt128LittleEndian(destination, this.Value);

        return ByteLength;
    }

    /// <summary>
    /// このフィンガープリントを 32 桁の小文字 16 進数として表現する。
    /// </summary>
    /// <returns>16 進数表現。</returns>
    public override string ToString()
    {
        return this.Value.ToString("x32", CultureInfo.InvariantCulture);
    }
}
