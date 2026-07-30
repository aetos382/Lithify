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
| 5 | `Lithify.Abstractions` — 共通 AST、`DocumentMetadata`、`FileAccessPolicy`、パーサー／レンダラー／テンプレート／ハイライターの契約 |
| 6 | `Lithify.Core` — 増分計算グラフ、フラグメント合成、`IOutputStore`、`IBuildCache`、`Utf8BufferTextWriter` |
| 7 | 形式抽象層 — `Lithify.Markdown.Abstractions` / `Lithify.AsciiDoc.Abstractions` |
| 8 | `Lithify.Hosting` — `UseLithify()` / `ILithifyBuilder` / `RunLithifyAsync()` / `build` / `clean` |

現時点で `Sources/Blog`、`Sources/Renderers/Html`、`Sources/Templates/*`、`Sources/Highlighting/TextMate`、
`Sources/Parsers/*`、`Sources/Serve`、`Sources/Testing` は **csproj のみで `.cs` ファイルがゼロ**。
参照関係とパッケージ メタデータだけが確定している状態。

## 9. プラグイン パッケージ

各パッケージが `ILithifyBuilder` 拡張メソッドを1つ公開し、DI に自分の実装を登録する。
`UseLithify()` はパーサーもレンダラーもテンプレート エンジンも登録しない（既定で何かを登録すると
「差し替え可能」という建前が崩れる）ので、ここで登録されるものが唯一の供給源になる。

### 9.1 `Lithify.Parsers.Markdig`

- `MarkdigContentParser : IContentParser` — `SupportedFormats` は `[ContentFormat.Markdown]`
- `MarkdownOptions` → `MarkdownPipelineBuilder` の写像（`MarkdownFlavor` / `Tables` / `Footnotes` …）
- `YamlFrontMatterExtension` でフロントマターを切り出し、YamlDotNet で `MetadataValue` に写す
- `UseMarkdig()` 拡張メソッド

設計上の制約:

- **`ParseMetadataAsync` は文書先頭のフロントマターだけを読む。** `ParseAsync(...).Document.Metadata` と
  必ず一致しなければならない（契約テストで検証する）。ここを本文パースに委譲すると
  「1ページ表示するために全記事を完全パースする」ことになり、オンデマンド ビルドの利点が消える
- **`title` / `date` などのネイティブ名を `WellKnownMetadata` のキーに写すのはパーサーの責務。**
  元の名前も保持したまま追加で生やす（情報を失わない）
- **YamlDotNet への依存はこのパッケージだけ。** `Lithify.Abstractions` に漏らさない

### 9.2 `Lithify.Parsers.AdocNet`

- `AdocNetContentParser : IContentParser` — `SupportedFormats` は `[ContentFormat.AsciiDoc]`
- `AsciiDocOptions` の写像、document attributes（`:name: value` / `:name!:`）→ `MetadataValue`
- AdocNet AST → 共通 AST の写像
- `UseAdocNet()` 拡張メソッド

設計上の制約:

- **`doctitle` → `WellKnownMetadata.Title`、`revdate` → `WellKnownMetadata.Date` の写像はここで行う。**
  `Lithify.Blog` 側で形式ごとに分岐させると Blog が AsciiDoc の語彙を知ることになり R4 が崩れる
- `:!toc:` 形式は `MetadataValue.Flag(false)` になる。YAML は不要
- **Asciidoctor の `SafeMode` は `AsciiDoc.Abstractions` に入れない。** 必要なら
  このパッケージの engine-specific オプションに置く。include の許可は `FileAccessPolicy.AllowIncludes` が担う

### 9.3 `Lithify.Renderers.Html`

- `HtmlDocumentRenderer : IDocumentRenderer` — `OutputMediaType` は `"text/html"`
- `HtmlRenderOptions`（見出し ID の生成規則、脚注の配置など）
- `UseHtmlRenderer()` 拡張メソッド

設計上の制約:

- **`ISyntaxHighlighter` は抽象経由でのみ使う。** `Lithify.Highlighting.TextMate` を参照してはならない。
  検証は `packages.lock.json` に `TextMateSharp` が現れないことで行う
- エスケープは `System.Text.Encodings.Web.HtmlEncoder`（Fluid が要求する `TextEncoder` と同じ型なので、
  レンダラーとテンプレートでエスケープ規則を揃えられる）
- 書き込み先は `TextWriter`。UTF-8 への変換はフラグメント生成時の `Utf8BufferTextWriter` が一度だけ行う

### 9.4 `Lithify.Templates.HandlebarsNet` / `.Fluid` / `.Blazor`

いずれも `ITemplateEngine` と `ICompiledTemplate` を実装する。

| パッケージ | コンパイル結果 | `Fingerprint` の作り方 |
|---|---|---|
| `HandlebarsNet` | `HandlebarsTemplate<TextWriter, object, object>` | テンプレート本体 + partial 群の合成 |
| `Fluid` | `IFluidTemplate` | 同上 |
| `Blazor` | 型として解決したコンポーネント | **アセンブリの MVID** |

設計上の制約:

- **`ICompiledTemplate.Fingerprint` は partial を含めた合成でなければならない。** そうしないと
  `_sidebar.hbs` の変更が伝播せず、テンプレートを直しても再レンダリングされない
