namespace Lithify.Abstractions;

/// <summary>
/// メタデータの項目がどこから来たかの分類。
/// </summary>
/// <remarks>
/// <para>
/// 同じキーの値が「利用者が書いたもの」なのか「パーサーが導出したもの」なのかは、
/// 診断の文面（どこを直せばよいか）と、値の上書きが妥当かどうかの判断に必要である。
/// たとえば <see cref="WellKnownMetadata.Title"/> が <see cref="Declared"/> 由来なら
/// 利用者が明示的に書いた題名だが、<see cref="Derived"/> なら本文の最初の見出しから
/// パーサーが拾ったものであり、後者を既定値で上書きするのは妥当でありうる。
/// </para>
/// </remarks>
public enum MetadataOrigin
{
    /// <summary>
    /// 出所が記録されていない。
    /// </summary>
    /// <remarks>
    /// <see langword="default"/> がこれになるのは意図的である。出所の記録は任意であり、
    /// 記録しないパーサーの項目は「不明」として扱われる。
    /// </remarks>
    Unknown,

    /// <summary>
    /// ソース ファイルの内容に、その項目として明示的に書かれていた。
    /// </summary>
    /// <remarks>
    /// <para>
    /// YAML フロント マターの項目、AsciiDoc の document attributes がこれに当たる。
    /// 利用者が<em>そのキーの値として</em>直接書いた文字列なので、値の誤りは書かれた場所を直せば済む。
    /// </para>
    /// <para>
    /// 同じく内容に由来するが、メタデータとして書かれたものではなく本文から取ってきたものは
    /// <see cref="Derived"/> である。判定は「利用者がその値をそのメタデータ項目として書いたか」で、
    /// 内容中に値が現れるかどうかではない。
    /// </para>
    /// </remarks>
    Declared,

    /// <summary>
    /// ファイル システムが持つ属性に由来する。
    /// </summary>
    /// <remarks>
    /// 最終更新日時をファイルの mtime から埋める場合など。内容には現れないので、
    /// 位置情報は持たない。同じ内容のファイルでも環境によって変わりうる点に注意。
    /// ファイルの<em>パス</em>に由来するものは <see cref="Path"/> である。
    /// </remarks>
    FileSystem,

    /// <summary>
    /// ソース ファイルのパスに由来する。
    /// </summary>
    /// <remarks>
    /// ファイル名から導出した <see cref="WellKnownMetadata.Slug"/> や、
    /// <c>posts/2026-01-01-hello.md</c> のような命名規則から取り出した日付がこれに当たる。
    /// </remarks>
    Path,

    /// <summary>
    /// メタデータ宣言ではない内容から、パーサーの解釈によって導出された。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本文のレベル 0 見出し（<c>= Hello</c>）から取った AsciiDoc の <c>doctitle</c> や、
    /// 本文の冒頭から切り出した <see cref="WellKnownMetadata.Description"/> がこれに当たる。
    /// 由来する箇所が特定できる場合は <see cref="MetadataProvenance.Location"/> に入れる。
    /// </para>
    /// <para>
    /// <see cref="Declared"/> と分ける理由は診断の宛先である。<see cref="Declared"/> なら
    /// 「その行を直せ」で済むが、導出は値そのものが妥当でも<em>解釈</em>が意図と違いうるので、
    /// 直し方が「導出元を直す」と「メタデータに明示的に書いて上書きする」の 2 通りになる。
    /// 潰すと後者を提案できない。
    /// </para>
    /// <para>
    /// 同じキーが両方から来ることに注意。<c>:doctitle: Hello</c> と明示的に書けば
    /// <see cref="Declared"/> であり、見出しから拾えば <see cref="Derived"/> である。
    /// </para>
    /// </remarks>
    Derived,

    /// <summary>
    /// 別のキーの値を写したもの。
    /// </summary>
    /// <remarks>
    /// <para>
    /// パーサーが自形式のネイティブな名前を <see cref="WellKnownMetadata"/> のキーに写したもの
    /// （AsciiDoc の <c>revdate</c> → <c>date</c>）。写し元のキーは
    /// <see cref="MetadataProvenance.SourceKey"/> に入る。
    /// </para>
    /// <para>
    /// <see cref="Derived"/> との違いは、写し元がメタデータ宣言であり同じ
    /// <see cref="DocumentMetadata.Entries"/> の中に項目として残っていることである。
    /// したがって「<c>revdate</c> を直せ」とキー名で言える。
    /// 導出元が本文であれば <see cref="Derived"/> を使う。
    /// </para>
    /// </remarks>
    Mapped,

