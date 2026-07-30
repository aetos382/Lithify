using System.Collections.Immutable;

namespace Lithify.Abstractions;

/// <summary>
/// パーサーやテンプレート エンジンによる外部ファイル アクセスの許可範囲。
/// </summary>
/// <remarks>
/// <para>
/// Asciidoctor の safe mode は AsciiDoc <em>言語仕様</em>ではなく Asciidoctor の実装機能である
/// （プロセッサに何を許すかを制御する API / CLI の概念であり、言語仕様が定めるのは
/// 構文・文書構造・属性・プリプロセッサ指令の意味論まで）。
/// </para>
/// <para>
/// したがって safe mode が扱っていた関心は AsciiDoc 固有ではない。
/// 「include を許すか」「どのディレクトリの外に出られないか」は Handlebars や Liquid の
/// partial 解決にも Markdown 拡張の include にも同じく効き、実際にファイル アクセスを
/// 握っているのは Lithify である。ゆえにこれは <c>Lithify.AsciiDoc.Abstractions</c> ではなく
/// <c>Lithify.Abstractions</c> に、Lithify の語彙で置かれている。
/// </para>
/// <para>
/// Asciidoctor 互換エンジン固有の <c>SafeMode</c> が必要になった場合は、
/// 実装パッケージ側のエンジン固有オプションに置く。
/// </para>
/// </remarks>
public sealed record FileAccessPolicy
{
    /// <summary>
    /// 既定のポリシー。サイト ルート配下のみを許可し、シンボリック リンクを許可しない。
    /// </summary>
    /// <remarks>
    /// include は許可する。include を禁じると AsciiDoc の一般的な文書が壊れるため、
    /// 既定では「範囲を絞る」ことで安全性を確保し、include 自体は認める。
    /// </remarks>
    public static FileAccessPolicy Default { get; } = new();

    /// <summary>
    /// アクセスを許可するルートを取得する。
    /// </summary>
    /// <remarks>
    /// 空の場合はサイト ルートのみが許可される。
    /// 追加のルート（共有インクルード ディレクトリ等）はここに列挙する。
    /// </remarks>
    public ImmutableArray<ContentPath> AllowedRoots { get; init; } = [];

    /// <summary>
    /// シンボリック リンクの追跡を許可するかどうかを示す値を取得する。
    /// </summary>
    /// <remarks>
    /// 既定は <see langword="false"/>。パスの文字列検査だけでは
    /// <see cref="AllowedRoots"/> の外へ出るリンクを検出できないため。
    /// </remarks>
    public bool AllowSymbolicLinks { get; init; }

    /// <summary>
    /// 外部ファイルの取り込みを許可するかどうかを示す値を取得する。
    /// </summary>
    /// <remarks>
    /// AsciiDoc の <c>include::</c>、テンプレート エンジンの partial 解決の双方に適用される。
    /// </remarks>
    public bool AllowIncludes { get; init; } = true;
}
