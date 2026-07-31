# 実装ロードマップ

骨格段階（抽象定義とインフラのみ）の残作業を記録する。実装本体は原則 `NotImplementedException` で、
純粋関数（`Fingerprint` / `OutputDecision.Decide` / `MetadataKey` の正規化）と記録機構だけを実装する方針は
全ステップを通じて変わらない。

各ステップの「設計上の制約」は、**骨格段階で契約に織り込まないと後から直せない**ものだけを挙げている。
後から足せるものは意図的に書いていない。

## 完了済み

| # | 内容 |
|---|---|
| 1 | リポジトリ インフラ（`global.json` / `Directory.Build.props` / `.targets` / `Directory.Packages.props` / `NuGet.config` / `.editorconfig` / `.globalconfig` / `LICENSE` / `renovate.json` / `README.md`） |
| 2 | `.claude/`（`CLAUDE.md` + `rules/dotnet.md` + `rules/claude-md.md`）、`.git-hooks/` と `.gitconfig` による設定ベース フック、devcontainer |
| 3 | `.github/workflows/`（`test.yml` / `release.yml` / `codeql.yml` / `component-detection.yml`） |
| 4 | 23 プロジェクトの作成と `Lithify.slnx` への登録 |
| 5 | `Lithify.Abstractions` — 共通 AST、`DocumentMetadata`、パーサー／レンダラー／テンプレート／ハイライターの契約（ステップ 9 で `ContentPath` を一般化し、`FileAccessPolicy` を削除済み） |
| 6 | `Lithify.Core` — 増分計算グラフ、フラグメント合成、`IOutputStore`、`IBuildCache`、`Utf8BufferTextWriter` |
| 7 | 形式抽象層 — `Lithify.Markdown.Abstractions` / `Lithify.AsciiDoc.Abstractions` |
| 8 | `Lithify.Hosting` — `UseLithify()` / `ILithifyBuilder` / `RunLithifyAsync()` / `build` / `clean` |

現時点で `Sources/Blog`、`Sources/Renderers/Html`、`Sources/Templates/*`、`Sources/Highlighting/TextMate`、
`Sources/Parsers/*`、`Sources/Serve`、`Sources/Testing` は **csproj のみで `.cs` ファイルがゼロ**。
参照関係とパッケージ メタデータだけが確定している状態。

## 9. `ContentPath` の一般化とソース プロバイダ

**ステップ 10 より前に行う。** 完了済みのステップ 5 に対する改版であり、後から入れると
パーサー・レンダラー・テンプレートの全実装が触る型の破壊変更になる。

現在の `ContentPath` はサイト ルート相対のローカル パスしか表せない。これを
**ローカル / リモート / インメモリ**の 3 種を表す位置に一般化し、実際の取得手段は
`IContentSourceProvider` に委ねる。

なぜ骨格段階でやるかというと、`bool` のフラグは後からいつでも足せるが、
`IContentResolver.TryResolve` の出力、`ContentSource.Path`、`Diagnostic.Path` が
いずれも `ContentPath` であるという決定は後から変えられないからである。
リモートを後付けするなら、それまでに書かれた全実装のシグネチャが変わる。

### 9.1 `ContentPath` の表現

**済**（[ContentPath.cs](../Sources/Abstractions/ContentPath.cs)）。以下の設計判断はすべて型に入っている。
実装との差は 2 点のみで、いずれも判断が細くなった方向である。

- **`TryParse` は `file:` スキームをリモートとして受けない。** `C:\posts` や `\\server\share` は
  `Uri` には `file:` の絶対 URI として解析されるので、素朴に「絶対 URI ならリモート」とすると
  ローカルの絶対パスがリモート扱いになる。ローカルの経路に流して**絶対パスとして拒否させる**。
  `file:` を許すと「サイト ルートの外に出られない」保証を素通りする経路にもなる
- **`InMemory` の名前もパスと同じ規則で正規化する。** 名前に階層を持たせたい実装
  （`layouts/post`）があり、正規形が一意でないと同じ名前が別のノードになる

```csharp
public enum ContentPathKind { Local, Remote, InMemory }

public readonly record struct ContentPath : IComparable<ContentPath>
{
    private readonly string? _value;    // 正規形のテキスト表現
    private readonly ContentPathKind _kind;

    public ContentPath(string value);                       // Local のみ（現状と同一の意味）
    public static ContentPath Remote(Uri uri);
    public static ContentPath InMemory(string authority, string name);
    public static bool TryParse(string value, out ContentPath path);
}
```

設計上の制約:

- **`ContentPath(string)` は今後も Local だけを意味する。** 文字列を見て
  「`https://` で始まるならリモート」と分類してはならない。既存の呼び出し側はすべて
  ローカル パスを渡す意図で書かれているので、暗黙に再分類すると
  Markdown の `[x](https://…)` のような外部リンクが取得対象のコンテンツに化ける。
  リモートは `Remote(Uri)` で明示的に作り、構成ファイルから来る文字列だけ `TryParse` で受ける
- **「サイト ルートの外を指せない」という型の保証は `Local` だけのものになる。**
  現在の型 remarks はこれを `ContentPath` 全体の不変条件として書いているので、書き換えが必要。
  `OutputPath` への写像とファイル システムへの解決は `IsLocal` を確認してから行う
- **`Remote` の相対参照解決は URI の意味論に委ねる。** `new Uri(base, relative)` は
  `..` がオリジンより上に出ることを構造的に許さないので、`PathNormalizer` の脱出検査に
  相当するものを自前で書く必要がない。取得したリモート文書中の
  `include::../../etc/passwd[]` は同一オリジン内に解決され、ローカル ファイルには届かない。
  **リモート起点の参照がローカルに解決されることは決してあってはならない**（逆向きは
  ポリシーが許せば可）。これを踏み外すと取得した文書がローカル ファイルを読む経路になる
- **順序は `Kind` → 正規形テキストのオーディナル比較で全順序にする。** 3 種が混在する集合の
  列挙順が安定しないと、そこから作るフィンガープリントが不安定になり R7 が壊れる
- `Extension` / `FileName` は `Remote` では URI パスの最終セグメントから、
  `InMemory` では名前から取る。形式のディスパッチが拡張子を鍵にしているため必要
- `default` が空の `Local` であることは変えない

**`InMemory` は「取得手段がメモリである」という意味ではない。** そこが最も誤解されやすい。

| | 何を表すか | 例 |
|---|---|---|
| プロバイダ | どこから読むか（取得手段） | テストで `Local` パスをメモリから供給する |
| `Kind` | 何を指しているか（アドレス空間） | `InMemory` = そもそもアドレスを持たない内容 |

`InMemory` が要るのは、文字列として直接与えられた内容やプラグインが合成した内容である。
現在の `ContentSource.FromText` は呼び出し側に架空の `ContentPath` を作らせているが、
これを `InMemory` にすれば `Diagnostic.Path` が `posts/x.md` を騙らずに済む。
一方、ステップ 13 の `InMemoryContentSourceProvider` は `Local` パスをメモリから供給するので
**`Kind` は `Local` のまま**で、`IContentSourceProvider` の実装になる。

#### 一意性

`ContentPath` は計算ノードの鍵でありビルドを跨いで永続化されるので、一意性は同一性の問題である。
**ただしプロバイダの識別子を `ContentPath` に持たせてはならない。** 3 つの理由がある。

- **循環する。** `CanOpen(ContentPath)` はパスを見てプロバイダを選ぶ。つまりプロバイダはパスの関数である。
  パスがプロバイダ ID を含むと、パスを作るのにプロバイダが必要になる。
  パーサーが相対リンクから `ContentPath` を作る時点で、誰が供給するかは決まっていない
- **プロバイダの差し替えで同一性が壊れる。** テストで `InMemoryContentSourceProvider` に差し替えると
  全パスの同一性が変わり、「同じ入力なら同じフィンガープリント」の契約テスト（16.1）が
  構造的に成立しなくなる。HTTP プロバイダをオフライン ミラーに差し替えればキャッシュが全滅する。
  `https://x/y` の内容は誰が取ってきても同じ内容であるべきである
- **`ProviderId` は既に `SourceValidator` にある。** あれは*検証*の関心（他プロバイダの
  不透明トークンを誤解釈しない）であり、そこが正しい置き場所である。同一性に持ち上げると上記が起きる

実際に一意でないのは `InMemory` だけである。

| 種別 | 一意性 | 根拠 |
|---|---|---|
| `Remote` | 構造的に一意 | 絶対 URI。スキームがプロバイダの選択も兼ねる |
| `Local` | ほぼ一意 | サイト ルート相対（下記の例外あり） |
| `InMemory` | **一意でない** | 名前に命名権威が無く、2 つのプラグインが衝突しうる |

したがって埋めるべき穴は「誰が供給したか」ではなく「**誰がその名前空間を所有するか**」である。
所有者はパッケージや機能であって実行時のプロバイダ インスタンスではないので、`ProviderId` とは別物になる。

- **`InMemory` は名前空間（authority）を必須にする。** `InMemory("lithify.blog", "sidebar-tags")`。
  衝突は診断エラーにする
- **`Local` はルートが単一なので一意である**（9.3 参照）。シンボリック リンクを張れば
  同じ実体が複数の `ContentPath` から到達しうるが、それは利用者が意図した構成なので検査しない

### 9.2 `IContentSourceProvider`

**型は済**（[IContentSourceProvider.cs](../Sources/Abstractions/IContentSourceProvider.cs) /
[ContentSourceResult.cs](../Sources/Abstractions/ContentSourceResult.cs) /
[SourceValidator.cs](../Sources/Abstractions/SourceValidator.cs) /
[SourceRefreshMode.cs](../Sources/Abstractions/SourceRefreshMode.cs)）。**実装はまだ無い。**
`FileSystemContentSourceProvider` と `Lithify.Sources.Http` は未着手（9.4 の採否判断が先）。

実装したうえで、設計案からの変更が 3 点ある。

- **`IContentSourceProvider.Id` を足した。** `SourceValidator` に `ProviderId` を持たせると決めた以上、
  その値の出所が要る。プロバイダ自身が名乗るのが自然で、他に置き場所がない
- **`Unavailable` は事由を持つ**（`Unavailable(string Reason, Exception? Cause)`）。
  これを受けて `Diagnostic` を出すのだから事由が要り、中核はスキームを知らないので
  書けるのはプロバイダだけである。`Cause` はログのためだけのもので、
  同一性の判断や利用者向けメッセージには使わない
- **`SourceValidator` は内容で等価比較する。** `ImmutableArray<byte>` の既定の等価性は
  基になる配列の**参照**比較なので、コンパイラ生成の実装をそのまま使うと
  永続化して読み直したトークンが元のものと等しくならない。ビルドを跨いで保存する型なので
  `Equals` / `GetHashCode` を書く必要がある

