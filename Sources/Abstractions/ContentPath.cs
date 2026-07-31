using System;

using JetBrains.Annotations;

namespace Lithify.Abstractions;

/// <summary>
/// コンテンツの位置が属するアドレス空間。
/// </summary>
/// <remarks>
/// <para>
/// <strong>これは「どこから読むか」（取得手段）ではなく「何を指しているか」（アドレス空間）である。</strong>
/// 最も誤解されやすい点なので明示する。取得手段は <see cref="IContentSourceProvider"/> の関心であり、
/// たとえばテストでローカル パスの内容をメモリから供給する実装は
/// <see cref="ContentPathKind.Local"/> のままである（アドレスとしてはサイト ルート相対のパスを指しているため）。
/// </para>
/// </remarks>
public enum ContentPathKind
{
    /// <summary>
    /// サイト ルートからの相対パス。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> がこれになるのは意図的である。既存のコードはすべて
    /// ローカル パスを渡す意図で書かれており、既定がローカルであることに依存している。
    /// </remarks>
    Local,

    /// <summary>
    /// 絶対 URI で表される、サイトの外にある取得対象。
    /// </summary>
    /// <remarks>
    /// 本文中のハイパーリンク（<see cref="Ast.LinkTarget.External"/>）とは別物である。
    /// こちらは Lithify が<em>取得してビルド入力にする</em>内容を指す。
    /// </remarks>
    Remote,

    /// <summary>
    /// アドレスを持たない、実行時に合成された内容。
    /// </summary>
    /// <remarks>
    /// 文字列として直接与えられた内容や、プラグインが組み立てた内容。
    /// ファイル システムにもネットワークにも対応する場所がないので、
    /// 名前空間（authority）と名前で識別する。
    /// </remarks>
    InMemory,
}

