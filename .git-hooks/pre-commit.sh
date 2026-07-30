#!/bin/bash
# コミット前の検査。.gitconfig の [hook "pre-commit"] から呼ばれる。
#
# lint-staged 相当のことをするが、node には依存しない。
# lint-staged が node を必要とするのは staged ファイルの一覧取得と振り分けのためで、
# それは git diff --cached と case 文で足りる。
# さらに lint-staged は絶対パスを渡してくるため dotnet format に渡す前に相対パスへ
# 変換する必要があったが、git diff --cached はもともと相対パスを返すのでその手間もない。
#
# 制約: 検査対象はワークツリー上のファイルであって、staged された内容そのものではない。
# lint-staged は未 staged の変更を一時退避して staged の内容だけを検査するが、
# ここではその複雑さを持たない。部分 staging をした場合、検査は
# ワークツリーの内容に対して行われる。
set -euo pipefail

repo_root=$(git rev-parse --show-toplevel)
cd "$repo_root"

# 追加・コピー・変更されたファイルのみを対象にする（削除されたファイルは検査できない）。
# ファイル名に空白や非 ASCII が含まれても壊れないよう NUL 区切りで受け取る。
staged=()
while IFS= read -r -d '' file; do
    # staged 後にワークツリーから消えたファイルは飛ばす
    if [ -f "$file" ]; then
        staged+=("$file")
    fi
done < <(git diff --cached --name-only --diff-filter=ACM -z)

if [ ${#staged[@]} -eq 0 ]; then
    exit 0
fi

bash .git-hooks/check-encoding.sh --check "${staged[@]}"

cs_files=()
for file in "${staged[@]}"; do
    case "$file" in
        *.cs) cs_files+=("$file") ;;
    esac
done

if [ ${#cs_files[@]} -gt 0 ]; then
    # SDK 同梱の dotnet format を使うので .config/dotnet-tools.json への登録は不要。
    # style と whitespace を分けて呼ぶのは、analyzers（--severity 既定）まで走らせると
    # コミットのたびにソリューション全体の解析が走って遅すぎるため。
    include=$(printf '%s ' "${cs_files[@]}")
    # shellcheck disable=SC2086 # include は空白区切りの複数引数として展開させたい
    dotnet format style Lithify.slnx --verify-no-changes --include $include
    # shellcheck disable=SC2086
    dotnet format whitespace Lithify.slnx --verify-no-changes --include $include
fi