- **Blazor には実行時コンパイルが存在しない。** Razor コンパイラがビルド時に IL へ変換するので、
  `CompileAsync` は `TemplateSource` を型名として解決するだけになり、フィンガープリントは
  テンプレート ソースの内容ハッシュではなくアセンブリの MVID から作る。結果として
  「テンプレートを直したら再ビルドが必要」になる。これは Blazor を選ぶことの本質的な帰結なので
  抽象を歪めず [architecture.md](architecture.md) に制約として明記する
- 拡張メソッド名は実装名に合わせる: `UseHandlebarsNet()` / `UseFluid()` / `UseBlazor()`

### 9.5 `Lithify.Blog`

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

### 9.6 パーサーのディスパッチ

`IContentFormatRegistry` の既定実装（`.md` / `.markdown` → markdown、`.adoc` / `.asciidoc` → asciidoc）を
`Lithify.Core` または `Lithify.Hosting` に置く。

- **1つの形式を複数のパーサーが主張しうる**（`SupportedFormats` が複数持てるため）。
  **後から登録されたものが勝つ**が、上書きが起きたことを情報レベルで記録する。暗黙に無視しない

## 10. `Lithify.Highlighting.TextMate`

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

## 11. `Lithify.Serve`

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
- `PipeWriter` が正当に登場するのはここのレスポンス書き込み（`HttpResponse.BodyWriter`）だけ
- `serve` コマンドは `AddDevelopmentServer()` が呼ばれている場合のみ現れる。
  `Lithify.Hosting` はサブコマンドの一覧を持たない

## 12. `Lithify.Testing`

記録機構自体は**実装する**（デコレーターなので実装が空でも動く部分が多い）。

- `RecordingOutputStore(IOutputStore inner)` — `WriteCount(OutputPath)` / `History`
- `RecordingComputeContext(IComputeContext inner)` — `EvaluationCount(NodeId)` / `EvaluationOrder`
- `InMemoryContentFileResolver` — `Add` / `Remove`
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

## 13. `ProjectTemplates/content/Lithify.Blog/`

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

## 14. `Samples/Blog/`

現状は `Program.cs` のみ（`UseLithify()` を呼ぶだけで、プラグイン登録はまだ書けない）。
テンプレートと同内容にし、**Markdown と AsciiDoc の記事を各1本**置いて AST 写像を実際に通す。

- `posts/*.md` と `posts/*.adoc`
- `_templates/`
- `static/`
- ステップ 9 が済んだら `Program.cs` に `UseMarkdig()` 以下を追加する

`Samples/Directory.Build.props` が `ArtifactsProjectName` を `Sample.$(MSBuildProjectName)` にしているので、
`Sources/Blog` と出力先が衝突しない。

## 15. `Tests/`

現状は 7 プロジェクトが `MSTestSettings.cs` のみで、テストが1件も無い。

### 15.1 契約テストの枠組み

`IContentParser` 実装が満たすべき性質を検証する抽象基底クラスを `Tests/` 側に置き、
Markdig / AdocNet の両テスト プロジェクトが継承する。**両実装が同じ基底クラスを継承して
コンパイルが通ること自体が共通 AST の設計検証になる。**

検証する性質:

- `ParseMetadataAsync` と `ParseAsync(...).Document.Metadata` が一致すること
- 両形式の等価な文書から同一の `WellKnownMetadata` が読めること
- 同じ入力を2回パースして同じフィンガープリントになること（決定性）

### 15.2 実際に通るテスト

骨格段階でも検証可能なものは検証する。

- `MetadataKey` の正規化（小文字化・`_` → `-`。`page_title` と `:page-title:` の同一視）
- `Fingerprint.Combine`（順序依存であること、空の場合の扱い）
- `OutputDecision.Decide`（`Created` / `Updated` / `Unchanged` の3分岐）
- `InMemoryOutputStore`
- `Utf8BufferTextWriter`（char → UTF-8 の境界。サロゲート ペアが書き込み境界に跨る場合）

### 15.3 テストの実行

テスト プロジェクトは全て MSTest.Sdk（Microsoft.Testing.Platform）。
**フィルタは `--` の後に渡す**こと。付けないとフィルタがランナーに渡らず 0 件マッチで静かに終わる。

```console
$ dotnet test Lithify.slnx --framework net10.0
$ dotnet test Tests/Core/Core.Tests.csproj --framework net10.0 -- --filter "FullyQualifiedName~Fingerprint"
```

## 16. `docs/`

### 16.1 `docs/architecture.md`

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
- バックグラウンド ビルドが計算グラフに課す3要求（ステップ 11 参照）
- Blazor に実行時コンパイルが無いことの帰結（ステップ 9.4 参照）
- 依存の向きの図と、`Lithify.Abstractions` が具体的なエンジンに依存しないという不変条件

### 16.2 `docs/setup.md`

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

`dotnet pack` の結果として以下 **16 個**の `.nupkg` が出ること:
`Lithify.Abstractions` / `Core` / `Hosting` / `Markdown.Abstractions` / `Parsers.Markdig` /
`AsciiDoc.Abstractions` / `Parsers.AdocNet` / `Renderers.Html` / `Highlighting.TextMate` /
`Templates.HandlebarsNet` / `Templates.Fluid` / `Templates.Blazor` / `Blog` / `Serve` / `Testing` /
`Lithify.ProjectTemplates`

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
