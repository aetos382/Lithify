using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lithify.Core;

/// <summary>
/// <see cref="IBufferWriter{T}"/> に UTF-8 として書き出す <see cref="TextWriter"/>。
/// </summary>
/// <remarks>
/// <para>
/// char から UTF-8 への変換境界はこの型が担う。テンプレート エンジン
/// （Handlebars.Net / Fluid / Blazor）とレンダラーはいずれも <see cref="TextWriter"/> に書くため
/// 生成側の型は変えられない。一方、フィンガープリントの計算・フラグメントの合成・
/// 出力の書き込み・HTTP レスポンスへの転送はすべて UTF-8 バイト列で行われる。
/// </para>
/// <para>
/// 変換が起きるのはフラグメントの生成時だけである。フラグメントはメモ化されるため、
/// サイドバーが変わって全ページを合成し直す場合でも本文の再変換は起きない（R8）。
/// もしフラグメントを <see cref="string"/> で保持すると、合成のたびに全ページ分の
/// 再符号化が走り、フラグメントに分割した意義が失われる。
/// </para>
/// <para>
/// サロゲート ペアが <see cref="Write(char)"/> の呼び出し 2 回に分かれても正しく変換されるよう、
/// <see cref="Encoder"/> は呼び出しを跨いで状態を保持する。したがってこの型はスレッド セーフではない。
/// </para>
/// </remarks>
public sealed class Utf8BufferTextWriter :
    TextWriter
{
    /// <summary>
    /// 1 回の変換で <see cref="IBufferWriter{T}"/> から借りるバッファーの最小サイズ。
    /// </summary>
    private const int MinimumBufferSize = 1024;

    private readonly IBufferWriter<byte> _writer;

    private readonly Encoder _encoder;

    private bool _disposed;

    /// <summary>
    /// 指定した書き込み先に対する <see cref="Utf8BufferTextWriter"/> を生成する。
    /// </summary>
    /// <param name="writer">UTF-8 バイト列の書き込み先。</param>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> が <see langword="null"/> である。</exception>
    public Utf8BufferTextWriter(
        IBufferWriter<byte> writer)
        : base(CultureInfo.InvariantCulture)
    {
        ArgumentNullException.ThrowIfNull(writer);

        this._writer = writer;

        // 不正なサロゲートは置換文字にする。ここで例外にすると、
        // 記事に壊れた文字が 1 つあるだけでサイト全体のビルドが落ちる。
        this._encoder = Encoding.UTF8.GetEncoder();
    }

    /// <inheritdoc />
    public override Encoding Encoding =>
        Encoding.UTF8;

    /// <inheritdoc />
    public override void Write(
        char value)
    {
        this.WriteCore(new ReadOnlySpan<char>(in value), flush: false);
    }

    /// <inheritdoc />
    public override void Write(
        char[] buffer,
        int index,
        int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        this.WriteCore(buffer.AsSpan(index, count), flush: false);
    }

    /// <inheritdoc />
    public override void Write(
        ReadOnlySpan<char> buffer)
    {
        this.WriteCore(buffer, flush: false);
    }

    /// <inheritdoc />
    public override void Write(
        string? value)
    {
        if (value is null)
        {
            return;
        }

        this.WriteCore(value.AsSpan(), flush: false);
    }

    /// <inheritdoc />
    public override Task WriteAsync(
        char value)
    {
        this.Write(value);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteAsync(
        char[] buffer,
        int index,
        int count)
    {
        this.Write(buffer, index, count);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteAsync(
        ReadOnlyMemory<char> buffer,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        this.WriteCore(buffer.Span, flush: false);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task WriteAsync(
        string? value)
    {
        this.Write(value);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 保留中の変換状態を書き出す。
    /// </summary>
    /// <remarks>
    /// 書き込み先が <see cref="IBufferWriter{T}"/> であって非同期の I/O を伴わないため、
    /// このメソッドは同期的に完了する。
    /// </remarks>
    public override void Flush()
    {
        // 末尾に単独の高位サロゲートが残っている場合、それを置換文字として確定させる。
        this.WriteCore([], flush: true);
    }

    /// <inheritdoc />
    public override Task FlushAsync()
    {
        this.Flush();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task FlushAsync(
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        this.Flush();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override void Dispose(
        bool disposing)
    {
        if (!this._disposed)
        {
            this._disposed = true;

            if (disposing)
            {
                this.Flush();
            }
        }

        base.Dispose(disposing);
    }

    private void WriteCore(
        ReadOnlySpan<char> buffer,
        bool flush)
    {
        ObjectDisposedException.ThrowIf(this._disposed, this);

        var remaining = buffer;

        // Convert は「変換先が足りなければ途中まで変換して completed = false を返す」という契約なので、
        // 入力を使い切るまで、あるいは flush が完了するまで繰り返す。
        while (true)
        {
            // 必要バイト数を GetByteCount で問い合わせると入力を 2 回走査することになる。
            // Convert は途中まで変換して completed = false を返す契約なので、
            // 固定サイズを借りて繰り返す方が安い。
            var destination = this._writer.GetSpan(MinimumBufferSize);

            this._encoder.Convert(
                remaining,
                destination,
                flush,
                out var charsUsed,
                out var bytesUsed,
                out var completed);

            this._writer.Advance(bytesUsed);

            remaining = remaining[charsUsed..];

            if (remaining.IsEmpty && (completed || !flush))
            {
                return;
            }
        }
    }
}
