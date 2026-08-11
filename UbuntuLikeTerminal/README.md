# UbuntuLikeTerminal

Windows 用の自作ターミナル。C# / .NET Framework 4.8。NuGet パッケージ不使用（オフライン環境でもビルド可）。

## 開くには

1. Visual Studio 2019 を起動
2. `UbuntuLikeTerminal.sln`（または `.csproj` を直接）を開く
3. .NET Framework 4.8 Developer Pack がインストールされていることを確認
4. F5 でビルド＆実行

SDK スタイルの `.csproj` を使っていますが `<TargetFramework>net48</TargetFramework>` を指定しているので、
NuGet の復元は不要です（パッケージ参照が一切ありません）。もし VS が復元を試みてオフラインでエラーになる場合は、
ソリューションを右クリック →「NuGet パッケージの復元」を無効にするか、`dotnet build /p:RestorePackages=false` 相当の
設定で問題ありません。基本的には参照パッケージが無いのでビルドは完全にオフラインで完結します。

## 対応コマンド

| コマンド | オプション | 説明 |
|---|---|---|
| `ls` | `-l` `-a` `-1` | 一覧表示（ディレクトリ名の末尾には `\` が付きます） |
| `pwd` | | カレントディレクトリ表示 |
| `cd` | | ディレクトリ移動（`~`, `..` 対応） |
| `cp` | `-r` | コピー |
| `mv` | | 移動・リネーム |
| `rm` | `-r` `-f` | 削除 |
| `cat` | | ファイル内容表示 |
| `grep` | `-i` `-n` `-r` `-v` | 文字列検索（.NET 正規表現） |
| `mkdir` | `-p`（常に再帰作成） | ディレクトリ作成 |
| `rmdir` | | 空ディレクトリ削除 |
| `touch` | | ファイル作成／更新日時変更 |
| `echo` | | テキスト出力 |
| `clear` / `cls` | | 画面クリア |
| `history` | `[件数]` | コマンド履歴表示（`~/.ublt_history` に永続化） |
| `vim` / `vi` | | Git Bash の vim.exe を起動 |
| `help` | | コマンド一覧 |
| `exit` / `quit` | | 終了 |

## キー操作

- **Tab**: コマンド名／パスの補完。ディレクトリを補完した場合は末尾に `\` が自動付与されます。候補が複数あり展開できない場合、もう一度 Tab を押すと候補一覧を表示します。
- **Ctrl+K**: カーソル位置から行末までを削除
- **Ctrl+U**: 行頭からカーソル位置までを削除
- **↑ / ↓**: コマンド履歴を遡る／進む（bash と同様）
- **Home/End, ←/→, Backspace/Delete**: 通常のライン編集
- **Ctrl+C**: 入力中の行をキャンセル

これらはすべて `Console.ReadLine()` を使わず、`LineEditor` クラスで `Console.ReadKey` ベースの
独自ライン編集を実装することで実現しています（標準の `ReadLine` は Tab 補完や Ctrl+K/U、履歴検索に対応していないため）。

## 文字化け対策

- 起動時に `SetConsoleOutputCP` / `SetConsoleCP` で コードページを 65001 (UTF-8) に設定し、
  `Console.OutputEncoding` / `Console.InputEncoding` も UTF-8 に設定しています。
- 日本語ファイル名やコメントが文字化けする場合は、**コンソールのプロパティでフォントを
  「MS ゴシック」などの日本語対応の等幅フォントに変更**してください（`Consolas` は日本語グリフを含みません）。
  Windows Terminal を使う場合はこの問題は基本的に発生しません。
- カーソル位置計算は日本語などの全角文字を 2 桁分として扱うようにしているため、
  日本語混じりの行でも Ctrl+K/U やカーソル移動の位置がずれないようにしています。

### 既知の制限（IME）

`Console.ReadKey` ベースの実装のため、**IME（日本語入力）で変換中の文字列を直接この画面へ入力する挙動は
Windows の通常のコンソールアプリと同様の制限を受けます**。IME確定後の文字は正しく入力されますが、
環境によって変換ウィンドウの表示位置がずれる場合があります。ファイルパスなどに日本語を使う場合は、
エクスプローラー等からコピーして **貼り付け（右クリック貼り付け／Ctrl+Shift+V等、環境依存）** する方法が安定して動作します。

## vim (Git Bash) 連携

`vim` または `vi` と入力すると、以下の順で `vim.exe` を探して起動します（見つかった時点でコンソールをそのまま vim に明け渡します）。

1. 環境変数 `VIM_PATH`（vim.exe へのフルパス）
2. `C:\Program Files\Git\usr\bin\vim.exe`
3. `C:\Program Files\Git\bin\vim.exe`
4. `C:\Program Files (x86)\Git\usr\bin\vim.exe` / `bin\vim.exe`
5. `%LocalAppData%\Programs\Git\usr\bin\vim.exe`
6. `PATH` 環境変数上の `vim.exe`

見つからない場合はエラーメッセージと共に `VIM_PATH` の設定方法を案内します。

例:
```
vim memo.txt
```

## ファイル構成

- `Program.cs` — エントリポイント。エンコーディング設定とメインループ
- `LineEditor.cs` — Tab補完・Ctrl+K/U・履歴検索を実装する独自ライン編集
- `PathCompleter.cs` — コマンド名／パスの補完ロジック
- `CommandHistory.cs` — 履歴の保持・永続化・↑↓ナビゲーション
- `CommandExecutor.cs` — 各コマンドの実装本体
- `Tokenizer.cs` — コマンドラインの分割（`"..."` 対応）
- `VimLauncher.cs` — Git Bash の vim.exe 検出・起動
- `DisplayWidth.cs` — 全角文字を考慮したカーソル位置計算

## 拡張したい場合

- パイプ（`|`）やリダイレクト（`>`, `>>`）は現状未対応です。`CommandExecutor.Execute` の入口で
  `|` 等を解釈して複数コマンドを繋ぐ形で拡張できます。
- `ls -l` の「パーミッション」表示は Windows の属性（読み取り専用・隠しファイル）を簡易的に
  Unix 風に見せているだけで、本物の Unix パーミッションではありません。