/// <summary>
/// ビルドの入力となるコンテンツの位置。
/// </summary>
/// <remarks>
/// <para>
/// ローカル ファイル・リモート URI・実行時に合成された内容のいずれも表す
/// （<see cref="Kind"/> で区別する）。計算ノードの鍵であり、ビルドを跨いで永続化される。
/// </para>
/// <para>
/// <strong>プロバイダの識別子を含めてはならない。</strong> <see cref="IContentSourceProvider.CanOpen"/> は
/// パスを見てプロバイダを選ぶ。つまりプロバイダはパスの関数である。パスがプロバイダを含むと
/// パスを作るのにプロバイダが必要になり、循環する。パーサーが相対リンクから
/// <see cref="ContentPath"/> を作る時点では、誰が供給するかは決まっていない。
/// またプロバイダを差し替えるだけで同一性が変わってしまい、
/// 「同じ入力なら同じフィンガープリント」の契約が構造的に成立しなくなる。
/// </para>
/// <para>
/// 大文字小文字は区別する。ファイル システムが区別しない環境でも出力される URL は区別するため、
/// 区別しない扱いにすると環境によって出力が変わってしまう。
/// </para>
/// <para>
/// 順序は <see cref="Kind"/> → テキスト表現のオーディナル比較で全順序になる。
/// 3 種が混在する集合の列挙順が安定しないと、そこから作るフィンガープリントが不安定になる。
/// </para>
/// </remarks>
public readonly record struct ContentPath :
    IComparable<ContentPath>
{
    private readonly string? _value;

    private readonly ContentPathKind _kind;

    /// <summary>
    /// 指定したサイト ルート相対のパスから、ローカルの <see cref="ContentPath"/> を生成する。
    /// </summary>
    /// <param name="value">サイト ルートからの相対パス。<c>/</c> と <c>\</c> のどちらの区切りでもよい。</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> が空である、絶対パスである、またはサイト ルートより上に遡っている。
    /// </exception>
    /// <remarks>
    /// <strong>このコンストラクターは常にローカルを意味する。</strong>
    /// <c>https://</c> で始まる文字列を渡してもリモートにはならず、その名前のディレクトリとして扱われる
    /// （<c>:</c> を含むので実際には正規化を通っても意味を持たないパスになる）。
    /// 文字列を見て種別を推測しないのは、既存の呼び出し側がすべてローカル パスを渡す意図で
    /// 書かれているためである。暗黙に再分類すると、Markdown の <c>[x](https://…)</c> のような
    /// 本文中の外部リンクが取得対象のコンテンツに化ける。
    /// リモートは <see cref="Remote"/> で明示的に作り、構成ファイルから来る文字列だけ
    /// <see cref="TryParse"/> で受ける。
    /// </remarks>
    public ContentPath(
        string value)
    {
        this._value = PathNormalizer.Normalize(value, PathKind.Content, nameof(value));
        this._kind = ContentPathKind.Local;
    }

    private ContentPath(
        string value,
        ContentPathKind kind)
    {
        this._value = value;
        this._kind = kind;
    }

    /// <summary>
    /// このパスが属するアドレス空間を取得する。
    /// </summary>
    public ContentPathKind Kind =>
        this._kind;

    /// <summary>
    /// このパスがローカルかどうかを示す値を取得する。
    /// </summary>
    /// <remarks>
    /// <see cref="OutputPath"/> への写像とファイル システムへの解決は、
    /// これを確認してから行わなければならない。
    /// </remarks>
    public bool IsLocal =>
        this._kind == ContentPathKind.Local;

    /// <summary>
    /// 正規化されたテキスト表現を取得する。
    /// </summary>
    /// <remarks>
    /// <see cref="ContentPathKind.Local"/> ならサイト ルート相対のパス、
    /// <see cref="ContentPathKind.Remote"/> なら絶対 URI、
    /// <see cref="ContentPathKind.InMemory"/> なら <c>authority/name</c> の形になる。
    /// <see langword="default"/> の <see cref="ContentPath"/> では空文字列になる。
    /// </remarks>
    public string Value =>
        this._value ?? string.Empty;

    /// <summary>
    /// このパスが値を持たない（<see langword="default"/> である）かどうかを示す値を取得する。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> は空の <see cref="ContentPathKind.Local"/> である。
    /// </remarks>
    public bool IsEmpty =>
        string.IsNullOrEmpty(this._value);

    /// <summary>
    /// 絶対 URI からリモートの <see cref="ContentPath"/> を生成する。
    /// </summary>
    /// <param name="uri">取得対象の絶対 URI。</param>
    /// <returns>生成されたパス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="uri"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException"><paramref name="uri"/> が絶対 URI でない。</exception>
    /// <remarks>
    /// スキームは検査しない。どのスキームを扱えるかを知っているのはプロバイダであり、
    /// この型が知る根拠はない（<c>git+ssh</c> のようなスキームを後から足せる必要がある）。
    /// 扱えないスキームは「開けるプロバイダが無い」という形で露見する。
    /// </remarks>
    [Pure]
    public static ContentPath Remote(
        Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException(
                Messages.FormatRemoteContentPathMustBeAbsolute(uri.OriginalString),
                nameof(uri));
        }

        return new ContentPath(uri.AbsoluteUri, ContentPathKind.Remote);
    }

    /// <summary>
    /// 名前空間と名前からインメモリの <see cref="ContentPath"/> を生成する。
    /// </summary>
    /// <param name="authority">
    /// 名前空間を所有する主体。パッケージ名や機能名（<c>lithify.blog</c>）を用いる。
    /// </param>
    /// <param name="name">その名前空間の中での名前（<c>sidebar-tags</c>）。</param>
    /// <returns>生成されたパス。</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="authority"/> または <paramref name="name"/> が <see langword="null"/> である。
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="authority"/> が空かパス区切り文字を含む、または <paramref name="name"/> が空である。
    /// </exception>
    /// <remarks>
    /// <para>
    /// <paramref name="authority"/> を必須にしているのは、この種別だけが構造的に一意でないためである。
    /// ローカルはサイト ルートが単一なので一意、リモートは絶対 URI なので一意だが、
    /// インメモリの名前には命名権威が無く、2 つのプラグインが同じ名前を作りうる。
    /// </para>
    /// <para>
    /// 所有者はパッケージや機能であって実行時のプロバイダ インスタンスではない。
    /// したがってこれは <see cref="SourceValidator.ProviderId"/> とは別物であり、
    /// プロバイダを差し替えても変わらない。
    /// </para>
    /// <para>
    /// <paramref name="name"/> はパスと同じ規則で正規化する。名前に階層を持たせたい実装
    /// （<c>layouts/post</c> 等）があり、正規形が一意でないと同じ名前が別のノードになるためである。
    /// </para>
    /// </remarks>
    [Pure]
    public static ContentPath InMemory(
        string authority,
        string name)
    {
        ArgumentNullException.ThrowIfNull(authority);

        if (authority.Length == 0)
        {
            throw new ArgumentException(Messages.InMemoryAuthorityMustNotBeEmpty, nameof(authority));
        }

        if (authority.AsSpan().IndexOfAny('/', '\\') >= 0)
        {
            throw new ArgumentException(
                Messages.FormatInMemoryAuthorityMustNotContainSeparator(authority),
                nameof(authority));
        }

        var normalized = PathNormalizer.Normalize(name, PathKind.Content, nameof(name));

        return new ContentPath(
            string.Concat(authority, "/", normalized),
            ContentPathKind.InMemory);
    }

    /// <summary>
    /// 文字列から <see cref="ContentPath"/> を解析する。
    /// </summary>
    /// <param name="value">解析する文字列。</param>
    /// <param name="path">解析されたパス。失敗した場合は <see langword="default"/>。</param>
    /// <returns>解析できた場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <para>
    /// <strong>構成ファイルやコマンドラインから来た文字列にのみ用いる。</strong>
    /// スキーム付きの絶対 URI をリモートとして、それ以外をローカルとして解釈する。
    /// コンストラクターと違って種別を推測するので、コード中でパスを組み立てる用途には使わない
    /// （そちらは意図が決まっているのだから、コンストラクターか <see cref="Remote"/> を直に呼ぶ）。
    /// </para>
    /// <para>
    /// インメモリは解析しない。authority と名前の境界を文字列から復元する規則を決めると、
    /// <c>/</c> を含む名前が曖昧になる。実行時に合成された内容を構成ファイルに書く用途もない。
    /// </para>
    /// </remarks>
    [Pure]
    public static bool TryParse(
        string? value,
        out ContentPath path)
    {
        path = default;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !IsFileSystemUri(uri))
        {
            path = new ContentPath(uri.AbsoluteUri, ContentPathKind.Remote);
            return true;
        }

        try
        {
            path = new ContentPath(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 絶対 URI として解析されたものが、実際はファイル システムのパスかどうかを判定する。
    /// </summary>
    /// <param name="uri">判定する URI。</param>
    /// <returns>ファイル システムのパスの場合は <see langword="true"/>。</returns>
    /// <remarks>
    /// <c>C:\posts</c> や <c>\\server\share</c> は <see cref="Uri"/> には <c>file:</c> スキームの
    /// 絶対 URI として解析される。これをリモートとして受けるとローカルの絶対パスがリモート扱いになるので、
    /// ローカルの経路に流して<em>絶対パスとして拒否させる</em>。
    /// <c>file:</c> をリモートのスキームとして扱わないのは、それを許すと
    /// 「サイト ルートの外に出られない」というローカルの保証を素通りする経路になるためである。
    /// </remarks>
    [Pure]
    private static bool IsFileSystemUri(
        Uri uri)
    {
        return uri.IsFile || uri.IsUnc;
    }

    /// <summary>
    /// 拡張子を取得する（先頭の <c>.</c> を含む）。拡張子がない場合は空。
    /// </summary>
    /// <remarks>
    /// 形式のディスパッチ（<see cref="IContentFormatRegistry.TryGetFormat"/>）が拡張子を鍵にしているため、
    /// どの種別でも取れなければならない。<see cref="ContentPathKind.Remote"/> では
    /// URI パスの最終セグメントから取る（クエリとフラグメントは含めない）。
    /// リモートは拡張子を持たないことがあるので、媒体型から形式を得る経路も別に要る。
    /// </remarks>
    public ReadOnlySpan<char> Extension =>
        PathNormalizer.GetExtension(this.LastSegmentSource);

    /// <summary>
    /// ファイル名を取得する。
    /// </summary>
    /// <remarks>
    /// <see cref="ContentPathKind.Remote"/> では URI パスの最終セグメント、
    /// <see cref="ContentPathKind.InMemory"/> では名前の最終セグメントを返す。
    /// </remarks>
    public ReadOnlySpan<char> FileName =>
        PathNormalizer.GetFileName(this.LastSegmentSource);

    /// <summary>
    /// 拡張子を除いたファイル名を取得する。
    /// </summary>
    public ReadOnlySpan<char> FileNameWithoutExtension =>
        PathNormalizer.GetFileNameWithoutExtension(this.LastSegmentSource);

    /// <summary>
    /// ファイル名と拡張子を取り出す元になる文字列を取得する。
    /// </summary>
    /// <remarks>
    /// <see cref="ContentPathKind.Remote"/> の場合、<see cref="Value"/> は
    /// クエリとフラグメントを含む絶対 URI なので、そのまま最終セグメントを取ると
    /// <c>?v=2</c> が拡張子に混ざる。パス部分だけを切り出す。
    /// </remarks>
    private string LastSegmentSource
    {
        get
        {
            if (this._kind != ContentPathKind.Remote)
            {
                return this.Value;
            }

            return Uri.TryCreate(this.Value, UriKind.Absolute, out var uri)
                ? uri.AbsolutePath
                : this.Value;
        }
    }

    /// <summary>
    /// 親ディレクトリのパスを取得する。親がない場合は <see langword="default"/>。
    /// </summary>
    /// <exception cref="InvalidOperationException">このパスがローカルでない。</exception>
    /// <remarks>
    /// <strong>ローカル専用である。</strong> リモートの「親」は URI の相対参照解決の規則に従い、
    /// 基準が末尾の <c>/</c> の有無で変わるので、パス セグメントを削るのとは違う結果になる。
    /// リモートの解決は <see cref="IContentSourceProvider.TryResolveReference"/> が担う。
    /// </remarks>
    public ContentPath Directory
    {
        get
        {
            this.EnsureLocal();

            return PathNormalizer.GetDirectory(this.Value) is { } directory
                ? new ContentPath(directory)
                : default;
        }
    }

    /// <summary>
    /// このパスの下に相対パスを連結する。
    /// </summary>
    /// <param name="relative">
    /// 連結する相対パス。<c>..</c> を含んでもよいが、結果がサイト ルートの外に出てはならない。
    /// </param>
    /// <returns>連結されたパス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="relative"/> が <see langword="null"/> である。</exception>
    /// <exception cref="ArgumentException">連結結果が空になる、またはサイト ルートの外に出る。</exception>
    /// <exception cref="InvalidOperationException">このパスがローカルでない。</exception>
    /// <remarks>
    /// <strong>これは相対参照解決の唯一の手段ではない。</strong> パス セグメントの結合であり、
    /// ローカルのアドレス空間でしか正しくない。リモートは RFC 3986 の相対参照解決、
    /// git はリビジョンの引き継ぎを要するので、規則を知っているプロバイダが
    /// <c>TryResolveReference</c> で担う。この操作は
    /// <c>FileSystemContentSourceProvider</c> の実装詳細に降りる。
    /// </remarks>
    [Pure]
    public ContentPath Combine(
        string relative)
    {
        ArgumentNullException.ThrowIfNull(relative);

        this.EnsureLocal();

        return this.IsEmpty
            ? new ContentPath(relative)
            : new ContentPath(string.Concat(this.Value, "/", relative));
    }

    /// <summary>
    /// 拡張子を差し替えたパスを返す。
    /// </summary>
    /// <param name="extension">新しい拡張子。先頭の <c>.</c> は省略できる。空文字列なら拡張子を取り除く。</param>
    /// <returns>拡張子が差し替えられたパス。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="extension"/> が <see langword="null"/> である。</exception>
    /// <exception cref="InvalidOperationException">このパスがローカルでない。</exception>
    /// <remarks>
    /// <strong>ローカル専用である。</strong> 用途は出力パスの導出（<c>.md</c> → <c>.html</c>）であり、
    /// 取得元のアドレスを書き換える用途は存在しない。リモート URI の拡張子を差し替えると
    /// 別の資源を指すことになる。
    /// </remarks>
    [Pure]
    public ContentPath WithExtension(
        string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);

        this.EnsureLocal();

        return new ContentPath(PathNormalizer.ReplaceExtension(this.Value, extension));
    }

    /// <summary>
    /// このパスがローカルであることを確認する。
    /// </summary>
    /// <exception cref="InvalidOperationException">このパスがローカルでない。</exception>
    /// <remarks>
    /// ローカル専用の操作をリモートやインメモリのパスに適用するのは、呼び出し側が
    /// <see cref="IsLocal"/> の確認を怠ったということなのでプログラムの誤りである。
    /// コンテンツの誤りではないので <see cref="Diagnostic"/> ではなく例外にする。
    /// </remarks>
    private void EnsureLocal()
    {
        if (this._kind != ContentPathKind.Local)
        {
            throw new InvalidOperationException(
                Messages.FormatContentPathMustBeLocal(this.Value, this._kind));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="Kind"/> を第 1 の鍵にしている。3 種が混在する集合でも
    /// 列挙順が決定的になり、そこから作るフィンガープリントが安定する。
    /// </remarks>
    public int CompareTo(
        ContentPath other)
    {
        var kind = ((int)this._kind).CompareTo((int)other._kind);

        return kind != 0
            ? kind
            : string.CompareOrdinal(this.Value, other.Value);
    }

    /// <inheritdoc />
    public bool Equals(
        ContentPath other)
    {
        return this._kind == other._kind &&
            string.Equals(this.Value, other.Value, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(this._kind, StringComparer.Ordinal.GetHashCode(this.Value));
    }

    /// <summary>
    /// 正規化されたテキスト表現を返す。
    /// </summary>
    /// <returns>正規化されたテキスト表現。</returns>
    /// <remarks>
    /// 種別は含めない。診断メッセージに出るのはこの値であり、リモートなら絶対 URI が、
    /// ローカルならサイト ルート相対のパスがそのまま読める形になる。
    /// </remarks>
    public override string ToString()
    {
        return this.Value;
    }

    /// <summary>
    /// 一方のパスが他方より前に並ぶかどうかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> より前に並ぶ場合は <see langword="true"/>。</returns>
    public static bool operator <(
        ContentPath left,
        ContentPath right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// 一方のパスが他方より後に並ぶかどうかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> より後に並ぶ場合は <see langword="true"/>。</returns>
    public static bool operator >(
        ContentPath left,
        ContentPath right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// 一方のパスが他方より前に並ぶか等しいかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> 以下の場合は <see langword="true"/>。</returns>
    public static bool operator <=(
        ContentPath left,
        ContentPath right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// 一方のパスが他方より後に並ぶか等しいかを判定する。
    /// </summary>
    /// <param name="left">左辺。</param>
    /// <param name="right">右辺。</param>
    /// <returns><paramref name="left"/> が <paramref name="right"/> 以上の場合は <see langword="true"/>。</returns>
    public static bool operator >=(
        ContentPath left,
        ContentPath right)
    {
        return left.CompareTo(right) >= 0;
    }
}
