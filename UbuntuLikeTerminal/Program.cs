using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace UbuntuLikeTerminal
{
    public static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCP(uint wCodePageID);

        private const uint CP_UTF8 = 65001;

        [STAThread]
        public static void Main(string[] args)
        {
            SetupEncoding();

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string historyFile = Path.Combine(userProfile, ".ublt_history");
            string aliasFile = Path.Combine(userProfile, ".ublt_aliases");

            var history = new CommandHistory(historyFile);
            var aliases = new AliasManager(aliasFile);
            var executor = new CommandExecutor(history, aliases);
            var editor = new LineEditor(history, () => Environment.CurrentDirectory);

            PrintBanner();

            while (!executor.ShouldExit)
            {
                string prompt = BuildPrompt();
                string line;
                try
                {
                    line = editor.ReadLine(prompt);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("入力エラー: " + ex.Message);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                history.Add(line);
                executor.Execute(line);
            }
        }

        private static void SetupEncoding()
        {
            // Make sure Japanese (and any UTF-8) text reads/writes without mojibake,
            // both via the .NET Console class and the underlying Win32 console code pages.
            try { SetConsoleOutputCP(CP_UTF8); } catch { /* ignore on non-Windows / restricted environments */ }
            try { SetConsoleCP(CP_UTF8); } catch { }

            try { Console.OutputEncoding = new UTF8Encoding(false); } catch { }
            try { Console.InputEncoding = new UTF8Encoding(false); } catch { }
        }

        private static void PrintBanner()
        {
            Console.WriteLine("Ubuntu風ターミナル (C# / .NET Framework 4.8)");
            Console.WriteLine("'help' でコマンド一覧、'exit' で終了します。");
            Console.WriteLine();
        }

        private static string BuildPrompt()
        {
            string cwd = Environment.CurrentDirectory;
            return cwd + "> ";
        }
    }
}
