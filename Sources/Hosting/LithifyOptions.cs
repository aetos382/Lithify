using System;
using System.Collections.Generic;

using Lithify.Abstractions;

namespace Lithify.Hosting;

/// <summary>
/// サイト全体の構成。
/// </summary>
/// <remarks>
/// <c>IOptions&lt;LithifyOptions&gt;</c> として束縛される。
/// appsettings.json や環境変数、コマンドライン引数から設定できる。
/// </remarks>
public sealed class LithifyOptions
{
    /// <summary>
    /// 入力のルート ディレクトリを取得または設定する。
    /// </summary>
    /// <remarks>
    /// 相対パスの場合はカレント ディレクトリからの相対になる。
    /// <see cref="ContentPath"/> はこのディレクトリからの相対パスである。
    /// </remarks>
    public string SourceRoot { get; set; } = ".";

    /// <summary>
    /// 出力のルート ディレクトリを取得または設定する。
    /// </summary>
    /// <remarks>
    /// このディレクトリは生成物であって編集対象ではない。
    /// 手編集した内容は次のビルドで予告なく失われる。
    /// </remarks>
    public string OutputRoot { get; set; } = "_site";

    /// <summary>
    /// ビルド キャッシュを置くディレクトリを取得または設定する。
    /// </summary>
    public string CacheRoot { get; set; } = ".lithify";

    /// <summary>
    /// サイトの正規 URL を取得または設定する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// フィードや <c>rel=canonical</c> のように絶対 URL が必要な場面で使う。
    /// </para>
    /// <para>
    /// 構成では文字列で書ける（<c>TypeConverter</c> 経由で束縛される）。
    /// <see cref="Uri"/> で持つのは、<see cref="OutputPath.ToUrlPath"/> の結果と
    /// 連結する側が毎回 <c>new Uri(...)</c> を書かずに済むようにするため。
    /// </para>
    /// </remarks>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// サイトのタイトルを取得または設定する。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// ファイル アクセスのポリシーを取得または設定する。
    /// </summary>
    public FileAccessPolicy FileAccess { get; set; } = FileAccessPolicy.Default;

    /// <summary>
    /// キャッシュを無視して全出力を書き直すかどうかを取得または設定する。
    /// </summary>
    /// <remarks>
    /// 出力ディレクトリの手編集はサポートしないため、キャッシュを信頼できない場合の
    /// 逃げ道はこれだけである。<c>--force</c> で指定する。
    /// </remarks>
    public bool Force { get; set; }

    /// <summary>
    /// 診断を警告からエラーに昇格させるかどうかを取得または設定する。
    /// </summary>
    public bool TreatWarningsAsErrors { get; set; }

    /// <summary>
    /// 静的ファイルとしてそのままコピーする入力のグロブ パターンを取得する。
    /// </summary>
    public IList<string> StaticFilePatterns { get; } = [];
}