また `IContentResolver.OpenAsync` の戻り値を `ValueTask<ContentSource>` から
`ValueTask<ContentSourceResult>` に変えた。remarks では「4 分岐を解釈した結果を返す」と
書いていたが、**解釈した結果として何を返せるのかがない。** `Missing` を例外にすれば
「決定的なのでキャッシュしてよい」性質が失われ、`Unavailable` を例外にすれば
呼び出し側が両者を区別できない。分岐はそのまま渡す。

```csharp
public interface IContentSourceProvider
{
    bool CanOpen(ContentPath path);

    ValueTask<ContentSourceResult> OpenAsync(
        ContentPath path,
        SourceValidator? previous,      // 前回の検証子。初回は null
        CancellationToken cancellationToken = default);

    // 相対参照の解決規則はアドレス空間ごとに違うのでプロバイダが担う
    bool TryResolveReference(
        ContentPath origin,             // 参照を書いた文書の位置
        string reference,               // 文書に書かれたままの文字列
        out ContentPath resolved);
}

public readonly record struct SourceValidator(
    string ProviderId,                  // 他プロバイダの検証子を誤解釈しないため
    ImmutableArray<byte> Token);        // 中身はプロバイダの私事
```

設計上の制約:

- **`Fingerprint` は内容バイト列のハッシュのままでなければならない。ETag を
  `Fingerprint` にしてはならない。** これが最も間違えやすい点である。ETag は内容の同一性ではなく
  「取り直す必要があるか」の代理にすぎず、nginx の inode 由来 ETag・ミラーの切り替え・
  CDN による表現の変化のいずれでも、**同じバイト列に別の値**が付く。
  early cutoff の根拠に据えると R7 が壊れる。ゆえに 2 つの別概念にする:
  同一性は `Fingerprint`、再取得の必要性は `SourceValidator`
- **`SourceValidator` はプロバイダ以外から解釈されない不透明トークンにする。** 抽象に
  ETag や 304 を出してはならない。AsciiDoc 仕様がリモート ソースのスキームを定めていないのと同じ理由で、
  HTTP を特別扱いする根拠が中核側には無い。HTTP なら ETag / Last-Modified、
  FTP なら `MDTM` + `SIZE`、git なら commit SHA、ローカル ファイルなら mtime + サイズ。
  **ローカルがこの抽象の特殊例になる**のが実際の見返りである
- **結果は 4 分岐にする。** `FileNotFoundException` だけで足りていたローカル専用設計との実質的な差はここ

  | 結果 | 意味 | 扱い |
  |---|---|---|
  | `Fresh(ContentSource, SourceValidator, SourceStability)` | 取得した | 内容から `Fingerprint` を計算する |
  | `Unchanged(SourceValidator)` | 取り直し不要 | 前回の `Fingerprint` と `ChangedAt` を据え置く（= early cutoff） |
  | `Missing` | 参照先が存在しない | **コンテンツの誤り。** `Diagnostic` にする。決定的 |
  | `Unavailable` | 接続不能・タイムアウト | **環境の誤り。** キャッシュを汚してはならない |

  **`Missing` と `Unavailable` を潰してはならない。** 潰すとネットワーク断が
  「include 先が消えた」として伝播し、欠落したページを正常な出力として書き出す
- **`Unchanged` は「ネットワークで確認した結果、変わっていなかった」を意味しない。**
  「変わっていないと判断した」だけである。HTTP の `Cache-Control: max-age` や `Expires` が
  鮮度期間内であれば、プロバイダは**ネットワークに一切触れずに** `Unchanged` を返せる
  （条件付き GET すら要らない）。この判断はプロバイダの私事なので、
  **鮮度の概念を抽象に出す必要はない。** `OpenAsync` は常に呼ばれ、
  通信するかどうかはプロバイダが内部で決める。この性質を保てているかが、
  抽象が正しく引けているかの試験紙になる
- **プロバイダは自前の永続ストアを必要とする。** `Unchanged` を返すには、
  検証子・鮮度の期限・**内容そのもの**をビルドを跨いで保持していなければならない。
  下流がキャッシュ済みなら内容は要らないが、下流のキャッシュだけが失われた場合
  （`.lithify` の一部欠落、ノードの追加）に内容を再取得できる必要がある。
  よって `IBuildCache` とは別に、プロバイダが名前空間を分けて使える永続領域を渡す
- **時刻は `TimeProvider` から取る。** 鮮度の判定は時刻に依存するので、
  `DateTimeOffset.UtcNow` を直に読むとキャッシュの挙動をテストできない
- **再確認を強制／抑止するフラグは `LithifyOptions` に置く。** 鮮度がプロバイダの私事である一方、
  「通信を許すか」は利用者の判断である。3 状態にする必要がある

  | 値 | 意味 | 用途 |
  |---|---|---|
  | `Default` | 鮮度が切れていれば再確認する | 通常のビルド |
  | `Always` | 鮮度を無視して必ず再確認する | 公開前の最終ビルド。`--refresh-sources` |
  | `Never` | 通信しない。鮮度切れでもストアの内容を使う | オフライン。`--offline` |

  `Never` で内容がストアに無い場合は `Unavailable` である（`Missing` ではない）。
  既存の `Force` とは別の軸なので統合してはならない。`Force` は出力の書き直し、
  これは入力の再取得であり、「キャッシュを信じないが網には出たくない」が正当な組み合わせとして要る
- **リモート取得の実装は別パッケージ（`Lithify.Sources.Http` / `UseHttpSources()`）に置く。**
  パッケージを入れない限り取得の能力がそもそも存在しないので、`bool` の既定値より強い形で
  既定拒否になる。ローカルの `FileSystemContentSourceProvider` は `Lithify.Core` に置く
  （`Lithify.Core` に `HttpClient` を持ち込まないため）
- **`HttpClient` に HTTP キャッシュは実装されていない。** `SocketsHttpHandler` は鮮度も検証子も扱わない。
  `System.Net.Cache.RequestCachePolicy` は `WebRequest` 時代の API で `HttpClient` には効かず、
  ASP.NET Core の response / output caching はサーバー側の機構なので下流の取得には使えない。
  `Microsoft.Extensions.Caching.*` は汎用の KV ストアであって `Cache-Control` を解釈しない。
  したがって RFC 9111 の鮮度計算・検証子の保持・`Vary` の扱いは BCL の外から持ってくる必要がある。
  自前で書くのではなく既存のハンドラー実装を使う方針とその分界は 9.4 に書く
- **相対参照の解決はプロバイダが担う。** 解決規則がアドレス空間ごとに違うので、
  `ContentPath.Combine`（パス セグメントの結合）だけでは足りない

  | 起点 | `../shared/x.adoc` の解決規則 |
  |---|---|
  | `Local` | パス セグメントの結合。`ContentPath.Combine` そのまま |
  | `Remote`（HTTP） | **RFC 3986 の相対参照解決。** 基準が末尾 `/` の有無で変わり、クエリとフラグメントの扱いも規定がある |
  | `Remote`（git） | リポジトリ内のパス結合。**リビジョンを引き継ぐ**（起点が SHA 固定なら参照先も同じ SHA） |
  | `InMemory` | authority を引き継いだ名前の結合。`..` に意味を与えるかはその名前空間の所有者が決める |

  中核がこれらを全部知るのは「中核はスキームを特別扱いしない」という方針と矛盾するので、
  `TryResolveReference` をプロバイダに置く。`ContentPath.Combine` は
  `FileSystemContentSourceProvider` の実装詳細に降りる（型としては残すが、
  相対参照解決の唯一の手段ではなくなる）
- **解決は 1 つのプロバイダ内で閉じない。** ローカル文書に絶対 URI が書かれれば
  リモート プロバイダの領域に移り、逆にリモート文書からローカルへは**解決させない**
  （9.3 参照）。したがって `IContentResolver` は
  「**起点のプロバイダに解決を委ね、得られた `ContentPath` で改めてプロバイダを選び直す**」
  という 2 段の段取りになる。プロバイダを跨ぐ移動の可否を判断できるのは
  プロバイダ*間*を見ている `IContentResolver` だけなので、この責務分割が必要である
- **絶対パス化の最後の 1 段はプロバイダが持つ。** `ContentPath` から実際に開けるアドレスへの
  変換（ローカルなら `SourceRoot` と結合した完全修飾パス、HTTP なら絶対 URI）は
  プロバイダの内部で行い、外に出さない。9.6 の「変換を 1 箇所に集約する」の
  「1 箇所」とはプロバイダのことである
- ~~`IContentFileResolver` は `IContentResolver` に改名する。ファイルに限らなくなるため~~
  **済**（[IContentResolver.cs](../Sources/Abstractions/IContentResolver.cs)）。
  型と remarks は先行して書き換えたが、**実装はまだ無い**（`IContentSourceProvider` が
  未定義なので remarks 中の参照は `<c>` で書いてある）。
  `IRenderContext.FileResolver` も `ContentResolver` に改名済み

### 9.3 `FileAccessPolicy` を削除する

**済。** 型を削除し、`LithifyOptions.FileAccess` を除去、`AsciiDocOptions` の remarks を
「Lithify の語彙に置き換えて持つこともしない」理由の説明に差し替えた。以下は判断の記録である。

**改名ではなく削除する。** 使用箇所は 2 つ（[LithifyOptions.cs](../Sources/Hosting/LithifyOptions.cs) の
プロパティ 1 つと、`AsciiDocOptions` の remarks 参照）なので今なら安い
（`IContentResolver` の remarks 参照は改名の際に除去済み）。

この型は「Asciidoctor の safe mode に対応するものが必要だろう」という前提で作られたが、
**Asciidoctor の safe mode は Asciidoctor という実装の機能であって AsciiDoc 仕様ではない。**
したがって「あるから必要」にはならない。判断すべきは Lithify 自身がファイル I/O をするうえで
外から与えなければ決まらないものが何かだけで、3 つのメンバーはいずれもそれに該当しない。

| メンバー | 削除の理由 |
|---|---|
| `AllowedRoots` | **型として意図を表現できていない。** 要素が `ContentPath` なので[サイト ルート配下しか書けず](../Sources/Abstractions/ContentPath.cs)、remarks が言う「共有インクルード ディレクトリ等の追加のルート」は書けない。しかも空 = 全許可なので、値を入れると許可が*狭まる*（remarks の意図と逆）。ルートは単一に確定する（下記） |
| `AllowSymbolicLinks` | **どちらの値も正しくない。** 下記参照 |
| `AllowIncludes` | 既定 `true`、切ると AsciiDoc の一般的な文書が壊れる、切りたい利用者がいない |

制限は既に `ContentPath` が型として担っている（[PathNormalizer](../Sources/Abstractions/PathNormalizer.cs) が
絶対パスとルート脱出を弾く）。削除された remarks は「サイト ルート配下のみを許可し」と
書いていたが、実際に保証しているのはこの型ではなく `ContentPath` だった。

#### シンボリック リンクは検査しない

**`AllowSymbolicLinks` を削除し、代わりに何も置かない。** `ResolveLinkTarget` による
実体解決も、ルート含有判定も、同一実体の重複検出もしない。ファイルはファイルとして読む。

