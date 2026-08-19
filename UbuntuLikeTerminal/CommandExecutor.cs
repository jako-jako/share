using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UbuntuLikeTerminal
{
    public class CommandExecutor
    {
        private readonly CommandHistory _history;
        private readonly AliasManager _aliases;
        public bool ShouldExit { get; private set; }

        public CommandExecutor(CommandHistory history, AliasManager aliases)
        {
            _history = history;
            _aliases = aliases;
        }

        public void Execute(string line)
        {
            var tokens = Tokenizer.Tokenize(line);
            if (tokens.Count == 0) return;

            string cmd = tokens[0];
            string fallbackLine = line;

            // alias展開（1段階のみ。alias同士のチェインは意図的に非対応とし無限ループを防ぐ）
            string aliasValue;
            if (_aliases.TryGet(cmd, out aliasValue))
            {
                var expanded = Tokenizer.Tokenize(aliasValue);
                expanded.AddRange(tokens.Skip(1));
                tokens = expanded;
                cmd = tokens.Count > 0 ? tokens[0] : cmd;
                fallbackLine = string.Join(" ", tokens);
            }

            var args = tokens.Skip(1).ToList();

            try
            {
                switch (cmd.ToLowerInvariant())
                {
                    case "ls": Ls(args); break;
                    case "pwd": Console.WriteLine(Environment.CurrentDirectory); break;
                    case "cd": Cd(args); break;
                    case "cp": Cp(args); break;
                    case "mv": Mv(args); break;
                    case "rm": Rm(args); break;
                    case "cat": Cat(args); break;
                    case "grep": Grep(args); break;
                    case "mkdir": Mkdir(args); break;
                    case "rmdir": Rmdir(args); break;
                    case "touch": Touch(args); break;
                    case "echo": Console.WriteLine(string.Join(" ", args)); break;
                    case "clear":
                    case "cls": Console.Clear(); break;
                    case "history": History(args); break;
                    case "alias": Alias(args); break;
                    case "unalias": Unalias(args); break;
                    case "split": Split(args); break;
                    case "vim":
                    case "vi": VimLauncher.Launch(args.ToArray(), Environment.CurrentDirectory); break;
                    case "help": Help(); break;
                    case "exit":
                    case "quit": ShouldExit = true; break;
                    default:
                        FallbackToCmd(cmd, fallbackLine);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(cmd + ": エラー - " + ex.Message);
            }
        }

        // ---- option parsing helper ----------------------------------------------------

        private static void ParseFlags(List<string> args, out HashSet<char> flags, out List<string> positional)
        {
            flags = new HashSet<char>();
            positional = new List<string>();
            foreach (var arg in args)
            {
                if (arg.Length >= 2 && arg[0] == '-' && arg != "-" && arg != "--")
                {
                    foreach (char c in arg.Substring(1)) flags.Add(char.ToLowerInvariant(c));
                }
                else
                {
                    positional.Add(arg);
                }
            }
        }

        // ---- ls -------------------------------------------------------------------------

        private void Ls(List<string> args)
        {
            HashSet<char> flags;
            List<string> positional;
            ParseFlags(args, out flags, out positional);

            bool showAll = flags.Contains('a');
            bool longFormat = flags.Contains('l');
            bool onePerLine = flags.Contains('1') || longFormat;

            var targets = positional.Count == 0 ? new List<string> { "." } : positional;

            for (int t = 0; t < targets.Count; t++)
            {
                string target = ResolvePath(targets[t]);
                if (targets.Count > 1)
                {
                    if (t > 0) Console.WriteLine();
                    Console.WriteLine(targets[t] + ":");
                }

                if (File.Exists(target))
                {
                    PrintEntry(target, longFormat);
                    continue;
                }

                if (!Directory.Exists(target))
                {
                    Console.WriteLine("ls: '" + targets[t] + "' が見つかりません");
                    continue;
                }

                IEnumerable<string> entries = Directory.GetFileSystemEntries(target)
                    .Select(Path.GetFileName)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

                if (!showAll)
                {
                    entries = entries.Where(n => !n.StartsWith("."));
                }

                var list = entries.ToList();

                if (onePerLine)
                {
                    foreach (var name in list) PrintEntry(Path.Combine(target, name), longFormat);
                }
                else
                {
                    PrintColumns(list, target);
                }
            }
        }

        private void PrintEntry(string fullPath, bool longFormat)
        {
            string name = Path.GetFileName(fullPath);
            bool isDir = Directory.Exists(fullPath);

            if (!longFormat)
            {
                Console.WriteLine(name + (isDir ? "\\" : ""));
                return;
            }

            string typeChar = isDir ? "d" : "-";
            string sizeStr = "-";
            string modified = "";
            try
            {
                if (isDir)
                {
                    modified = Directory.GetLastWriteTime(fullPath).ToString("yyyy-MM-dd HH:mm");
                }
                else
                {
                    var fi = new FileInfo(fullPath);
                    sizeStr = fi.Length.ToString();
                    modified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                }
            }
            catch
            {
                // ignore inaccessible metadata
            }

            var attr = FileAttributes.Normal;
            try { attr = File.GetAttributes(fullPath); } catch { }
            bool readOnly = (attr & FileAttributes.ReadOnly) != 0;
            bool hidden = (attr & FileAttributes.Hidden) != 0;

            string perms = typeChar + (readOnly ? "r-" : "rw") + "-" + (hidden ? " (hidden)" : "");

            Console.WriteLine(string.Format("{0} {1,10} {2}  {3}{4}", perms, sizeStr, modified, name, isDir ? "\\" : ""));
        }

        private void PrintColumns(List<string> names, string baseDir)
        {
            if (names.Count == 0) return;

            var display = names.Select(n => n + (Directory.Exists(Path.Combine(baseDir, n)) ? "\\" : "")).ToList();
            int colWidth = display.Max(DisplayWidth.Of) + 2;
            int consoleWidth = SafeConsoleWidth();
            int columns = Math.Max(1, consoleWidth / colWidth);

            for (int i = 0; i < display.Count; i++)
            {
                bool lastInRow = (i + 1) % columns == 0 || i == display.Count - 1;
                Console.Write(display[i]);
                if (!lastInRow) Console.Write(new string(' ', colWidth - DisplayWidth.Of(display[i])));
                if ((i + 1) % columns == 0) Console.WriteLine();
            }
            if (display.Count % columns != 0) Console.WriteLine();
        }

        private static int SafeConsoleWidth()
        {
            try { return Math.Max(20, Console.BufferWidth); }
            catch { return 80; }
        }

        // ---- cd -------------------------------------------------------------------------

        private void Cd(List<string> args)
        {
            string target = args.Count == 0
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : ResolvePath(args[0]);

            if (!Directory.Exists(target))
            {
                Console.WriteLine("cd: '" + (args.Count == 0 ? "~" : args[0]) + "' は存在しません");
                return;
            }
            Environment.CurrentDirectory = target;
        }

        // ---- cp -------------------------------------------------------------------------

        private void Cp(List<string> args)
        {
            HashSet<char> flags;
            List<string> positional;
            ParseFlags(args, out flags, out positional);
            bool recursive = flags.Contains('r') || flags.Contains('R');

            if (positional.Count < 2)
            {
                Console.WriteLine("使い方: cp [-r] コピー元 コピー先");
                return;
            }

            string src = ResolvePath(positional[0]);
            string dst = ResolvePath(positional[1]);

            if (Directory.Exists(src))
            {
                if (!recursive)
                {
                    Console.WriteLine("cp: '" + positional[0] + "' はディレクトリです（-r を指定してください）");
                    return;
                }
                if (Directory.Exists(dst)) dst = Path.Combine(dst, Path.GetFileName(src.TrimEnd('\\', '/')));
                CopyDirectory(src, dst);
            }
            else if (File.Exists(src))
            {
                if (Directory.Exists(dst)) dst = Path.Combine(dst, Path.GetFileName(src));
                File.Copy(src, dst, true);
            }
            else
            {
                Console.WriteLine("cp: '" + positional[0] + "' が見つかりません");
            }
        }

        private void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var file in Directory.GetFiles(src))
            {
                File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
            }
            foreach (var dir in Directory.GetDirectories(src))
            {
                CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
            }
        }

        // ---- mv -------------------------------------------------------------------------

        private void Mv(List<string> args)
        {
            HashSet<char> flags;
            List<string> positional;
            ParseFlags(args, out flags, out positional);

            if (positional.Count < 2)
            {
                Console.WriteLine("使い方: mv 移動元 移動先");
                return;
            }

            string src = ResolvePath(positional[0]);
            string dst = ResolvePath(positional[1]);

            if (Directory.Exists(dst))
            {
                dst = Path.Combine(dst, Path.GetFileName(src.TrimEnd('\\', '/')));
            }

            if (Directory.Exists(src))
            {
                Directory.Move(src, dst);
            }
            else if (File.Exists(src))
            {
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(src, dst);
            }
            else
            {
                Console.WriteLine("mv: '" + positional[0] + "' が見つかりません");
            }
        }

        // ---- rm -------------------------------------------------------------------------

        private void Rm(List<string> args)
        {
            HashSet<char> flags;
            List<string> positional;
            ParseFlags(args, out flags, out positional);
            bool recursive = flags.Contains('r') || flags.Contains('R');
            bool force = flags.Contains('f');

            if (positional.Count == 0)
            {
                Console.WriteLine("使い方: rm [-r] [-f] パス...");
                return;
            }

            foreach (var p in positional)
            {
                string full = ResolvePath(p);
                try
                {
                    if (Directory.Exists(full))
                    {
                        if (!recursive)
                        {
                            Console.WriteLine("rm: '" + p + "' はディレクトリです（-r を指定してください）");
                            continue;
                        }
                        Directory.Delete(full, true);
                    }
                    else if (File.Exists(full))
                    {
                        File.Delete(full);
                    }
                    else if (!force)
                    {
                        Console.WriteLine("rm: '" + p + "' が見つかりません");
                    }
                }
                catch (Exception ex)
                {
                    if (!force) Console.WriteLine("rm: '" + p + "' を削除できません - " + ex.Message);
                }
            }
        }

        // ---- cat ------------------------------------------------------------------------

        private void Cat(List<string> args)
        {
            if (args.Count == 0)
            {
                Console.WriteLine("使い方: cat ファイル...");
                return;
            }

            foreach (var a in args)
            {
                string full = ResolvePath(a);
                if (!File.Exists(full))
                {
                    Console.WriteLine("cat: '" + a + "' が見つかりません");
                    continue;
                }
                Console.WriteLine(File.ReadAllText(full, Encoding.UTF8));
            }
        }

        // ---- grep -----------------------------------------------------------------------

        private void Grep(List<string> args)
        {
            HashSet<char> flags;
            List<string> positional;
            ParseFlags(args, out flags, out positional);

            bool ignoreCase = flags.Contains('i');
            bool showLineNumbers = flags.Contains('n');
            bool recursive = flags.Contains('r') || flags.Contains('R');
            bool invert = flags.Contains('v');

            if (positional.Count == 0)
            {
                Console.WriteLine("使い方: grep [-i] [-n] [-r] [-v] パターン [ファイル...]");
                return;
            }

            string pattern = positional[0];
            var targets = positional.Skip(1).ToList();
            if (targets.Count == 0) targets.Add(".");

            var regexOptions = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            Regex regex;
            try
            {
                regex = new Regex(pattern, regexOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine("grep: 不正な正規表現です - " + ex.Message);
                return;
            }

            var filesToSearch = new List<string>();
            foreach (var t in targets)
            {
                string full = ResolvePath(t);
                if (Directory.Exists(full))
                {
                    if (recursive)
                    {
                        filesToSearch.AddRange(SafeEnumerateFiles(full));
                    }
                    else
                    {
                        Console.WriteLine("grep: '" + t + "' はディレクトリです（-r を指定してください）");
                    }
                }
                else if (File.Exists(full))
                {
                    filesToSearch.Add(full);
                }
                else
                {
                    Console.WriteLine("grep: '" + t + "' が見つかりません");
                }
            }

            bool multipleFiles = filesToSearch.Count > 1;

            foreach (var file in filesToSearch)
            {
                string[] lines;
                try { lines = File.ReadAllLines(file, Encoding.UTF8); }
                catch { continue; }

                for (int i = 0; i < lines.Length; i++)
                {
                    bool isMatch = regex.IsMatch(lines[i]);
                    if (isMatch == invert) continue;

                    var sb = new StringBuilder();
                    if (multipleFiles) sb.Append(file).Append(":");
                    if (showLineNumbers) sb.Append(i + 1).Append(":");
                    sb.Append(lines[i]);
                    Console.WriteLine(sb.ToString());
                }
            }
        }

        private static IEnumerable<string> SafeEnumerateFiles(string dir)
        {
            var result = new List<string>();
            try
            {
                foreach (var f in Directory.GetFiles(dir)) result.Add(f);
                foreach (var d in Directory.GetDirectories(dir)) result.AddRange(SafeEnumerateFiles(d));
            }
            catch
            {
                // skip inaccessible directories
            }
            return result;
        }

        // ---- mkdir / rmdir / touch --------------------------------------------------------

        private void Mkdir(List<string> args)
        {
            HashSet<char> flags;
            List<string> positional;
            ParseFlags(args, out flags, out positional);

            if (positional.Count == 0)
            {
                Console.WriteLine("使い方: mkdir [-p] ディレクトリ...");
                return;
            }

            foreach (var p in positional)
            {
                string full = ResolvePath(p);
                Directory.CreateDirectory(full); // .NET CreateDirectory is inherently recursive, like mkdir -p
            }
        }

        private void Rmdir(List<string> args)
        {
            if (args.Count == 0)
            {
                Console.WriteLine("使い方: rmdir ディレクトリ...");
                return;
            }
            foreach (var p in args)
            {
                string full = ResolvePath(p);
                try
                {
                    if (!Directory.Exists(full)) { Console.WriteLine("rmdir: '" + p + "' が見つかりません"); continue; }
                    if (Directory.GetFileSystemEntries(full).Length > 0)
                    {
                        Console.WriteLine("rmdir: '" + p + "' は空ではありません");
                        continue;
                    }
                    Directory.Delete(full);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("rmdir: '" + p + "' を削除できません - " + ex.Message);
                }
            }
        }

        private void Touch(List<string> args)
        {
            if (args.Count == 0)
            {
                Console.WriteLine("使い方: touch ファイル...");
                return;
            }
            foreach (var p in args)
            {
                string full = ResolvePath(p);
                if (File.Exists(full))
                {
                    File.SetLastWriteTime(full, DateTime.Now);
                }
                else
                {
                    using (File.Create(full)) { }
                }
            }
        }

        // ---- history --------------------------------------------------------------------

        private void History(List<string> args)
        {
            var entries = _history.Entries;
            int count = entries.Count;
            if (args.Count > 0 && int.TryParse(args[0], out int n))
            {
                count = Math.Min(n, entries.Count);
            }

            int start = entries.Count - count;
            for (int i = start; i < entries.Count; i++)
            {
                Console.WriteLine(string.Format("{0,5}  {1}", i + 1, entries[i]));
            }
        }

        // ---- alias / unalias --------------------------------------------------------------

        private void Alias(List<string> args)
        {
            if (args.Count == 0)
            {
                foreach (var kv in _aliases.Entries.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                {
                    Console.WriteLine("alias " + kv.Key + "='" + kv.Value + "'");
                }
                return;
            }

            foreach (var arg in args)
            {
                int eq = arg.IndexOf('=');
                if (eq <= 0)
                {
                    string existing;
                    if (_aliases.TryGet(arg, out existing))
                        Console.WriteLine("alias " + arg + "='" + existing + "'");
                    else
                        Console.WriteLine("alias: '" + arg + "' が見つかりません");
                    continue;
                }

                string name = arg.Substring(0, eq);
                string value = arg.Substring(eq + 1);
                _aliases.Set(name, value);
            }
        }

        private void Unalias(List<string> args)
        {
            if (args.Count == 0)
            {
                Console.WriteLine("使い方: unalias 名前...");
                return;
            }
            foreach (var name in args)
            {
                if (!_aliases.Remove(name))
                {
                    Console.WriteLine("unalias: '" + name + "' が見つかりません");
                }
            }
        }

        // ---- help -------------------------------------------------------------------------

        private void Help()
        {
            Console.WriteLine("利用可能なコマンド:");
            Console.WriteLine("  ls [-l -a -1] [パス...]      ディレクトリの内容を表示");
            Console.WriteLine("  pwd                          現在のディレクトリを表示");
            Console.WriteLine("  cd [パス]                    ディレクトリを移動");
            Console.WriteLine("  cp [-r] 元 先                コピー");
            Console.WriteLine("  mv 元 先                     移動/リネーム");
            Console.WriteLine("  rm [-r -f] パス...           削除");
            Console.WriteLine("  cat ファイル...               ファイル内容を表示");
            Console.WriteLine("  grep [-i -n -r -v] パターン [ファイル...]  文字列検索");
            Console.WriteLine("  mkdir [-p] パス...           ディレクトリ作成");
            Console.WriteLine("  rmdir パス...                空ディレクトリを削除");
            Console.WriteLine("  touch ファイル...             ファイル作成/更新日時変更");
            Console.WriteLine("  echo テキスト                 テキストを表示");
            Console.WriteLine("  clear / cls                  画面をクリア");
            Console.WriteLine("  history [件数]                コマンド履歴を表示");
            Console.WriteLine("  alias [名前=値...]           エイリアスを登録/一覧表示");
            Console.WriteLine("  unalias 名前...               エイリアスを削除");
            Console.WriteLine("  split [-h] [コマンド]         Windows Terminal で画面分割(既定:左右分割/自身を起動、-h で上下分割)");
            Console.WriteLine("  vim / vi [ファイル]           Git Bash の vim を起動");
            Console.WriteLine("  exit / quit                  終了");
            Console.WriteLine();
            Console.WriteLine("上記にないコマンドは Windows 標準のコマンドプロンプト(cmd.exe)にフォールバックして実行します。");
            Console.WriteLine("キー操作: Tab=補完  Ctrl+K=カーソルから行末まで削除  Ctrl+U=行頭からカーソルまで削除  ↑↓=履歴");
        }

        // ---- split（画面分割は Windows Terminal の split-pane に委譲） ----------------------

        private void Split(List<string> args)
        {
            HashSet<char> flags;
            List<string> positional;
            ParseFlags(args, out flags, out positional);

            string splitFlag = flags.Contains('h') ? "-H" : "-V";
            string target = positional.Count > 0
                ? string.Join(" ", positional)
                : "\"" + Process.GetCurrentProcess().MainModule.FileName + "\"";

            var psi = new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = "split-pane " + splitFlag + " -d \"" + Environment.CurrentDirectory + "\" " + target,
                UseShellExecute = false,
            };

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine("split: Windows Terminal (wt.exe) の起動に失敗しました - " + ex.Message);
                Console.WriteLine("Windows Terminal がインストールされ、PATH が通っていることを確認してください。");
            }
        }

        // ---- フォールバック（未知のコマンドは Windows 標準の cmd.exe に委ねる） ----------

        private void FallbackToCmd(string cmd, string rawLine)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c " + rawLine,
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false,
            };

            try
            {
                using (var process = Process.Start(psi))
                {
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(cmd + ": コマンドが見つかりません - " + ex.Message);
            }
        }

        // ---- helpers ------------------------------------------------------------------

        private static string ResolvePath(string p)
        {
            if (string.IsNullOrEmpty(p)) return Environment.CurrentDirectory;

            if (p == "~") return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (p.StartsWith("~\\") || p.StartsWith("~/"))
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), p.Substring(2));
            }

            if (Path.IsPathRooted(p)) return p;
            return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, p));
        }
    }
}
