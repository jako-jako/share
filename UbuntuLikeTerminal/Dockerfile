# .NET Framework 4.8 (net48) 向けの UbuntuLikeTerminal を Linux 上でビルドするための環境。
# Windows 専用アプリのため実行はできないが、コンパイル可否の確認・CI 用途に使う。
#
# 使い方:
#   docker build -t ubuntu-like-terminal-build .
#   docker run --rm -v "$PWD:/src" -w /src ubuntu-like-terminal-build \
#       sh -c "msbuild UbuntuLikeTerminal.sln /t:Restore && msbuild UbuntuLikeTerminal.sln /p:Configuration=Release"
#
# もしくは同梱の build.sh を使う: ./build.sh
FROM mono:6.12
WORKDIR /src