`false` は正当な構成（ルート内を指すリンク）を落とし、`true` は名目上の制限を無意味にする。
どちらも正しくないので選ばせる意味がない。では検査を実装すべきかというと、それも要らない。
**シンボリック リンクは事故で生えるものではなく、利用者が意図して張ったものである。**
リスクは承知の上と見なすのが妥当で、Lithify が代わりに判断する立場にない。

ここで**再現性の問題とセキュリティの問題を混同してはならない。** 別の問題であり、
成立条件も結論も違う。

| | 再現性 | セキュリティ |
|---|---|---|
| 何が問題か | clone に実体が入らない（リンクは commit されるが実体は入らない） | 意図しないファイルが出力に載る |
| 成立条件 | 常に | **コンテンツの作者 ≠ ビルドの実行者**のときだけ |
| 「利用者は承知の上」で済むか | **済む**（自分で張ったのだから） | **済まない**（張ったのは他人） |
| 結論 | `SourceStability.Unpinned` として扱い `ReproducibilityMode` に委ねる（9.5.1） | **対象外**（下記） |

**セキュリティの側は明示的に対象外とする。** 外部から受け取ったコンテンツを
そのままビルドする構成（PR の内容を CI でビルドする等）では、
`shared/x.adoc -> ~/.ssh/id_rsa` のようなリンクを含めることで内容が公開されうる。
これを Lithify で防がない理由は次の通りである。

- **パス検査では防ぎきれない。** ハード リンクは実体パスという概念が成立しないので
  原理的に解決できず、TOCTOU も残る。半端な検査は「防げているつもり」にさせるので、
  無いほうが誤解が少ない
- **ファイルの読み取り範囲は OS とコンテナが制限できる。** 信頼できないコンテンツを
  ビルドするなら、コンテナの中で、そのビルドに必要なファイルだけを見せて実行すればよい。
  そうした構成を取っていない環境で、静的サイト ジェネレーターのパス検査が
  埋められる差は小さい

**検出も通知もしない。** 読み取りのたびにリンクかどうかを見れば、検査しないと決めた話が
形を変えて戻ってくる（`FileInfo.LinkTarget` を見る費用自体は小さいが、
そこに「見ているのだから何か言うべきだ」という圧が生じる）。
代わりに**利用者向けドキュメントで既知の制約として触れ、at your own risk とする。**
以下は 9.5.1 の `SourceStability` の判定を除き、実装が能動的に扱うものではない。

