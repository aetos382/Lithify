using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lithify.Abstractions;

/// <summary>
/// パーサーに渡される 1 つのコンテンツ。
/// </summary>
/// <remarks>
/// <para>
/// 本文を <see cref="Stream"/> でも <see cref="string"/> でもなく遅延読み取りにしているのは、
/// メタデータのみを読む軽量パス（<see cref="IContentParser.ParseMetadataAsync"/>）が
/// 文書全体を読まずに済むようにするためである。オンデマンド ビルドでは
/// 「1 ページ表示するためにサイト全体のタグを集める」必要があり、
/// そこで全記事を完全に読み込んでいては需要駆動の利点が消える。
/// </para>
/// <para>
/// <see cref="Fingerprint"/> を含むのは、パーサーがこれを計算ノードの入力として
/// 使えるようにするためである。内容が変わらなければパースも走らない。
/// </para>
/// </remarks>
public sealed class ContentSource
{
    private readonly Func<CancellationToken, ValueTask<string>> _reader;

    /// <summary>
    /// <see cref="ContentSource"/> を生成する。
    /// </summary>
    /// <param name="path">このコンテンツの位置。</param>
    /// <param name="format">このコンテンツの形式。</param>
    /// <param name="fingerprint">内容のフィンガープリント。</param>
    /// <param name="reader">本文を読み取る関数。</param>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> が <see langword="null"/> である。</exception>
    public ContentSource(
        ContentPath path,
        ContentFormat format,
        Fingerprint fingerprint,
        Func<CancellationToken, ValueTask<string>> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        this.Path = path;
        this.Format = format;
        this.Fingerprint = fingerprint;
        this._reader = reader;
    }

    /// <summary>
    /// このコンテンツの位置を取得する。
    /// </summary>
    public ContentPath Path { get; }

    /// <summary>
    /// このコンテンツの形式を取得する。
    /// </summary>
    public ContentFormat Format { get; }

    /// <summary>
    /// 内容のフィンガープリントを取得する。
    /// </summary>
    public Fingerprint Fingerprint { get; }

    /// <summary>
    /// 本文を読み取る。
    /// </summary>
    /// <param name="cancellationToken">取り消しトークン。</param>
    /// <returns>本文。</returns>
    /// <remarks>
    /// 複数回呼び出せる。結果をキャッシュするかどうかは実装に委ねられる。
    /// </remarks>
    public ValueTask<string> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        return this._reader(cancellationToken);
    }

    /// <summary>
    /// 文字列を内容とする <see cref="ContentSource"/> を生成する。
    /// </summary>
    /// <param name="path">このコンテンツの位置。</param>
    /// <param name="format">このコンテンツの形式。</param>
    /// <param name="text">本文。</param>
    /// <returns>生成された <see cref="ContentSource"/>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> が <see langword="null"/> である。</exception>
    /// <remarks>
    /// <para>
    /// フィンガープリントは本文から計算される。テストと、
    /// すでにメモリ上にある内容を渡す場合に用いる。
    /// </para>
    /// <para>
    /// <strong>対応するファイルが実在しないなら <see cref="ContentPath.InMemory"/> を渡す。</strong>
    /// 架空の <c>posts/x.md</c> を渡すと <see cref="Diagnostic.Path"/> が実在しない位置を騙り、
    /// 利用者はそのファイルを探すことになる。逆に、実在するローカル ファイルの内容を
    /// すでに読み終えていて渡すだけの場合は、そのパスを渡すのが正しい
    /// （<see cref="ContentPathKind"/> は取得手段ではなくアドレス空間を表す）。
    /// </para>
    /// </remarks>
    public static ContentSource FromText(
        ContentPath path,
        ContentFormat format,
        string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return new ContentSource(
            path,
            format,
            Fingerprint.OfString(text),
            _ => ValueTask.FromResult(text));
    }
}
