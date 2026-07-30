using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Lithify.Abstractions;

namespace Lithify.Core.Output;

/// <summary>
/// ファイルシステム上に出力を書き込む <see cref="IOutputStore"/>。
/// </summary>
/// <remarks>
/// <para>
/// 既存の出力のフィンガープリントは <see cref="IBuildCache"/> から取得し、実ファイルは読まない。
/// これは手抜きではなく設計判断である。出力ディレクトリの手編集を検知しようとしても、
/// 更新日時とサイズでは中身の差し替えを見逃すため中途半端であり、それでいて全出力の
/// ハッシュ再計算は開発サーバーの応答時間と正面衝突する。
/// </para>
/// <para>
/// <see cref="WriteOutcome.Unchanged"/> のとき、ファイルには一切触れない。
/// 更新日時を変えないという要件（R7）は、書き込みを省略することでのみ満たされる。
/// </para>
/// </remarks>
public sealed class FileSystemOutputStore :
    IOutputStore
{
    private readonly IBuildCache _cache;

    /// <summary>
    /// 指定したルート ディレクトリに書き込む <see cref="FileSystemOutputStore"/> を生成する。
    /// </summary>
    /// <param name="root">出力ルートの絶対パス。</param>
    /// <param name="cache">既存の出力のフィンガープリントを記録するビルド キャッシュ。</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="root"/> または <paramref name="cache"/> が <see langword="null"/> である。
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="root"/> が絶対パスではない。</exception>
    public FileSystemOutputStore(
        string root,
        IBuildCache cache)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentNullException.ThrowIfNull(cache);

        if (!Path.IsPathFullyQualified(root))
        {
            throw new ArgumentException(
                Messages.FormatOutputStoreRootMustBeAbsolute(root),
                nameof(root));
        }

        this.Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        this._cache = cache;
    }

    /// <summary>
    /// 出力ルートの絶対パスを取得する。
    /// </summary>
    public string Root { get; }

    /// <inheritdoc />
    public ValueTask<Fingerprint?> TryGetFingerprintAsync(
        OutputPath path,
        CancellationToken cancellationToken = default)
    {
        return this._cache.TryGetOutputFingerprintAsync(path, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<WriteOutcome> WriteAsync(
        OutputPath path,
        ReadOnlySequence<byte> content,
        Fingerprint fingerprint,
        CancellationToken cancellationToken = default)
    {
        _ = content;
        _ = fingerprint;
        _ = cancellationToken;

        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask<bool> DeleteAsync(
        OutputPath path,
        CancellationToken cancellationToken = default)
    {
        _ = path;
        _ = cancellationToken;

        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public ValueTask<Stream?> OpenReadAsync(
        OutputPath path,
        CancellationToken cancellationToken = default)
    {
        _ = path;
        _ = cancellationToken;

        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public IAsyncEnumerable<OutputPath> EnumerateAsync(
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        throw new NotImplementedException();
    }
}
