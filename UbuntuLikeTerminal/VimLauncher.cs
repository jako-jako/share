using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace UbuntuLikeTerminal
{
    public static class VimLauncher
    {
        /// <summary>
        /// Search order for vim.exe:
        /// 1. VIM_PATH environment variable, if set (full path to vim.exe)
        /// 2. Common Git for Windows install locations
        /// 3. Any "vim.exe" found on PATH
        /// </summary>
        public static string FindVimExecutable()
        {
            string envPath = Environment.GetEnvironmentVariable("VIM_PATH");
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath)) return envPath;

            string[] candidates =
            {
                @"C:\Program Files\Git\usr\bin\vim.exe",
                @"C:\Program Files\Git\bin\vim.exe",
                @"C:\Program Files (x86)\Git\usr\bin\vim.exe",
                @"C:\Program Files (x86)\Git\bin\vim.exe",
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }

            // Try common per-user install location too
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userGitVim = Path.Combine(localAppData, @"Programs\Git\usr\bin\vim.exe");
            if (File.Exists(userGitVim)) return userGitVim;

            // Fall back to searching PATH
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathEnv.Split(';'))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    string candidate = Path.Combine(dir.Trim(), "vim.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch
                {
                    // ignore malformed PATH entries
                }
            }

            return null;
        }

        public static void Launch(string[] args, string workingDirectory)
        {
            string vimPath = FindVimExecutable();
            if (vimPath == null)
            {
                Console.WriteLine("vim.exe が見つかりませんでした。Git for Windows がインストールされているか確認してください。");
                Console.WriteLine("見つからない場合は、環境変数 VIM_PATH に vim.exe のフルパスを設定してください。");
                Console.WriteLine(@"例: C:\Program Files\Git\usr\bin\vim.exe");
                return;
            }

            string argLine = string.Join(" ", args.Select(QuoteIfNeeded));

            var psi = new ProcessStartInfo
            {
                FileName = vimPath,
                Arguments = argLine,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false, // inherit this console window so vim can take it over
            };

            try
            {
                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("vim の起動に失敗しました: " + ex.Message);
            }
        }

        private static string QuoteIfNeeded(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";
            return arg.Contains(" ") ? "\"" + arg + "\"" : arg;
        }
    }
}
