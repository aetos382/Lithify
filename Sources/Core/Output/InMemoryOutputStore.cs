using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using Lithify.Abstractions;

namespace Lithify.Core.Output;

/// <summary>
/// メモリ上に出力を保持する <see cref="IOutputStore"/>。
/// </summary>
/// <remarks>
/// <para>
/// これはテスト用のフェイクではない。開発サーバーがディスクに書かずに配信するための
/// 正規の実装である。<c>Lithify.Testing</c> に閉じ込めると開発サーバーが
/// <see cref="IOutputStore"/> を迂回する別経路を持つことになる。
/// </para>
/// <para>
/// 複数のスレッドから安全に使用できる。バックグラウンド ビルドと前景のリクエストが
/// 同時に書き込む可能性があるためである。
/// </para>
/// </remarks>
public sealed class InMemoryOutputStore :
    IOutputStore
{
    private readonly ConcurrentDictionary<OutputPath, Entry> _entries = new();

    /// <summary>
    /// 格納されている出力の数を取得する。
    /// </summary>
    public int Count =>
        this._entries.Count;

    /// <summary>
    /// 格納されている出力の内容を取得する。
    /// </summary>
    /// <param name="path">出力パス。</param>
    /// <param name="content">出力の内容。</param>
    /// <returns>出力が存在する場合は <see langword="true"/>。</returns>
    public bool TryGetContent(
        OutputPath path,
        out ReadOnlyMemory<byte> content)
    {
        if (this._entries.TryGetValue(path, out var entry))
        {
            content = entry.Content;

            return true;
        }

        content = default;

        return false;
    }

    /// <summary>
    /// すべての出力を削除する。
    /// </summary>
    public void Clear()
    {
        this._entries.Clear();
    }

    /// <inheritdoc />
    public ValueTask<Fingerprint?> TryGetFingerprintAsync(
        OutputPath path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult<Fingerprint?>(
            this._entries.TryGetValue(path, out var entry)
                ? entry.Fingerprint
                : null);
    }

    /// <inheritdoc />
    public ValueTask<WriteOutcome> WriteAsync(
        OutputPath path,
        ReadOnlySequence<byte> content,
        Fingerprint fingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = this._entries.TryGetValue(path, out var entry)
            ? entry.Fingerprint
            : (Fingerprint?)null;

        var outcome = OutputDecision.Decide(fingerprint, existing);

        if (outcome != WriteOutcome.Unchanged)
        {
            this._entries[path] = new Entry(content.ToArray(), fingerprint);
        }

        return ValueTask.FromResult(outcome);
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(
        OutputPath path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(this._entries.TryRemove(path, out _));
    }

    /// <inheritdoc />
    public ValueTask<Stream?> OpenReadAsync(
        OutputPath path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!this._entries.TryGetValue(path, out var entry))
        {
            return ValueTask.FromResult<Stream?>(null);
        }

        // 書き込み不可のビューを返す。呼び出し側がストリーム経由で
        // メモ化された内容を書き換えられてはならない。
        Stream stream = new MemoryStream(entry.Content, writable: false);

        return ValueTask.FromResult<Stream?>(stream);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<OutputPath> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var path in this._entries.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return path;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private readonly record struct Entry(
        byte[] Content,
        Fingerprint Fingerprint);
}
