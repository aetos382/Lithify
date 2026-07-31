namespace Lithify.Abstractions;

/// <summary>
/// アドレスが一意な内容を指すかどうかの分類。
/// </summary>
/// <remarks>
/// <para>
/// <strong>軸は「ローカルかリモートか」ではなく「アドレスが一意な内容を指すか」である。</strong>
/// リモートだから直ちに再現不可能ということはなく（commit SHA 付きの git 参照は一意）、
/// ローカルだから安全ということもない（サイト ルートの外を指すシンボリック リンクは
/// リンク自体は commit されるが実体は入らないので、clone では壊れたリンクになる）。
/// </para>
/// <para>
/// <strong>分類の主体は <c>IContentSourceProvider</c> である。</strong>
/// アドレスが一意かどうかを判定できるのはそのスキームを理解しているプロバイダだけなので、
/// 取得結果に添えて返す。中核はそれを集約するだけで、スキームごとの規則を知る必要がない。
/// </para>
/// <para>
/// <strong>判定は取得ごとに行う。</strong> 同じプロバイダでも
/// <c>https://x/v1.0.0/y</c>（不変を宣言した応答）と <c>https://x/latest/y</c> は別の分類になる。
/// </para>
/// <para>
/// これは <see cref="Fingerprint"/> とも <c>SourceValidator</c> とも別の概念である。
/// <see cref="Fingerprint"/> は「取れた内容が何であったか」、<c>SourceValidator</c> は
/// 「取り直す必要があるか」、これは「<em>次に取ったときも同じ内容が返ると期待できるか</em>」である。
/// </para>
/// </remarks>
public enum SourceStability
{
    /// <summary>
    /// 取得のたびに同じ内容が返ることが、アドレスによって決まっている。
    /// </summary>
    /// <remarks>
    /// <para>
    /// git の commit SHA、内容ハッシュを含む URL、不変を宣言した応答。
    /// ブランチ名や tag は <see cref="Unpinned"/> である（tag は動かせる）。
    /// </para>
    /// <para>
    /// <strong>発行側の自己申告には限界がある。</strong> <c>Cache-Control: immutable</c> は
    /// 発行側が付ける宣言にすぎず、乗っ取られたホストは同じ宣言を付けられる。
    /// アドレス自身が内容を決めているのは commit SHA と内容ハッシュ入り URL だけである。
    /// この差を型で表現するかは未決なので、当面はプロバイダの判断に委ねる。
    /// </para>
    /// </remarks>
    Pinned,

    /// <summary>
    /// 同じアドレスが別の内容を返しうる。
    /// </summary>
    /// <remarks>
    /// 素の HTTP URL、ブランチや tag の指定、サイト ルートの外を指すシンボリック リンク経由の参照。
    /// 禁止はせず、<see cref="ReproducibilityMode"/> に扱いを委ねる。
    /// </remarks>
    Unpinned,
}

/// <summary>
/// <see cref="SourceStability.Unpinned"/> な参照を見つけたときの扱い。
/// </summary>
/// <remarks>
/// 内容が文章であることを踏まえると、常に厳格である必要はない。既定は緩くし、
/// 要求する選択肢を用意する。
/// </remarks>
public enum ReproducibilityMode
{
    /// <summary>
    /// 何もしない。
    /// </summary>
    /// <remarks>
    /// 下書きや実験のためのビルド。この値のときは、分類のためだけに必要な作業
    /// （ルート外リンクの実体解決）を省略してよい。結果を使わないためである。
    /// </remarks>
    Ignore,

    /// <summary>
    /// 警告の診断を出して続行する。
    /// </summary>
    /// <remarks>
    /// 既定値。通常の執筆で <see cref="SourceStability.Unpinned"/> な参照を
    /// 止める理由はないが、気付けないのも困る。
    /// </remarks>
    Warn,

    /// <summary>
    /// エラーの診断を出す。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 公開ビルド、CI、アーカイブ。<c>--require-reproducible</c> で指定する。
    /// </para>
    /// <para>
    /// <strong>ビルド全体を即座に止めるのではなく、参照ごとに診断を出す。</strong>
    /// <see cref="Diagnostic"/> は参照元のパスと位置を持てるので、
    /// どの参照を直せばよいかが示せる。どれか 1 つの失敗で全体が止まると原因が分からない。
    /// </para>
    /// <para>
    /// <strong>オフラインでの取得（通信しない設定）では代用できない。</strong>
    /// オフラインでもストアの内容は取得時点のものであり、
    /// それが一意なアドレス由来かどうかは別の問題である。
    /// </para>
    /// </remarks>
    Require,
}