    /// <summary>
    /// 既定値に由来する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DocumentMetadata.WithFallback"/> で下敷きから採られた項目。
    /// 文書のどこにも書かれていないので、この値に起因する誤りは文書ではなく設定を直す必要がある。
    /// </para>
    /// <para>
    /// 既定値は<em>層</em>をなす（サイト全体 → <c>posts/</c> → <c>posts/2026/</c> → 文書）。
    /// どの層が定義したかは <see cref="MetadataProvenance.SourcePath"/> に入る。
    /// サイト全体の既定値はルート層なので <see langword="default"/> になり、
    /// 専用の分類は設けない。層を区別しないと「既定値が効いている」ことは分かっても
    /// どこを直せばよいかが分からず、階層のある既定値では出所として役に立たない。
    /// </para>
    /// </remarks>
    Defaults,
}

/// <summary>
/// メタデータの項目の出所。
/// </summary>
/// <param name="Origin">出所の分類。</param>
/// <param name="SourceKey">
/// 写し元のキー。<see cref="MetadataOrigin.Mapped"/> の場合に、写す前のネイティブな名前
/// （AsciiDoc の <c>revdate</c>）が入る。それ以外では <see langword="default"/>。
/// </param>
/// <param name="SourcePath">
/// この値を定義した場所。<see cref="MetadataOrigin.Defaults"/> の場合に、
/// その既定値を定義した層（<c>posts/</c> 等のディレクトリ、または既定値ファイル）が入る。
/// サイト全体の既定値と、それ以外の出所では <see langword="default"/>。
/// </param>
/// <param name="Location">
/// 内容中の位置。<see cref="MetadataOrigin.Declared"/> と <see cref="MetadataOrigin.Derived"/> の場合に、
/// パーサーが位置を提供できるなら入る。提供しない場合と、内容に由来しない出所では <see langword="default"/>。
/// </param>
/// <remarks>
/// <para>
/// <see cref="DocumentMetadata.Entries"/> と対にした<em>疎な</em>副表として保持する
/// （<see cref="DocumentMetadata.Origins"/>）。値そのものに持たせないのは 2 つの理由による。
/// 1 つは <see cref="MetadataValue"/> が入れ子（<see cref="MetadataValue.Sequence"/> /
/// <see cref="MetadataValue.Mapping"/>）を持ち、要素ごとに出所を付けると出所の合成規則が必要になること。
/// もう 1 つは出所が値の同一性に含まれてしまい、「同じ値だが出所が違う」だけで
/// フィンガープリントが変わって再ビルドが走ることである。
/// </para>
/// <para>
/// 位置情報は <see cref="SourceLocation"/> をそのまま使う。行と桁の 2 つで、
/// 提供しないパーサーは <see langword="default"/> を置けばよいので、
/// 位置を持たせることの費用はほぼ無い。範囲（開始と終了）にはしない。
/// 用途は診断の提示先であり、始点があれば足りる。
/// </para>
/// <para>
/// <see cref="SourcePath"/> と <see cref="Location"/> を合わせると、既定値であっても
/// 「<c>posts/defaults.yml</c> の 3 行目」まで指せる。<see cref="Diagnostic"/> が
/// <see cref="ContentPath"/> と <see cref="SourceLocation"/> の対を取るのと同じ形なので、
/// 出所をそのまま診断に渡せる。
/// </para>
/// </remarks>
public readonly record struct MetadataProvenance(
    MetadataOrigin Origin,
    MetadataKey SourceKey,
    ContentPath SourcePath,
    SourceLocation Location)
{
    /// <summary>
    /// 出所が記録されていないことを表す <see cref="MetadataProvenance"/>。
    /// </summary>
    public static MetadataProvenance Unknown =>
        default;

    /// <summary>
    /// ファイル システムが持つ属性に由来することを表す <see cref="MetadataProvenance"/>。
    /// </summary>
    public static MetadataProvenance FromFileSystem { get; } =
        new(MetadataOrigin.FileSystem, default, default, default);

    /// <summary>
    /// ソース ファイルのパスに由来することを表す <see cref="MetadataProvenance"/>。
    /// </summary>
    public static MetadataProvenance FromPath { get; } =
        new(MetadataOrigin.Path, default, default, default);

    /// <summary>
    /// サイト全体の既定値に由来することを表す <see cref="MetadataProvenance"/>。
    /// </summary>
    /// <remarks>
    /// ルート層の既定値である。<see cref="FromDefaults"/> に <see langword="default"/> の
    /// <see cref="ContentPath"/> を渡したものと等しい。サイト全体の既定値を
    /// 別の <see cref="MetadataOrigin"/> にしないのは、それが「層のないもの」ではなく
    /// 単に<em>最も外側の層</em>であり、区別すると層を辿るコードが根だけ特別扱いを要するためである。
    /// </remarks>
    public static MetadataProvenance FromSiteDefaults { get; } =
        new(MetadataOrigin.Defaults, default, default, default);

    /// <summary>
    /// ソース ファイルの内容に明示的に書かれていたことを表す <see cref="MetadataProvenance"/> を生成する。
    /// </summary>
    /// <param name="location">内容中の位置。不明な場合は <see langword="default"/>。</param>
    /// <returns>生成された出所。</returns>
    /// <remarks>
    /// 位置はその文書自身の内容中の位置なので、<see cref="SourcePath"/> は入らない
    /// （どの文書かは <see cref="DocumentMetadata"/> を持つ文書自身が知っている）。
    /// </remarks>
    public static MetadataProvenance Declared(
        SourceLocation location = default)
    {
        return new MetadataProvenance(MetadataOrigin.Declared, default, default, location);
    }

    /// <summary>
    /// 内容の他の箇所から導出されたことを表す <see cref="MetadataProvenance"/> を生成する。
    /// </summary>
    /// <param name="location">導出元の位置。不明な場合は <see langword="default"/>。</param>
    /// <returns>生成された出所。</returns>
    public static MetadataProvenance Derived(
        SourceLocation location = default)
    {
        return new MetadataProvenance(MetadataOrigin.Derived, default, default, location);
    }

    /// <summary>
    /// 別のキーの値を写したものであることを表す <see cref="MetadataProvenance"/> を生成する。
    /// </summary>
    /// <param name="sourceKey">写し元のキー。</param>
    /// <param name="location">写し元の位置。不明な場合は <see langword="default"/>。</param>
    /// <returns>生成された出所。</returns>
    /// <remarks>
    /// 写し元の項目も <see cref="DocumentMetadata.Entries"/> に残るので、
    /// <paramref name="sourceKey"/> は同じ辞書の別の項目を指す。
    /// </remarks>
    public static MetadataProvenance Mapped(
        MetadataKey sourceKey,
        SourceLocation location = default)
    {
        return new MetadataProvenance(MetadataOrigin.Mapped, sourceKey, default, location);
    }

    /// <summary>
    /// 既定値に由来することを表す <see cref="MetadataProvenance"/> を生成する。
    /// </summary>
    /// <param name="definedIn">
    /// その既定値を定義した層。ディレクトリごとの既定値ならそのディレクトリまたは既定値ファイルのパス。
    /// サイト全体の既定値なら <see langword="default"/>。
    /// </param>
    /// <param name="location">
    /// 既定値ファイル中の位置。<paramref name="definedIn"/> がファイルを指し、
    /// かつ位置を提供できる場合に渡す。
    /// </param>
    /// <returns>生成された出所。</returns>
    /// <remarks>
    /// 層の<em>深さ</em>ではなくパスを持つ。深さは既定値の合成順を決める側の関心であって、
    /// 「どこを直せばよいか」には答えられない。パスがあれば深さは導ける。
    /// </remarks>
    public static MetadataProvenance FromDefaults(
        ContentPath definedIn,
        SourceLocation location = default)
    {
        return new MetadataProvenance(MetadataOrigin.Defaults, default, definedIn, location);
    }

    /// <summary>
    /// この出所が記録されていない（<see langword="default"/> である）かどうかを示す値を取得する。
    /// </summary>
    public bool IsUnknown =>
        this.Origin == MetadataOrigin.Unknown;

    /// <summary>
    /// この項目がソース ファイルの内容に現れているかどうかを示す値を取得する。
    /// </summary>
    /// <remarks>
    /// 診断を文書のどこに向けるべきかの判断に使う。真であれば
    /// <see cref="Location"/> が意味を持ちうる（それでも不明でありうる）。
    /// </remarks>
    public bool IsFromContent =>
        this.Origin is MetadataOrigin.Declared or MetadataOrigin.Derived or MetadataOrigin.Mapped;
}
