#!/bin/bash
set -euo pipefail

mode="check"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --check)   mode="check";   shift ;;
        --correct) mode="correct"; shift ;;
        --) shift; break ;;
        -*) echo "error: unknown option: $1" >&2
            echo "Usage: $0 [--check|--correct] <file>..." >&2
            exit 1 ;;
        *) break ;;
    esac
done

if [ $# -eq 0 ]; then
    echo "Usage: $0 [--check|--correct] <file>..." >&2
    exit 1
fi

fail=0
bom_files=()

for f in "$@"; do
    # バイナリファイルはスキップ
    if ! grep -Iq . "$f" 2>/dev/null; then
        continue
    fi

    # UTF-8 BOM (EF BB BF) の確認
    bom=$(od -An -tx1 -N3 "$f" | tr -d ' \n')
    if [[ "$bom" == efbbbf* ]]; then
        if [[ "$mode" == "correct" ]]; then
            sed -i '1s/^\xEF\xBB\xBF//' "$f"
            echo "BOM removed: $f"
        else
            echo "error: UTF-8 BOM detected: $f" >&2
            bom_files+=("$f")
            fail=1
        fi
    fi
done

if [ ${#bom_files[@]} -gt 0 ]; then
    echo "" >&2
    echo "BOM を除去するには以下を実行してください:" >&2
    echo "  bash .git-hooks/check-encoding.sh --correct ${bom_files[*]}" >&2
fi

exit $fail
