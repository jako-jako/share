#!/usr/bin/env bash
# Linux 上で UbuntuLikeTerminal (net48) をビルドするためのスクリプト。
# Docker + mono イメージの msbuild を使う（Windows 実機や Visual Studio がなくてもコンパイル確認ができる）。
set -euo pipefail

cd "$(dirname "$0")"

IMAGE="ubuntu-like-terminal-build"
CONFIG="${1:-Release}"

docker build -q -t "$IMAGE" .

docker run --rm -v "$PWD:/src" -w /src "$IMAGE" sh -c "
    set -e
    msbuild UbuntuLikeTerminal.sln /t:Restore /nologo
    msbuild UbuntuLikeTerminal.sln /p:Configuration=$CONFIG /nologo
    chown -R $(id -u):$(id -g) /src/bin /src/obj
"

echo
echo "ビルド成果物: bin/$CONFIG/net48/UbuntuLikeTerminal.exe"
echo "(Windows 専用アプリのため、実行は Windows 環境で行ってください)"
