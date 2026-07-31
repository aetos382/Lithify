# 未決事項

[roadmap.md](roadmap.md) の各節に散っている「まだ決めていないこと」の索引。
**決めた時点でここから消し、決定は roadmap の該当節に書く。** この文書に決定を書くと、
同じ話題について roadmap と 2 箇所に記述が生まれる。

各項目には**何が決まっていないか**と、**決めるために必要な材料**を書く。
「後で考える」だけのものは書かない（それは項目ではなく気分である）。

---

## 1. `WellKnownMetadata.Title` と AsciiDoc の `:title:` の衝突

**節: [10.2](roadmap.md#102-lithifyparsersadocnet)。着手前に決める必要がある。**

### 確認した仕様（[Asciidoctor: Document Title](https://docs.asciidoctor.org/asciidoc/latest/document/title/)、2026-07-31 時点）

- ヘッダーのレベル 0 セクション タイトル（`=` 1 個）が文書タイトルになる。
  「Technically, a document title is a level 0 section title (`=`)」
- それは**自動的に `doctitle` 属性に代入される。**
  「The level 0 section title in a document's header … is automatically assigned to the
  document attribute `doctitle`」。本文中で `{doctitle}` として参照できる
- `:doctitle:` をヘッダーで明示代入することもできる（属性リファレンスで **Header Only**）
- 既定では文書タイトルが HTML の `<title>` の値になる
- **`:title:` は 2 つの役を持つ。** `<head><title>` の値であり、**かつ**文書タイトルの
  最後の手段である。「Used as a fallback when the document title is not specified」
  「If neither a level 0 section title or `doctitle` is specified in the header, but `title` is,
  its value is used as a fallback document title」

したがって仕様上の文書タイトルの解決順は:

```
レベル 0 見出し  →  :doctitle:（明示代入）  →  :title:（フォールバック）
```

`doctitle` が見出しから自動的に立つので、**候補列に「レベル 0 見出し」を並べる必要はない**
（`AsciiDocAliasDefaults` に `["doctitle", "title"]` と書けば仕様の順になる）。
出所は見出し由来なら `Derived(見出しの位置)`、明示代入なら `Declared` で区別できる。

### 決まっていないこと

**AsciiDoc は「文書タイトル」と「`<head>` のタイトル」を別の概念として持つが、
Lithify には後者に対応する well-known キーが無い。** そして今のコードでは
`["doctitle", "title"]` という宣言が効かない。2 箇所で無効化される:

- `MetadataAliasTable.Set` — 写し先と同じ綴りの候補を落とす（決して選ばれない死んだ候補だから）
- `WellKnownMetadataMapper.Map` — 写し先が明示的に書かれていれば写しごとスキップする
  （書かれたものが別名に負けてはならないから）

`:title:` は正規化だけで写し先 `title` そのものに一致するので、
**`:title:` がある文書では `doctitle` が写される前に弾かれる — 仕様の逆になる。**
候補列をどう並べても効かない。

根っこは、`title` という 1 つのスロットに
「well-known な文書タイトル」と「AsciiDoc のネイティブ属性 `:title:`」の
2 つが入ろうとしていることである。どちらかが動かないと解けない。

### 案 A: well-known キーの綴りを変える（`title` → `doc-title` 等）

`title` が普通のネイティブ属性に戻るので衝突が消え、写しの規則は無傷。

代償: 世界中が `title` と書くものに別の綴りを当てる。Markdown の `title:` も別名経由になり、
出所が `Declared` から `Mapped` に変わる（「フロント マターに直接書いた」ことが読めなくなる）。

### 案 B: 写し先を自分の候補列に並べられるようにする

並べた場合はその位置で優先順に参加し、並べなければ従来どおり
「書かれた写し先が常に勝つ」。Markdown は何も書かないので挙動は変わらず、
AsciiDoc は `["doctitle", "title"]` と宣言するだけで仕様の順になる。

`title` 固有の問題ではなく「形式のネイティブな綴りが well-known の綴りと衝突する」という
一般形なので、A の都度改名（改名は消費者に波及する）より筋がよい。
衝突の解決権がパーサーの宣言として 1 箇所に出る。

**B の副作用（避けられない）:** `doctitle` が `title` に写されると、
`Entries["title"]` にあった `:title:` の生の値が上書きされる。写しは元の `metadata` から
読んで結果に書くので、後述の新キーは `:title:` を正しく拾えるが、
`title` の生の値は「元の名前も残る」という不変条件から外れる。
1 つのスロットに 2 つは入らないので避けられない。

また、この場合の `LI1003` の文面（「`{1}` はそれ自身の名前でのみ読めます」）が嘘になるので、
**写し先自身が候補として負けた場合は別の文面が要る。**

### 併せて決めること: `<head>` のタイトル用の well-known キーを足すか

足す場合の名前は **`MetaTitle`** を推す。`HtmlTitle` にはしない
（`WellKnownMetadata` は出力形式に依らない語彙であり、HTML レンダラーの都合を持ち込む場所ではない）。
概念は「表示される見出しではなく、文書のメタ情報としてのタイトル」である。

```
Title     = ["doctitle", "title"]   // 見出し → :doctitle: → :title:
MetaTitle = ["title", "doctitle"]   // :title: → 文書タイトル
```

---

## 2. 再現可能ビルドの入力集合の定義

**節: [17.1](roadmap.md#171-docsarchitecturemd) と [9.5.1](roadmap.md#951-再現可能ビルドは-localremote-では決まらない)。**

roadmap は再現性（同一入力 → 同一出力）と再現可能ビルド（同一ソース ツリー → 同一出力）を
分けているが、**入力集合に何が入るのかを述べていない。** そこが決まらないと決められないこと:

- ファイルの mtime は**入力**なのか**環境**なのか。入力なら `git clone` した時刻で
  出力が変わるので再現可能ビルドではなくなる。環境なら mtime を出力に混ぜてはならない
- したがって **`:fileModTime` 相当の別名候補（Hugo の `lastmod` の暗黙のフォールバック）を
  そもそも許すのかどうか。** Hugo は `:git` / `:fileModTime` を並べるが、
  あれらは必ず値を返すので候補の順序が実際に効く。Lithify がこれらを持たないなら、
  「候補の順序はどうせ `LI1003` で報告される競合を裁くだけ」という現在の前提が保たれる
- ビルド時刻、ホスト名、絶対パス、ロケール、タイム ゾーンの扱い
  （`MetadataValue.TryGetDateTimeOffset` は既に `InvariantCulture` + UTC 既定で、
  この方向の判断を 1 つ済ませている）

**定義を書くのが先で、`:fileModTime` の可否はその帰結である。** 順序を逆にしてはならない。

---

## 3. `MetadataValue` を C# から組み立てる手段が無い

**節: [10.5](roadmap.md#105-lithifyblog) / [15](roadmap.md#15-samplesblog)。サイト全体の既定メタデータを書く段で必ず当たる。**

`MetadataValue` は構造だけを持つ閉じた DU なので、パーサーが組み立てるには足りているが、
**利用者が C# で書くには現実的でない**（サイトは C# で構成する。R5）。

```csharp
// Sequence: 冗長
new MetadataValue.Sequence([new MetadataValue.Scalar("tech"), new MetadataValue.Scalar("dotnet")])

// Mapping: 書けたものではない
new MetadataValue.Mapping(ImmutableDictionary.CreateRange(
    [KeyValuePair.Create(MetadataKey.Create("twitter"), (MetadataValue)new MetadataValue.Scalar("@x"))]))
```

必要そうなもの: `MetadataValue.Of(...)` 系のファクトリ、`string` からの暗黙の変換、
`Mapping` 用のコレクション式が通る形。**別名の設定とは独立した課題である**
（別名は `MetadataValue` を無変更で動かすだけなので、こちらの欠陥に影響されない）。

決めること: 暗黙の変換をどこまで入れるか（`Abstractions` の公開 API が増える）、
ディレクトリごとの既定値をどう書くか（`WithFallback` を外側から内側へ繰り返す形は決まっているが、
それを利用者がどう書くかは決まっていない）。

---

## 4. 別名の形式ごとの設定

**節: [10.1](roadmap.md#別名の設定101-の後に加えた設計)。当面は不要と判断したが、判断の根拠が経験ではなく調査である。**

現状は共通設定のみ。根拠は「AsciiDoc の形式固有属性は描画指令であって well-known な
写し先を持たず、メタデータらしい属性は既存ジェネレーターと意味が一致する」こと。
唯一見つかった実害候補が項目 1 の `:title:` で、それは形式ごとの設定では解決しない
（利用者の設定ではなくパーサーの既定の問題である）。

**実例が出たら再考する。** 出たときに足すのは無害で、上書きが写し先ごとの置き換えなので
形式ごとの層は共通の層の後に同じ演算で重なるだけであり、既存の設定の意味は変わらない。

---

## 5. `Meziantou.Framework.Http.Caching` を依存として受け入れるか

**節: [9.4](roadmap.md#94-lithifysourceshttp-の構成httpclient-の外か-delegatinghandler-か)。**

RFC 準拠の HTTP キャッシュを自分で書くか、このパッケージに乗るか。
**個人メンテナンスのパッケージである**ことが判断の中心。
サプライ チェーンの観点はセキュリティ機構を作る話ではないので、
「全体が形になるまでセキュリティはドキュメントに留める」という方針とは衝突しない。

---

## 6. テンプレートの置き場所と名前解決

**節: [10.4.1](roadmap.md#1041-テンプレートの置き場所未決)。roadmap 側に材料が揃っているので、ここでは索引だけ。**

「`SourceRoot` 配下」に固定できない（Blazor のテンプレートはアセンブリ内の型で、
`TemplateSource.FromTypeName` は `ContentPath` を受け取らない）。
Hugo の mounts による合成は 9.3 で却下した仮想ファイル システムそのものなので採れない。

併せて未決: `_templates/` が `SourceRoot` 配下にある場合にコンテンツの列挙から除く規則の形
（規約による除外か、明示的な設定か）。

---

## 7. 媒体型をどう運ぶか

**節: [9.5](roadmap.md#95-帰結) / `ContentFormatRegistry.TryGetFormatByMediaType`。**

拡張子を持たないリモート コンテンツのために媒体型からの形式判定が要るが、
**媒体型を運ぶ手段が決まっていない**（`ContentSourceResult` に添えるか、`ContentSource` が持つか）。
決まるまで `IContentFormatRegistry` を広げず、`ContentFormatRegistry` の公開メソッドに留めている。
つまり**現状この経路には到達できない。**

---

## 8. `docs/architecture.md` が存在しない

**節: [17.1](roadmap.md#171-docsarchitecturemd)。未決事項ではなく単に未着手だが、影響が大きいのでここに置く。**

[README.md](../README.md) と [.claude/CLAUDE.md](../.claude/CLAUDE.md) の両方が参照しており、
後者は「変更する際は読むこと」と指示している。**参照先が無い指示は守れない。**
書くべき内容は 17.1 に列挙済み。