| 制約 | 起きること |
|---|---|
| ツリー内部のリンク先の変更が live-reload に反映されない | 下記のとおり全プラットフォーム共通の仕様。ビルドは通るのに更新されない |
| リンクの循環で列挙が終わらない | **実測で確認**（[.NET の列挙はループを辿る](#)。25 秒で 60,683 件・パス長 546,226 文字まで伸び、例外も出ず継続）。`\\?\` 前置のため `MAX_PATH` にも当たらないので実質ハングする |
| 同じ実体が複数の `ContentPath` から到達し出力が重複する | 両方が列挙対象のときだけ。リンク先が列挙対象でなければ何も起きない。内容キャッシュは `Fingerprint` が同じ値になるので勝手に畳まれる |
| ルート外のリンク先は clone に入らない | 再現性の問題として `SourceStability.Unpinned` になる（9.5.1） |

live-reload の制約は**実装の漏れではなく意図された、全プラットフォーム共通の挙動**である。
Windows では `IncludeSubdirectories = true` でも、ファイル シンボリック リンク・
ディレクトリ シンボリック リンク・ジャンクションのいずれについても検出されないことを
実測で確認した（同じルート内の実ファイルは検出される）。Unix 側は
[dotnet/runtime#25078](https://github.com/dotnet/runtime/issues/25078) /
[PR #52679](https://github.com/dotnet/runtime/pull/52679) で、
一度 `IN_DONT_FOLLOW` を外して子リンクを辿る実装になったものが
「Windows に合わせるため」意図的に戻されている（"I have undone the changes that were
causing following child symlinks by default, this in order to behave similar to windows"）。
「follow symlinks」のフラグは検討課題として挙がったまま追加されていない。
**将来の .NET で改善されることを期待して設計してはならない。**

**ただし監視ルート自身がシンボリック リンクである場合は正常に監視できる。**
上記 PR が修正したのはこの場合で、.NET 6 以降は Unix でも動く（Windows は元から動く）。
つまり `SourceRoot` 自体をリンクにする構成は問題なく、**駄目なのはツリー内部のリンクだけ**である。

#### ルートは単一にする — 仮想ファイル システムを作らない

複数ルートは `ContentPath` の一意性を直接壊す（同じ相対パスが複数のルートで解決しうる）。
代案として「`shared/` という接頭辞に外部ディレクトリを写す」プロジェクト レベルの
仮想ファイル システムを考えたが、これも採らない。

- **`FileSystemWatcher` が写像を知らない。** 写像先の変更を仮想パスに逆写像する層が必要になり、
  逆写像は多対一になりうる
- **診断のパスが二重になる。** 利用者は `shared/x.adoc` と書き、エディターが開くのは
  実ディレクトリのパスである。`Diagnostic.Path` にどちらを出すかを決めねばならず、
  実際には両方必要になる
- **`OutputPath` への写像が壊れる。** 写像先が 2 箇所から参照されたら重複出力になる
- **得られるものが薄い。** 共有ディレクトリは git submodule でルート内に置けば済む。
  それなら再現可能で、監視も効き、パスも一意である。
  それでも外を参照したい利用者はシンボリック リンクを張れる（上記のとおり妨げない）

したがってローカル コンテンツは常に `LithifyOptions.SourceRoot` 配下のみとする。

#### セキュリティを理由にしたポリシーは 1 つも残らない

当初、リモート取得については許可リスト（`AllowedOrigins`）が必要だと考えた。
**これも廃止する。** 理由は単純で、**設定がコンテンツと同じリポジトリにあるなら
一緒に書き換えられる**からである。`include::https://internal/secret[]` を追加する PR は、
同じ PR で許可リストに 1 行足せばよい。防いでいるつもりで何も防いでいない。

これはシンボリック リンクの検査を却下したのと同じ形の欠陥である
（守れないものを守るふりをする）。同じ基準で却下する。

許可リストが機能するのは「**コンテンツを書ける人が変更できない場所**に設定がある」場合だけだが、
`LithifyOptions` は `IConfiguration` 経由で来るので、リポジトリ内のファイルからも
環境変数からも来る。**型では区別できない。**

代わりに、本来の対処を利用者向けドキュメントで案内する。いずれも Lithify の外にある。

| 対処 | なぜそちらが正しいか |
|---|---|
| **信頼できない相手からの PR をビルドしない** | 到達範囲を絞るより根本的である。GitHub なら fork からの PR に承認を要求できる（`pull_request_target` を使わない限りシークレットも渡らない） |
| **ランナー側で通信先を制限する**（`iptables` 等） | 本当に通信を止めるので、PR の内容では変えられない。許可リストと違って設定がコンテンツの外にある |

**能力そのものはパッケージ参照で制御される。** `Lithify.Sources.Http` を参照しなければ
リモート取得の実装が存在しない。これは csproj の変更を要するので PR の中身だけでは足せず、
**型ではなくパッケージの境界として保証されている。** `bool` の設定項目より強く、
その上に効かない許可リストを重ねる必要はない（9.2 参照）。

**ただしこれは「リモート取得に関する懸念が何もない」という意味ではない。**
許可リストが答えにならない別の脅威——参照先のホストそのものが乗っ取られる場合——が残る。
そちらはアドレスを絞るのではなく**内容を固定する**ことで対処し、
既に設計に入っている `SourceStability` / `ReproducibilityMode` がそのまま効く。
9.5.3 を参照。テンプレートに現れるリモート参照は 9.5.4 で別に扱う。

#### 残る関心の置き場所

削除して失われるものはない。

| 関心 | 置き場所 | 理由 |
|---|---|---|
| リモート起点の参照がローカルに解決されないこと | `IContentResolver` | プロバイダ*間*の関係なのでプロバイダ単体では守れない。**常に真の規則なので設定項目にしない**（取得した文書がローカル ファイルを読む経路を作らないため） |

### 9.6 相対パスの解決とパス長

**Lithify が担うのは「相対パスをリンク元の文書を基準に正しく絶対パス化する」ことだけである。**
Windows の `MAX_PATH` については BCL が対処するので、Lithify 側で気にしない。

#### 基準はリンク元の文書である

`include::../shared/x.adoc[]` の `..` の基準は**それを書いた文書の位置**であって、
プロセスのカレント ディレクトリではない。カレント ディレクトリを基準に解決したら
参照先が変わってしまうので、これは仕様上の要求である。

起点を `origin` で受け取る形は既に正しい
（[`IContentResolver.TryResolve`](../Sources/Abstractions/IContentResolver.cs) が
`origin` を取り、[`ContentPath.Combine`](../Sources/Abstractions/ContentPath.cs) が
そのディレクトリに結合して `..` を正規化する）。**解決そのものは 9.2 のとおり
プロバイダの `TryResolveReference` に委ねる**（規則がアドレス空間ごとに違うため）。
ローカルのプロバイダはそこで `SourceRoot` と結合し、完全修飾パスにしてから I/O に渡す。

- **変換には `Path.GetFullPath(relative, basePath)` を使う**（基準を明示的に渡す 2 引数版）。
  カレント ディレクトリはプロセス全体で共有された可変状態なので、
  `serve` の並行リクエストや将来の並列ビルドで誰かが変えれば全ての解決が狂う。
  診断に出すパスが実行時の状態で変わるとログの比較もできない
- **`LithifyOptions.SourceRoot` の既定値 `"."` が唯一の相対パスである。**
  構成を読み込んだ直後に一度だけ絶対パス化する。基準は CLI が起動した作業ディレクトリで
  よいが（利用者が `cd` して `lithify build` と打つのだから自然）、
  以降のコードがカレント ディレクトリを参照しないよう、そこで確定させる

#### `MAX_PATH` は BCL が対処するので気にしない

深い階層や長い slug から生成される出力パスは 260 文字を容易に超えるが、
**Lithify 側の対策は要らない。** .NET Core 以降の BCL は `MAX_PATH` の検査を自ら行わず、
`CreateFile` を呼ぶ前に
[`PathInternal.EnsureExtendedPrefixIfNeeded`](https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/IO/PathInternal.Windows.cs)
で `\\?\`（UNC なら `\\?\UNC\`）を前置する。

実測（.NET 10、478 文字のディレクトリ / 484 文字のファイル）で
`CreateDirectory` / `WriteAllText` / `ReadAllText` / `EnumerateFiles` /
`Path.Combine` / `Path.GetFullPath(relative, basePath)` はいずれも通った。
相対パスのまま渡しても、`/` 区切りで渡しても通る
（前置の判定に到達する前に絶対パス化と区切り文字の正規化が済んでいる）。

- `AppContext` の `Switch.System.IO.UseLegacyPathHandling` と
  `Switch.System.IO.BlockLongPaths` は .NET Framework 向けの互換スイッチであり、
  .NET Core 以降には存在しない。**設定してはならない**（無意味であり、誤解を招く）
- アプリ マニフェストの `longPathAware` とレジストリの `LongPathsEnabled` は
  Win32 API を直接使うコードに効くもので、BCL 経由の I/O には要らない。
  ただし**測定環境が `LongPathsEnabled = 1` だったため寄与を切り分けられていない。**
  無効な環境でも通ることを確認する（16.2 参照）
- **`Environment.CurrentDirectory` には長いパスを設定できない**（実測で失敗。
  `\\?\` を受け付けない Win32 の制約なので回避不能）。上記のとおり
  カレント ディレクトリは元々参照しないので、実害はない

唯一の注意点は**依存ライブラリに直接ファイルを開かせないこと**である。
ライブラリが Win32 を直接叩いていたり自前でパス長を検査していると、そこだけ落ちる。
これは既に別の理由（ポリシーと依存記録の迂回）で不変条件として要求されているので、
**長いパスへの対応も同じ境界で自動的に満たされる。**
自分でファイルを開く可能性が残るのは `TextMateSharp` の文法・テーマ ファイル（ステップ 11）と
テンプレート エンジンの partial 解決（ステップ 10.4）で、
いずれもコンテンツではなく利用者が配置する設定資産なので階層が浅く、実害は生じにくい。
partial は `IContentResolver` 経由に一本化すれば回避できるので、そちらを優先する。

### 9.4 `Lithify.Sources.Http` の構成 — `HttpClient` の外か `DelegatingHandler` か

**RFC 準拠の HTTP キャッシュを自前で書くことは避ける。**
[Meziantou.Framework.Http.Caching](https://www.meziantou.net/implementing-rfc-compliant-http-caching-for-httpclient-in-dotnet.htm)
が `DelegatingHandler`（`HttpCachingDelegateHandler`）として RFC 7234 と RFC 8246（`immutable`）を実装しており、
`IHttpCacheStore` でストアを差し替えられる。鮮度計算・条件付き検証・`Vary`・cache stampede の防止まで
含まれるので、この部分を書き直す理由はない。**採否をステップ 9 の判断事項とする**
（`Meziantou.Framework.*` は個人メンテナンスのパッケージなので、依存として受け入れるかは別途決める）。

その上で、**ハンドラーに載せてよいのは RFC の意味論までで、Lithify 固有の関心は載らない**。
分界を先に決めておかないと責務が混ざる。

| 層 | 担うもの |
|---|---|
| プロバイダ（`HttpClient` の外） | `SourceValidator` への写像、`ContentSourceResult` の 4 分岐、`SourceStability` の判定、`Never` / `Always` の適用、内容の永続保持 |
| `DelegatingHandler` | RFC の鮮度計算・条件付き検証・`Vary`（既存パッケージ） |
| `DelegatingHandler` | 再試行・バックオフ、認証、`User-Agent`、ログ |
| `SocketsHttpHandler` | 接続、TLS、`AllowAutoRedirect = false` |

ハンドラーに載せられない理由が個別にある:

- **`Unchanged` をハンドラーからは表現できない。** `DelegatingHandler` は
  `HttpResponseMessage` を返さなければならないので、キャッシュ ヒットは
  「本文入りの 200」として返る。プロバイダ側から `Fresh` と区別が付かず、
  既に持っているバイト列を読み直して `Fingerprint` を再計算することになる。
  結果は正しいが `Unchanged` の意義（本文を触らない、`ChangedAt` を据え置く）が消える。
  **ハンドラーがキャッシュから返したかどうかを、プロバイダが知る経路が必要になる**
  （`HttpResponseMessage.Headers` の自前マーカー、または `Age` の有無での判定）。
  ここが既存パッケージを使う場合の主要な接合点であり、採否の判断材料になる
- **リダイレクトの毎ホップ検査はハンドラーの内側に置けない。** `SocketsHttpHandler` が
  リダイレクトを内部で追うと `DelegatingHandler` からは最終応答しか見えない。
  9.3 の要求を満たすには `AllowAutoRedirect = false` にしてプロバイダがホップを回す
- **`Never`（オフライン）はハンドラーでは表せない。** RFC の意味論としては
  `only-if-cached` が近いが、鮮度切れでもストアの内容を使う点が異なる
  （RFC 準拠なら 504 を返すべき場面で、Lithify は古い内容を使いたい）。
  これはビルド ツールとしての要求であって HTTP キャッシュの要求ではないので、プロバイダ側に置く
- **ストアの二重化を避ける。** ハンドラー層のキャッシュ（`IHttpCacheStore`）と、
  プロバイダが下流のキャッシュ欠落に備えて持つ内容（9.2 参照）が同じバイト列になる。
  `IHttpCacheStore` の実装をプロバイダの永続領域に向けて**一本化する**

### 9.5 帰結

設計で消せないので [architecture.md](architecture.md) に記録する側:

- **`LinkTarget.External` と `Remote` の `ContentPath` を統合してはならない。**
  前者は「出力にそのまま書き出すハイパーリンク」、後者は「Lithify が取得してビルド入力にする内容」である。
  潰すと本文中の全外部リンクを取得しに行くことになる
- **検証フェーズが async になる。** ステップ 17.1 に書く再検証アルゴリズムは、
  依存の検証が同期かつ安いことを前提にしている。低速になるのは鮮度が切れている場合だけなので
  （鮮度期間内なら往復は起きない）常時の劣化ではないが、**署名が async になること自体は避けられない**。
  ローカルのみのサイトでも `ValueTask` を通ることになる
- **再現性（同一入力 → 同一出力）は常に保たれる。** 取得された内容は入力の一部なので、
  増分ビルドの正しさ（early cutoff、R7）はリモートの有無に依存しない。
  再現可能ビルドとは別の性質である（17.1 参照）
- `IContentFormatRegistry.TryGetFormat` は拡張子を鍵にしている。リモートは拡張子を持たない場合があるので、
  媒体型から形式を得る経路も要る

#### 9.5.1 再現可能ビルドは local/remote では決まらない

**`SourceStability` は済**（[SourceStability.cs](../Sources/Abstractions/SourceStability.cs)）。
判定を行う実装（プロバイダ）はまだ無い。

**軸は「ローカルかリモートか」ではなく「アドレスが一意な内容を指すか」である。**
リモートだから直ちに再現不可能ということはない。

| アドレス | 再現可能か | 理由 |
|---|---|---|
| `Local`（サイト ルート配下の実ファイル） | **可** | git に入る |
| `Local`（ルート外を指すシンボリック リンク経由） | **不可** | リンク自体は commit されるが実体は入らない。clone では壊れたリンクになる |
| `Remote` — commit SHA 付き git | **可** | SHA が内容を一意に決める |
| `Remote` — 不変を宣言した URL（`Cache-Control: immutable`、内容ハッシュ入り URL） | **可** | 発行側が不変を約束している |
| `Remote` — 素の HTTP URL | **不可** | 同じ URL が別の内容を返しうる |

したがってルート外を指すシンボリック リンク経由の参照は、素の HTTP URL と同程度に
再現不可能である。**ローカルだから安全という前提を置いてはならない。**

**これがルート外リンクの扱いを決める。** 禁止せず `Unpinned` として読み、
下記の `ReproducibilityMode` に委ねる（`Require` なら停止、既定の `Warn` なら警告して続行）。
セキュリティの関心とは別問題であり、そちらは対象外とする（9.3 参照）。

ただし**実装上の注意**として、ルート外リンクは `SourceStability` を判定するためだけに
実体解決を要する唯一の場所になる。`ReproducibilityMode.Ignore` では解決を省略してよい
（結果を使わないので）。9.3 で「検査しない」としたのはアクセス許否の検査であり、
分類のための解決とは別である。

分類の主体は `IContentSourceProvider` である。**アドレスが一意かどうかを判定できるのは
そのスキームを理解しているプロバイダだけ**なので、`ContentSourceResult` に添える。
中核は真偽値を集約するだけで、スキームごとの規則を知る必要がない。

```csharp
public enum SourceStability { Pinned, Unpinned }
```

- **`Pinned` は「取得のたびに同じ内容が返る」ことの宣言である。** git の commit SHA、
  `immutable` を宣言した応答、内容ハッシュを含む URL。ブランチ名や tag は `Unpinned`
  （tag は動かせる）
- **判定は 1 度だけでなく、取得ごとに行う。** 同じプロバイダでも
  `https://x/v1.0.0/y`（`immutable`）と `https://x/latest/y` は別の分類になる

#### 9.5.2 再現可能性を要求するオプション

**型とオプションは済**（[SourceStability.cs](../Sources/Abstractions/SourceStability.cs) に
`ReproducibilityMode`、[LithifyOptions.cs](../Sources/Hosting/LithifyOptions.cs) に
`Reproducibility` と `SourceRefresh`）。**診断を出す実装はまだ無い。**
9.2 の「再確認を強制／抑止するフラグ」は `SourceRefreshMode`
（[SourceRefreshMode.cs](../Sources/Abstractions/SourceRefreshMode.cs)）として実装した。
CLI の `--require-reproducible` / `--refresh-sources` / `--offline` はステップ 8 の
コマンド定義側なので未着手である。

内容が文章であることを踏まえると、常に厳格である必要はない。既定は緩く、
**要求する選択肢を用意する**。

```csharp
public ReproducibilityMode Reproducibility { get; set; } = ReproducibilityMode.Warn;
public enum ReproducibilityMode { Ignore, Warn, Require }
```

| 値 | `Unpinned` な参照があったとき | 想定 |
|---|---|---|
| `Ignore` | 何もしない | 下書き、実験 |
| `Warn` | 警告の診断（既定） | 通常の執筆 |
| `Require` | エラーで中止。`--require-reproducible` | 公開ビルド、CI、アーカイブ |

設計上の制約:

- **`Require` はビルド全体を止めるのではなく、`Unpinned` な参照ごとに診断を出す。**
  `Diagnostic` は参照元のパスと位置を持てるので、どの include を直せばよいかが示せる。
  どれか 1 つの失敗で全体が止まると原因が分からない
- **`Require` を `Never`（オフライン）で代用できない。** オフラインでもストアの内容は
  取得時点のものなので、それが一意なアドレス由来かどうかは別問題である
- **ロックファイルは骨格段階では作らない。** `Unpinned` な URL の取得結果を
  内容ハッシュで固定する仕組み（npm の `package-lock.json` 相当）は再現性を回復させるが、
  更新の意味論・衝突解決・`--update` の設計を要する独立した機能である。
  ただし **`SourceStability` を今入れておけば後から載せられる**。逆に無いと、
  何を固定すべきかを判定する足場が無い
- **git プロバイダ（`Lithify.Sources.Git`）は骨格段階では作らない。** `Pinned` な
  リモート ソースの主要な実現手段なので設計上は重要だが、`IContentSourceProvider` が
  正しく引けていれば後から独立したパッケージとして足せる。
  **`SourceStability` の設計はこれを想定して行う**（SHA 指定は `Pinned`、
  ブランチ・tag 指定は `Unpinned`）

#### 9.5.3 アドレスの乗っ取りへの対処は固定であって許可リストではない

9.3 で許可リストを廃止したが、リモート取得に関する脅威が 1 つ残る。
**PR に何ら悪意がなくても、参照先のホストが乗っ取られうる**（ドメインの失効と再取得、
CDN やパッケージ配布元の侵害、依存先リポジトリの改竄）。

**この脅威に許可リストは効かない。** 乗っ取られるのは許可済みのホストであり、
設定は誰も書き換えないので、9.3 の議論（設定がコンテンツと同じリポジトリにある）とは
無関係に無力である。**アドレスを絞る対処ではなく、内容を固定する対処が要る。**

これは 9.5.1 の分類がそのまま答えになっている。**`Unpinned` とは定義上
「同じアドレスが別の内容を返しうる」ことであり、乗っ取りはまさにその場合である。**
`ReproducibilityMode.Require` は `Unpinned` な参照を拒否するので、
再現可能性のために用意した仕組みが供給連鎖の対処を兼ねる。偶然ではなく、
どちらも「アドレスが内容を一意に決めるか」という同じ問いに根を持つためである。

| 対処 | 効くか |
|---|---|
| `AllowedOrigins`（許可リスト） | **効かない。** 許可済みのホストが乗っ取られる話である |
| `ReproducibilityMode.Require` + `Pinned` なアドレス | 効く。内容ハッシュ入り URL や commit SHA なら別の内容は取得できない |
| `SourceValidator` による再取得の抑制 | 効かない。「取り直す必要があるか」の判断にすぎず、内容が変わっていれば素直に取り直す |

ただし**現状の設計には穴がある。** `Pinned` の判定をプロバイダに委ねているので、
`Cache-Control: immutable` を見て `Pinned` と分類することになる。
それは**発行側の自己申告**であり、乗っ取った側が付け替えられる。
`Pinned` が意味を持つのは commit SHA や内容ハッシュ入り URL のように
**アドレス自身が内容を決めている**場合だけで、宣言に依拠する場合は保証がない。

**したがって全体が形になるまで、セキュリティのための機構は 1 つも作らない。**
`ReproducibilityMode` は再現可能性のための機能であって、
供給連鎖の対処を兼ねるのはその副産物にすぎない。この段階では
**利用者向けドキュメントで推奨事項として案内するに留める**（`Pinned` なアドレスを使う、
公開ビルドでは `Require` にする、通信先の制限はランナー側で行う）。
9.3 と同じ基準である。守れないものを守るふりをするより、
守り方を書いて利用者に選ばせるほうが実効性がある。

将来の候補として**利用者が期待する `Fingerprint` を書ける経路**を挙げておく
（subresource integrity に相当する）。これがあれば内容の差し替えがビルドの失敗として現れ、
`Pinned` の自己申告に依拠しなくなる。`SourceValidator` とは別の概念である
（あちらは「取り直す必要があるか」、こちらは「取れた内容が期待どおりか」）。
ロックファイル（9.5.2）と同じ足場に載るので `SourceStability` があれば後から足せる。
これがリポジトリ内にあっても意味を持つ点も許可リストとの違いである
（ハッシュを書き換える PR はレビューで内容の変更として見える。
許可リストへの 1 行追加のように、何が変わったかを隠せない）。

**正直な限界も書いておく。** 素の HTTP URL を追随する目的で意図的に `Unpinned` にしている参照では、
正当な更新と乗っ取りを Lithify の内部で区別する手立てはない。
`Require` が `Unpinned` を一律に拒否することだけが唯一の全面的な答えである。

#### 9.5.4 テンプレートのリモート参照は 2 種類ある

リモート参照はコンテンツに限らない。テンプレートにも現れるが、**性質の異なる 2 つを
区別しなければならない。** 一方は Lithify が取得し、他方は Lithify が取得すらしない。

| | ビルド時に取り込む参照 | 出力に書き出す参照 |
|---|---|---|
| 例 | partial をリモートから解決する、共有レイアウトを URL で参照する | `<script src="https://cdn.example/x.js">`、`<link rel="stylesheet">`、Web フォント |
| Lithify が取得するか | **する。** 取得した内容がテンプレートの一部になる | **しない。** 出力に載る文字列にすぎない |
| 乗っ取りの影響を受けるのは | ビルドの結果 | **閲覧者のブラウザ**（ビルドは正常に見える） |
| 対処の置き場所 | 下記のとおり既存の機構に乗る | **テンプレートの作者**。Lithify の外 |

**前者はコンテンツと同じ機構に乗せる。** これは `LinkTarget.External` と
`Remote` の `ContentPath` を統合してはならない（9.5 参照）という既定の区別が、
テンプレートにも同じ形で現れているだけである。

- **partial の解決は `IContentResolver` 経由に一本化する**（9.6 で長いパスの理由から
  既に要求している）。ここを通れば `SourceStability` も `ReproducibilityMode` も
  コンテンツと同じに効き、テンプレート エンジンごとに書く必要がない
- **`ICompiledTemplate.Fingerprint` は partial を含めた合成でなければならない**（10.4）。
  リモート partial も同じ規則に従う。取得した内容の `Fingerprint` を畳み込めば
  差し替えが再コンパイルとして伝播する
- **テンプレート エンジン自身に URI を読ませてはならない**（不変条件として既に記載）。
  エンジンが直接取ると依存記録も分類も迂回する

**後者に対して Lithify は何もできない。** 出力に書かれた URL を取得するのは閲覧者の
ブラウザであり、ビルド時には何も起きない。テンプレートの作者が
subresource integrity（`integrity` 属性）を書くのが対処で、これは Lithify の関心ではない。
**ただし脅威としてはこちらのほうが大きい**（影響がビルドではなく閲覧者に及び、
ビルドは最後まで正常に見える）ので、9.5.3 と同じくドキュメントに推奨事項として書く。

**将来 `integrity` を Lithify が生成する機会はある。** 出力に載せるアセットを
Lithify がパイプラインに取り込む場合（バンドル、指紋付きファイル名）は内容を持っているので
ハッシュを計算できる。これはアセット パイプラインの話であり骨格段階では扱わないが、
**「テンプレートが書いた URL」と「Lithify が出力するアセット」を混同しないこと。**
前者に対しては原理的に計算できない（内容を持っていない）。

## 10. プラグイン パッケージ

各パッケージが `ILithifyBuilder` 拡張メソッドを1つ公開し、DI に自分の実装を登録する。
`UseLithify()` はパーサーもレンダラーもテンプレート エンジンも登録しない（既定で何かを登録すると
「差し替え可能」という建前が崩れる）ので、ここで登録されるものが唯一の供給源になる。

### 10.1 `Lithify.Parsers.Markdig`

- `MarkdigContentParser : IContentParser` — `SupportedFormats` は `[ContentFormat.Markdown]`
- `MarkdownOptions` → `MarkdownPipelineBuilder` の写像（`MarkdownFlavor` / `Tables` / `Footnotes` …）
- `YamlFrontMatterExtension` でフロントマターを切り出し、YamlDotNet で `MetadataValue` に写す
- `UseMarkdig()` 拡張メソッド

設計上の制約:

- **`ParseMetadataAsync` は文書先頭のフロントマターだけを読む。** `ParseAsync(...).Document.Metadata` と
  必ず一致しなければならない（契約テストで検証する）。ここを本文パースに委譲すると
  「1ページ表示するために全記事を完全パースする」ことになり、オンデマンド ビルドの利点が消える
- **`title` / `date` などのネイティブ名を `WellKnownMetadata` のキーに写すのはパーサーの責務。**
  元の名前も保持したまま追加で生やす（情報を失わない）。写した項目には
  `MetadataProvenance.Mapped(写し元のキー)` を、フロントマターに直接書かれた項目には
  `MetadataProvenance.Declared(位置)` を付ける
- **YamlDotNet への依存はこのパッケージだけ。** `Lithify.Abstractions` に漏らさない

### 10.2 `Lithify.Parsers.AdocNet`

- `AdocNetContentParser : IContentParser` — `SupportedFormats` は `[ContentFormat.AsciiDoc]`
- `AsciiDocOptions` の写像、document attributes（`:name: value` / `:name!:`）→ `MetadataValue`
- AdocNet AST → 共通 AST の写像
- `UseAdocNet()` 拡張メソッド

設計上の制約:

- **`doctitle` → `WellKnownMetadata.Title`、`revdate` → `WellKnownMetadata.Date` の写像はここで行う。**
  `Lithify.Blog` 側で形式ごとに分岐させると Blog が AsciiDoc の語彙を知ることになり R4 が崩れる
- `:!toc:` 形式は `MetadataValue.Flag(false)` になる。YAML は不要
- `doctitle` は本文のレベル 0 見出しから来るので、出所は `MetadataProvenance.Derived(見出しの位置)`。
  `revdate` → `date` は `Mapped`。`Declared` と `Derived` の区別があると
  「題名を直すには見出しを直す」ことが診断で示せる
- **Asciidoctor の `SafeMode` は `AsciiDoc.Abstractions` に入れない。** 必要なら
  このパッケージの engine-specific オプションに置く。そもそも safe mode は Asciidoctor という
  実装の機能であって AsciiDoc 仕様ではないので、対応物を用意する義務はない（9.3 参照）
- **AdocNet 自身の URI 読み取り機能は無効に固定する**（Asciidoctor の `allow-uri-read` に相当するもの）。
  リモート ソースの取得は Lithify が `IContentSourceProvider` で担い、AdocNet には
  **取得済みの内容だけを渡す**。エンジンが自分で通信すると増分グラフへの依存記録も
  `SourceStability` の分類も飛ぶので、include 先の変更が再ビルドを誘発せず、
  `ReproducibilityMode` も効かなくなる。
  `include::https://…[]` は Lithify が解決して内容を供給する経路に一本化する
- **include の解決は `IContentResolver` に委ねる。** AdocNet に独自のファイル読み取りをさせない。
  ステップ 9 で `ContentPath` がリモートを表せるようになるので、
  エンジン側から見れば「ローカルかリモートか」は区別が付かず、区別する必要もない

### 10.3 `Lithify.Renderers.Html`

- `HtmlDocumentRenderer : IDocumentRenderer` — `OutputMediaType` は `"text/html"`
- `HtmlRenderOptions`（見出し ID の生成規則、脚注の配置など）
- `UseHtmlRenderer()` 拡張メソッド

設計上の制約:

- **`ISyntaxHighlighter` は抽象経由でのみ使う。** `Lithify.Highlighting.TextMate` を参照してはならない。
  検証は `packages.lock.json` に `TextMateSharp` が現れないことで行う
- エスケープは `System.Text.Encodings.Web.HtmlEncoder`（Fluid が要求する `TextEncoder` と同じ型なので、
  レンダラーとテンプレートでエスケープ規則を揃えられる）
- 書き込み先は `TextWriter`。UTF-8 への変換はフラグメント生成時の `Utf8BufferTextWriter` が一度だけ行う

### 10.4 `Lithify.Templates.HandlebarsNet` / `.Fluid` / `.Blazor`

いずれも `ITemplateEngine` と `ICompiledTemplate` を実装する。

| パッケージ | コンパイル結果 | `Fingerprint` の作り方 |
|---|---|---|
| `HandlebarsNet` | `HandlebarsTemplate<TextWriter, object, object>` | テンプレート本体 + partial 群の合成 |
| `Fluid` | `IFluidTemplate` | 同上 |
| `Blazor` | 型として解決したコンポーネント | **アセンブリの MVID** |

設計上の制約:

- **`ICompiledTemplate.Fingerprint` は partial を含めた合成でなければならない。** そうしないと
  `_sidebar.hbs` の変更が伝播せず、テンプレートを直しても再レンダリングされない。
  **partial の解決は `IContentResolver` 経由に一本化する**（エンジン自身に開かせない）。
  そうすればリモートの partial もローカルと同じ経路を通り、`SourceStability` の分類と
  依存記録が自動的に効く（ステップ 9.5.4 / 9.6 参照）
- **partial は Lithify が先に解決してエンジンに渡す（push）。エンジンに解決させない（pull）。**
  `IContentResolver.OpenAsync` が async であるのに対し、**エンジン側の解決口は同期である**
  （Fluid の `TemplateOptions.FileProvider` は `IFileProvider`）。
  コールバックの中で `OpenAsync` を待つと同期待ちになり、リモート partial では現実的でない。
  Handlebars.Net は `RegisterTemplate` による事前登録なので元から push である。
  したがって `CompileAsync` の中で**参照される partial を先に閉包まで解決し**、
  解決済みの集合をエンジンに与える形に揃える。これは依存記録のためにも必要で
  （何を読んだかを知らないとフィンガープリントに畳み込めない）、
  `Fingerprint` が partial の合成であるという上の要求と同じことを別の側から言っている
- **Blazor には実行時コンパイルが存在しない。** Razor コンパイラがビルド時に IL へ変換するので、
  `CompileAsync` は `TemplateSource` を型名として解決するだけになり、フィンガープリントは
  テンプレート ソースの内容ハッシュではなくアセンブリの MVID から作る。結果として
  「テンプレートを直したら再ビルドが必要」になる。これは Blazor を選ぶことの本質的な帰結なので
  抽象を歪めず [architecture.md](architecture.md) に制約として明記する
- **テンプレートの置き場所を「`SourceRoot` 配下」に固定できない。未決の課題として残す。**
  当初「テンプレートは `SourceRoot` 配下に置く」と書いたが、**それはルール化できない。**
  最も明確な反例は Blazor で、テンプレートは**アセンブリ内の型**である
  （`TemplateSource.FromTypeName` は `ContentPath` を受け取らない）。
  規約として書いた時点で既に成立していない。
  テーマを別リポジトリで共有する構成も同様に正当である。
- **`ContentFormat` にテンプレート言語（`handlebars` 等）を入れてはならない。**
  軸が違う。`ContentFormat` は「どのパーサーで文書として解析するか」を引く鍵であり、
  テンプレート言語は「どのエンジンでテンプレートとして実行するか」である。
  前者はパースされて AST になり出力ページになるもの、後者は AST を受け取って出力を作る側で、
  `.hbs` は前者ではない。エンジンの選択は `ITemplateEngine.Name` が既に担っている。
  入れると `IContentFormatRegistry.TryGetParser` が「`.hbs` を扱えるパーサーが無い」と
  言うことになるが、それはそもそも問うべきでない質問である
- **`_templates/` が `SourceRoot` 配下にある場合、コンテンツの列挙から除く規則が要る。**
  ページとして出力されてはならない。**`ContentPath` で名付けられること
  （`IContentResolver` が開けること）と、列挙対象のコンテンツであることは別の性質である。**
  `static/` も同じ問題を持つが、そちらは `StaticFilePatterns` があるので既に区別されている。
  **規則の形（規約による除外か、明示的な設定か）は 10.4 の判断事項とする**

#### 10.4.1 テンプレートの置き場所（未決）

上記のとおり「`SourceRoot` 配下」に固定できない。**判断を保留し、決めるための材料を残す。**

**Hugo の解を検討したが、そのままは採れない。** Hugo は Go モジュールをそのまま使い
（`go.mod` / `go.sum`、`cacheDir/modules` に永続キャッシュ、`hugo mod vendor` で
`_vendor/` に取り込んで commit）、**mounts で複数モジュールの `layouts/` を
1 つに合成する**。後者は 9.3 で「仮想ファイル システムを作らない」として却下したものそのものである。
却下の理由（`FileSystemWatcher` が写像を知らない、診断のパスが二重になる、
`OutputPath` への写像が壊れる）はテンプレートでも大部分がそのまま当てはまるが、
**コンテンツより弱くなる点が 1 つある**。テンプレートは `OutputPath` に写らないので、
「写像先が 2 箇所から参照されて重複出力になる」は起きない。
つまり**コンテンツで却下した理由がテンプレートでも同じ強さで成り立つとは限らない。**
ここを検討せずにコンテンツの結論を流用してはならない。

一方、**`go.sum` が 9.5.3 で「設計に欠けている」と書いたものの実例である。**
期待するハッシュを利用者側のリポジトリに記録し、内容が変われば失敗する。
前例があるので、あれは思弁ではなく既知の解である。

決めるべきことを分解しておく。

| 問い | 備考 |
|---|---|
| テンプレートを `ContentPath` で名付けるか | 名付けないなら `IContentResolver` 一本化は partial だけの話になる。Blazor は元から名付けられない |
| 複数の供給元を合成するか（Hugo の mounts 相当） | 却下の理由の強さがコンテンツと違う。上記参照 |
| 供給元をどう取得するか | git submodule で配下に持ち込む（Lithify は何もしない）で足りるか |

**NuGet パッケージでの配布は構想に無いので、検討の対象に含めない。**

#### 10.4.2 テンプレートの部分的なカスタマイズ

**テーマ全体を fork せずに一部だけ差し替えられることは要件である。** サイドバーだけ直したい、
記事のフッターに 1 行足したいという要求で、テーマ全体を複製させてはならない
（複製した時点でテーマの更新を取り込めなくなる）。

**これは仮想ファイル システムを要しない。** 9.3 で却下した機構を持ち込まずに実現できる。
テンプレートはコンテンツと違って**名前で参照される**からである。

| | コンテンツ | テンプレート |
|---|---|---|
| どう参照されるか | ファイル システムを**列挙**して発見される | `layout: post` のように**名前で要求**される |
| 合成に何が要るか | パスの写像（＝仮想ファイル システム） | **名前の解決順序**だけ |

`TemplateSource.Name` が既にこの鍵を持っている（`Path` とは別に `Name` があるのは、
テンプレートが名前で参照される単位だからである）。したがって必要なのは
**同じ名前を複数の供給元が主張したときの優先順位**であって、パスの写像ではない。
`IContentFormatRegistry.TryGetParser` が「後から登録されたものが勝つ」としているのと
同じ形の問題であり、同じ形で解ける。

設計上の要求:

- **優先順位は明示的に決まらなければならない。** 「後から登録されたものが勝つ」を採るなら、
  サイト固有のテンプレートをテーマより後に登録する。上書きが起きたことは
  `DiagnosticSeverity.Information` で記録する（暗黙に無視されない。これも
  `TryGetParser` と同じ扱いに揃える）
- **partial の解決も同じ順序に従わなければならない。** `layout.hbs` はテーマのものを使い
  `_sidebar.hbs` だけ差し替える、が典型的な要求である。
  テンプレート本体と partial で解決規則が違うと、この要求が表現できない
- **上書きしたテンプレートの `Fingerprint` は、実際に解決された集合から作る。**
  テーマ側の `_sidebar.hbs` を畳み込んではならない（使っていないものを依存にすると
  テーマの更新で無用な再ビルドが走る）。逆に**差し替えを止めたときに
  テーマ側に戻ることも検出できなければならない**ので、
  「サイト側に `_sidebar.hbs` が無い」という事実自体が依存である。
  これは増分計算グラフにとって**ファイルの不在への依存**である。
  **現状の設計で扱える。** `IComputeContext.GetAsync` は「ノードの値」を取るだけなので、
  不在を*値*として表すノードがあれば足りる。`ContentSourceResult` の `Missing` は
  既に「参照先が存在しない」を例外ではなく決定的な結果として持っており（9.2）、
  そのまま使える。**ただし `Missing` と `Unavailable` を潰さないことがここでも要る**
  （ネットワーク断を「差し替えが無い」と読むとテーマ側に静かに戻る）
- **エンジンを跨いだ上書きは許さない。** テーマが Handlebars なら差し替えも Handlebars である。
  `_sidebar.liquid` で Handlebars の partial を置き換えることはできない
  （エンジンが partial を自分の構文で解釈するため）

**この機構は 10.4.1 の「置き場所」を決めなくても設計できる。** 名前の解決順序は
供給元がどこにあるか（`SourceRoot` 配下か、別リポジトリか、アセンブリ内か）に依存しない。
むしろ**先にこちらを決めるほうがよい**。置き場所は「供給元をどう並べるか」の問題に還元され、
Blazor（アセンブリ内の型）も同じ枠組みに収まる。

### 10.5 `Lithify.Blog`

`Post` / `Permalink` / `Collection` / タグ一覧 / 月別アーカイブ / ページネーション / Feed を1パッケージに持つ。
いずれも「投稿コレクション」という同じ概念に依存しており、分割しても実質常に一緒に使われる。
将来分割が必要になったら名前空間（`Lithify.Blog.Archive` 等）を保ったまま切り出せる。

- `AddBlog(blog => blog.Content(...).Permalink(...).WithTags().WithMonthlyArchive().WithFeed(...))`
- `AddStaticFiles("static/**")`
- `FeedFormat`（`Atom` / `JsonFeed` の `[Flags]`）
- サイドバー フラグメント（`sidebar-tags` / `sidebar-archive`）を `IComputeNode<RenderedFragment>` として定義

設計上の制約:

- **サイドバー フラグメントは `Site.Tags` / `Site.Archive` にのみ依存する。** 記事ソースに直接依存させると
  記事を1本足すたびに全ページの本文が無効化され、R8 の目的が失われる
- **出力パスの衝突は診断エラーにして中止する。** `posts/hello.md` と `posts/hello.adoc` はどちらも `/hello/` に
  写る。last-writer-wins にすると入力列挙順で結果が変わり、増分ビルドの決定性も壊れる。
  どの2ファイルが衝突したかを `Diagnostic` で示す
- **順序は常に安定させる。** 不安定な順序はフィンガープリントを変え、R7（内容が同じなら書かない）を壊す
- `MetadataValue` の解釈（`tags` が `Sequence` か単一の `Scalar` か等）はここが担う。
  `Lithify.Abstractions` は `WellKnownMetadata` のキー定義までしか持たない
- **ディレクトリごとの既定メタデータの層構成はここが担う。** Hugo の `cascade` / Jekyll の
  `defaults.scope.path` / Eleventy のディレクトリ データ ファイルに相当する機能。
  `DocumentMetadata.WithFallback` を外側から内側へ繰り返すだけで表せるので抽象側に層の概念は要らない
  （`WithFallback` は層の数を知らない）。**各層は自分の `MetadataProvenance.FromDefaults(層のパス)` を
  stamp してから重ねる。** そうしないと合成後に「どの層の既定が効いているか」が失われ、
  利用者が値の出どころを追えなくなる。サイト全体の既定値はパスが `default` のルート層として扱う
- **既定値の層は増分ビルドの依存として扱う。** `posts/` の既定値の変更は `posts/` 配下の全文書を
  無効化するが、他は無効化しない。層を1つのノードにまとめると1文書の既定値の変更で全ページが落ちる

### 10.6 パーサーのディスパッチ

`IContentFormatRegistry` の既定実装（`.md` / `.markdown` → markdown、`.adoc` / `.asciidoc` → asciidoc）を
`Lithify.Core` または `Lithify.Hosting` に置く。

- **1つの形式を複数のパーサーが主張しうる**（`SupportedFormats` が複数持てるため）。
  **後から登録されたものが勝つ**が、上書きが起きたことを情報レベルで記録する。暗黙に無視しない

## 11. `Lithify.Highlighting.TextMate`

- `TextMateSyntaxHighlighter : ISyntaxHighlighter`（`TextMateSharp` 2.0.4）
- `TextMateOptions` — 文法・テーマ ファイルの配置
- `UseTextMateHighlighting()` 拡張メソッド

設計上の制約:

- **`ISyntaxHighlighter : IFingerprintable` であり、文法ファイル自体をフィンガープリントに含める。**
  そうしないとテーマや文法の更新が下流に伝わらず、古いハイライト結果が使われ続ける
- **ハイライトは独立した計算ノードにする。** `(code, language, highlighterFingerprint)` だけで決まる純粋関数で
  かつ重いので、メモ化すればビルドを跨いで、さらに同じスニペットを載せる複数ページ間でも共有される
  （毎回実行する Hugo の Chroma に対する優位点）
- `CanHighlight` が false の言語は `PassThroughSyntaxHighlighter` にフォールバックする
- `Onigwrap`（oniguruma のネイティブ バインディング）に依存するので RID 固有アセットが増える。
  NativeAOT を採らない判断と整合するので許容する

## 12. `Lithify.Serve`

- `IChangeSource` / `ContentChange` / `ChangeKind`
- `FileSystemChangeSource`（`FileSystemWatcher` ラッパー、`Microsoft.Extensions.FileProviders.Physical`）
- `ServeOptions`（`Port` / `LiveReload` / `OnDemand` / `PrebuildInBackground`）
- SSE エンドポイントと live-reload クライアント スクリプト
- `AddDevelopmentServer()` 拡張メソッドと、その中で登録する `ServeCommandProvider : ILithifyCommandProvider`

設計上の制約 — **バックグラウンド ビルドが計算グラフに課す3要求は骨格段階で契約に織り込む**（後から足すと全ノードの実装に影響する）:

1. **ノード単位の single-flight** — 同じノードを2スレッドが同時に要求したら片方が待つ
2. **リビジョン境界での中断** — 背景ビルド中にソースが変わったら進行中の評価は捨てる。
   古いリビジョンの結果をキャッシュしてはならない（`IComputeContext` がリビジョンを持つのはこのため）
3. **前景要求の優先** — HTTP リクエストが来たら背景ビルドを譲る。背景ビルドは単一の低優先度ワーカーに限定して単純化する

その他:

- **リクエスト時ビルドは `build` と同じ経路を通す。** HTTP リクエストのパスを `OutputPath` に写し、
  `PageComposition` を要求して `InMemoryOutputStore` に書き、`OpenReadAsync` でレスポンスに流す。
  `IOutputStore` を迂回すると静的ファイル・Feed・ページで扱いが分岐する
- **live-reload の変更検出は R7 の判定をそのまま使う。** `WriteOutcome != Unchanged` の集合が
  「実際に内容が変わった出力パス」なので専用の仕組みは要らない。全ページ リロードを撒かないのが要点
- **シンボリック リンクの先の変更は検出できない。** `FileSystemWatcher` は監視ツリー内部の
  リンクを辿らない（全プラットフォーム共通の意図された挙動。ステップ 9.3 参照）。
  **検出も通知もせず、ドキュメントの既知の制約に留める。** リンク先を追加監視する手は
  採らない（通知を元のパスに逆写像する層が必要になる）。
  なお `SourceRoot` 自身がリンクである場合は正常に監視できるので、これは対象外
- `PipeWriter` が正当に登場するのはここのレスポンス書き込み（`HttpResponse.BodyWriter`）だけ
- `serve` コマンドは `AddDevelopmentServer()` が呼ばれている場合のみ現れる。
  `Lithify.Hosting` はサブコマンドの一覧を持たない

## 13. `Lithify.Testing`

記録機構自体は**実装する**（デコレーターなので実装が空でも動く部分が多い）。

- `RecordingOutputStore(IOutputStore inner)` — `WriteCount(OutputPath)` / `History`
- `RecordingComputeContext(IComputeContext inner)` — `EvaluationCount(NodeId)` / `EvaluationOrder`
- `InMemoryContentSourceProvider` — `Add` / `Remove`
- `ManualChangeSource : IChangeSource` — `Raise(ContentPath, ChangeKind)`

設計上の制約:

- **`RecordingComputeContext` が最も重要。** early cutoff の検証は「何が再計算され**なかった**か」の確認なので、
  出力だけ見てもキャッシュヒットしたのか偶然同じ結果になったのか区別できない
- **テスト フレームワークに依存させない。** MSTest に依存する契約テスト基底クラスは `Tests/` 側に置く
  （検証は `packages.lock.json` に `Microsoft.Testing.*` が現れないことで行う）
- `InMemoryOutputStore` はここではなく `Lithify.Core` にある（`serve` の正規の実装なのでテスト用フェイクではない）
- `ManualChangeSource` は `IChangeSource` が `Lithify.Serve` にあるため、
  そこへの参照が必要になる。`Lithify.Testing` の依存が増えるのを避けたい場合は
  `IChangeSource` を `Lithify.Abstractions` に移すことを検討する（**未決**）

## 14. `ProjectTemplates/content/Lithify.Blog/`

```
.template.config/template.json     shortName: "lithify-blog"
Blog.csproj
Program.cs
posts/2026-01-01-hello-world.md
_templates/layout.hbs / post.hbs / _sidebar.hbs
static/style.css
```

パラメーター:

- `--template-engine handlebars|liquid|blazor`
- `--content-format markdown|asciidoc|both`（既定 `markdown`）

**パラメーター値は利用者視点の形式名／言語名にする**（利用者は「Liquid を使いたい」「AsciiDoc で書きたい」と考える）。
テンプレート内の `#if` が `UseFluid()` / `UseAdocNet()` 等の実装名 API に写す。
パッケージ名が実装名であることと矛盾しない。

## 15. `Samples/Blog/`

現状は `Program.cs` のみ（`UseLithify()` を呼ぶだけで、プラグイン登録はまだ書けない）。
テンプレートと同内容にし、**Markdown と AsciiDoc の記事を各1本**置いて AST 写像を実際に通す。

- `posts/*.md` と `posts/*.adoc`
- `_templates/`
- `static/`
- ステップ 10 が済んだら `Program.cs` に `UseMarkdig()` 以下を追加する

`Samples/Directory.Build.props` が `ArtifactsProjectName` を `Sample.$(MSBuildProjectName)` にしているので、
`Sources/Blog` と出力先が衝突しない。

## 16. `Tests/`

現状は 7 プロジェクトが `MSTestSettings.cs` のみで、テストが1件も無い。

### 16.1 契約テストの枠組み

`IContentParser` 実装が満たすべき性質を検証する抽象基底クラスを `Tests/` 側に置き、
Markdig / AdocNet の両テスト プロジェクトが継承する。**両実装が同じ基底クラスを継承して
コンパイルが通ること自体が共通 AST の設計検証になる。**

検証する性質:

- `ParseMetadataAsync` と `ParseAsync(...).Document.Metadata` が一致すること
- 両形式の等価な文書から同一の `WellKnownMetadata` が読めること
- 同じ入力を2回パースして同じフィンガープリントになること（決定性）

### 16.2 実際に通るテスト

骨格段階でも検証可能なものは検証する。

- `MetadataKey` の正規化（小文字化・`_` → `-`。`page_title` と `:page-title:` の同一視）
- `DocumentMetadata` の出所（`Origins` が疎であること。`SetItem` で値を差し替えると古い出所が落ちること。
  `WithFallback` で下敷きの出所が上書き側の値に引き継がれ**ない**こと）
- `Fingerprint.Combine`（順序依存であること、空の場合の扱い）
- `OutputDecision.Decide`（`Created` / `Updated` / `Unchanged` の3分岐）
- `InMemoryOutputStore`
- `Utf8BufferTextWriter`（char → UTF-8 の境界。サロゲート ペアが書き込み境界に跨る場合）
- **260 文字を超える出力パス**（Windows のみ。`OutputPath` → 実パスの変換が
  完全修飾パスを渡していることの検証。ステップ 9.6 参照）。
  CI では `LongPathsEnabled` が無効な環境でも通ることを確認したいが、
  ホストされたランナーの設定を変えられるかは未確認

### 16.3 テストの実行

テスト プロジェクトは全て MSTest.Sdk（Microsoft.Testing.Platform）。
**フィルタは `--` の後に渡す**こと。付けないとフィルタがランナーに渡らず 0 件マッチで静かに終わる。

```console
$ dotnet test Lithify.slnx --framework net10.0
$ dotnet test Tests/Core/Core.Tests.csproj --framework net10.0 -- --filter "FullyQualifiedName~Fingerprint"
```

## 17. `docs/`

### 17.1 `docs/architecture.md`

**未作成だが、[README.md](../README.md) と [.claude/CLAUDE.md](../.claude/CLAUDE.md) の両方が既に参照している。**
`.claude/CLAUDE.md` は「変更する際は読むこと」と指示しているので、優先度が高い。

書くべき内容:

- 増分計算グラフの再検証アルゴリズム（擬似コード）
  1. `Verified == CurrentRevision` なら即返す
  2. 依存を再帰的に検証。**すべての依存のフィンガープリントが変わっていなければ**再計算せず
     `Verified` だけ更新（= early cutoff）
  3. いずれかが変わったら再計算。**新しい出力フィンガープリントが前回と同じなら** `ChangedAt` を据え置く
     → 下流も再計算されない
- フラグメント合成の擬似コード（R8。新記事追加時に `sidebar-*` のみ再計算され、本文はキャッシュヒットする経路）
- **「形式仕様の語彙か、エンジンの都合か」の判断基準。** Asciidoctor の safe mode を
  `AsciiDoc.Abstractions` に含めなかった理由を例として記録する
- **「出力ディレクトリは生成物であり編集対象ではない」の明記。** 既存フィンガープリントはビルド キャッシュの
  記録から取り、実ファイルは読まない。手編集を検知しようとしても mtime とサイズでは中身の差し替えを
  見逃すので中途半端で、それでいて全出力のハッシュ再計算は live-reload の応答時間と正面衝突する。
  逃げ道は `--force` だけ
- バックグラウンド ビルドが計算グラフに課す3要求（ステップ 12 参照）
- Blazor に実行時コンパイルが無いことの帰結（ステップ 10.4 参照）
- 依存の向きの図と、`Lithify.Abstractions` が具体的なエンジンに依存しないという不変条件
- **`Fingerprint`（内容の同一性）と `SourceValidator`（再取得の必要性）が別概念である理由。**
  ETag を `Fingerprint` に代入すると R7 と early cutoff が壊れることを、
  「同じバイト列に別の ETag が付く」具体例（nginx の inode 由来 ETag、ミラー切り替え）で示す
- **ローカル ファイルがリモート ソースの特殊例であるという見方。** mtime + サイズが検証子、
  鮮度期間が 0。この一般化が `IContentSourceProvider` を 1 つの抽象で足りるようにしている
- **「決定性」を 2 つに分けて書く。** 同じ語で 2 つの別の性質を指すと、
  リモート ソースが何を壊すのかが説明できなくなる

  | 性質 | 内容 | リモートで |
  |---|---|---|
  | 同一入力 → 同一出力（再現性） | 同じ入力集合から常に同じ出力。フィンガープリントが安定すること | **常に保たれる** |
  | 同一ソース ツリー → 同一出力（再現可能ビルド） | git の内容が同じなら誰がいつビルドしても同じ出力 | **アドレスによる** |

  増分ビルドの正しさ（early cutoff、R7、順序の安定）が依存しているのは前者だけである。
  リモート内容は取得された時点で入力の一部になるので前者は壊れない。
  **したがって「順序を安定させる」等の既存の記述はすべて前者を指す**ことを明記する
- **再現可能ビルドを分けるのは local/remote ではなく、アドレスが一意な内容を指すかである。**
  commit SHA 付きの git 参照は再現可能であり、ルート外を指すシンボリック リンク経由の
  ローカル参照は再現可能でない。「ローカルだから安全」という前提を置かないこと。
  分類（`SourceStability`）と `ReproducibilityMode` の設計はステップ 9.5.1 / 9.5.2 参照
- **`LinkTarget.External`（出力に書き出すリンク）と `Remote` の `ContentPath`（取得してビルド入力にする内容）の区別**（ステップ 9.5 参照）

### 17.2 `docs/setup.md`

**未作成。[README.md](../README.md) と [.gitconfig](../.gitconfig) の両方が参照している。**

- `git config --local include.path ../.gitconfig` を実行して設定ベース フックを有効にする手順
- devcontainer の使い方（base イメージ + dotnet feature の構成）
- `setup.ps1` は**作らない**（Git 2.55 の設定ベース フックを使うので不要）

## 検証

骨格段階なので「動く機能」ではなく**ビルドと規約の健全性**を検証する。

```console
$ dotnet restore Lithify.slnx
$ dotnet build Lithify.slnx --no-logo -bl:build.binlog
$ dotnet test Lithify.slnx --framework net10.0
$ dotnet pack Lithify.slnx --configuration Release -p:Version=0.1.0-alpha
$ dotnet new install ./artifacts/package/release/Lithify.ProjectTemplates.0.1.0-alpha.nupkg
$ dotnet new lithify-blog -n TestBlog -o /tmp/TestBlog
$ dotnet build /tmp/TestBlog
$ dotnet new uninstall Lithify.ProjectTemplates
$ dotnet run --project Samples/Blog -- --help
```

`dotnet pack` の結果として以下 **17 個**の `.nupkg` が出ること:
`Lithify.Abstractions` / `Core` / `Hosting` / `Markdown.Abstractions` / `Parsers.Markdig` /
`AsciiDoc.Abstractions` / `Parsers.AdocNet` / `Renderers.Html` / `Highlighting.TextMate` /
`Templates.HandlebarsNet` / `Templates.Fluid` / `Templates.Blazor` / `Blog` / `Serve` / `Sources.Http` /
`Testing` / `Lithify.ProjectTemplates`

`Sources.Http` はステップ 9 で追加されるプロジェクトなので、`Lithify.slnx` への登録も必要。

ビルドが失敗した場合は `binlog_errors` / `binlog_warnings` MCP ツールで `build.binlog` を解析する。

### 設計の健全性チェック

実装が空でも**依存の向きと型の表現力**は検証できる。骨格段階の検証はここに集中させる。

| 確認すること | 何の証拠になるか |
|---|---|
| `Renderers.Html` の `packages.lock.json` に `TextMateSharp` が現れない | ハイライターを抽象経由でのみ使っている |
| `Abstractions` の `packages.lock.json` に `YamlDotNet` / `Markdig` / `AdocNet.*` が現れない | 抽象が具体的なエンジンに依存していない |
| `Core` に `Microsoft.AspNetCore.App` の `FrameworkReference` が無い | `InMemoryOutputStore` を Core に置いても `serve` 専用の依存が中核に漏れていない |
| `Testing` に MSTest / `Microsoft.Testing.*` への参照が無い | テスト フレームワーク非依存 |
| `Tests/Parsers/Markdig` と `Tests/Parsers/AdocNet` が同じ契約テスト基底クラスを継承してコンパイルが通る | 共通 AST が両形式を表現できている |
| `RecordingOutputStore` が `InMemoryOutputStore` と `FileSystemOutputStore` の両方に被せられる | デコレーターとして正しく設計できている |
| `Abstractions` と `Core` の `packages.lock.json` に `System.Net.Http.*` が現れない | リモート取得が実装パッケージ側に閉じている |
| `Abstractions` の公開 API に `Etag` / `LastModified` / `StatusCode` / `MediaType` を含む識別子が無い | 検証子が不透明トークンに保たれ、HTTP を特別扱いしていない |
| `FileSystemContentSourceProvider` と `HttpContentSourceProvider` が同じ契約テスト基底クラスを継承してコンパイルが通る | ローカルがリモートの特殊例として表現できている |

## 破ってはならない不変条件

[.claude/CLAUDE.md](../.claude/CLAUDE.md) にも記載があるが、実装が進むと壊れやすいので再掲する。

- **`Lithify.Abstractions` は具体的なエンジンに依存しない。** Markdig / AdocNet / YamlDotNet /
  TextMateSharp / テンプレート エンジンへの参照を追加してはならない
- **`Lithify.Renderers.Html` は `ISyntaxHighlighter` を抽象経由でのみ使う。**
  `Lithify.Highlighting.TextMate` を参照してはならない
- **char → UTF-8 の境界はフラグメント生成時の1回だけ。** 生成側は `TextWriter`、
  キャッシュ・合成・出力側は UTF-8 バイト列。`RenderedFragment` に `string` を持たせると
  全ページ合成のたびに再エンコードが走り、フラグメント合成の意義（R8）が失われる
- **出力の判断と I/O は分離する。** 「内容が同じなら書かない」判断は純粋関数 `Decide` に置き、
  `IOutputStore` は I/O 境界に留める
- **`Lithify.Testing` はテスト フレームワークに依存しない。** MSTest に依存する契約テスト基底クラスは
  `Tests/` 側に置く
- **`Fingerprint` は内容バイト列のハッシュに限る。** ETag・mtime・`MDTM` のような
  「変わったかもしれない」を示す検証子を `Fingerprint` に代入してはならない。
  同じ内容に異なる値が付いた瞬間に R7 と early cutoff が壊れる
- **プロジェクトのルートは単一である。** 追加ルートも、接頭辞を外部ディレクトリに写す
  仮想ファイル システムも作らない。どちらも `ContentPath` の一意性を壊し、
  `FileSystemWatcher` の逆写像・診断パスの二重化・出力先の重複を招く一方、
  git submodule で代替できる（ステップ 9.3 参照）
- **セキュリティのための設定項目は 1 つも設けない。** ファイルへのアクセス制限も
  リモート取得先の許可リストも置かない。前者は読み取り範囲を OS のアクセス権とコンテナで
  制限できるうえパス検査では防ぎきれず、後者は**設定がコンテンツと同じリポジトリにあるなら
  一緒に書き換えられる**ので防いでいるふりになる（ステップ 9.3 参照）。
  代わりに**利用者向けドキュメントで推奨事項として案内する**（信頼できない相手からの PR を
  ビルドしない、通信先はランナー側で制限する、`Pinned` なアドレスを使う）。
  能力そのものはパッケージ参照で制御される（`Lithify.Sources.Http` を参照しなければ
  リモート取得の実装が存在しない）ので、`bool` の設定項目より強い形で既定拒否になっている
- **再現性の問題とセキュリティの問題を混同しない。** ルート外を指すシンボリック リンクは
  再現性の問題としては常に成立するが（clone に実体が入らない）、セキュリティの問題としては
  コンテンツの作者とビルドの実行者が違うときにしか成立しない。前者は
  `SourceStability.Unpinned` として扱い、**後者は対象外とする**（読み取り範囲は
  OS とコンテナで制限でき、パス検査では防ぎきれない）。
  半端な検査を入れて「防げているつもり」にさせない
- **リモート ソースの取得は Lithify が担い、エンジンには通信させない。** パーサーや
  テンプレート エンジンの URI 読み取り機能は無効に固定する。エンジンが直接取ると
  増分グラフへの依存記録と `SourceStability` の分類の両方を迂回するので、
  変更が伝播しないうえ `ReproducibilityMode` も効かなくなる。
  テンプレートの partial をリモートから解決する場合も同じ（ステップ 9.5.4 参照）
- **ファイル I/O は Lithify が担い、依存ライブラリにファイルを開かせない。** パーサーには
  読み込んだ文字列を渡し、include もテンプレートの partial 解決も Lithify 側で行う。
  これは上記の理由（ポリシーと依存記録の迂回）で既に要求されることだが、
  **長いパスの扱いも同じ境界に集まる**という副次的な利点がある（ステップ 9.6）
- **相対パスの基準はカレント ディレクトリではない。** `include::../x[]` の `..` は
  それを書いた文書の位置を基準に解決する。`ContentPath` から実パスへの変換は 1 箇所に集約し、
  `Path.GetFullPath(relative, basePath)` で完全修飾パスにしてから I/O に渡す。
  カレント ディレクトリはプロセス全体で共有された可変状態でもあるので、参照してはならない
- **`Lithify.Abstractions` と `Lithify.Core` は HTTP を知らない。** 検証子は不透明トークンであり、
  抽象に ETag / 304 / 状態コードを出してはならない。スキームを特別扱いする根拠が中核側には無い
