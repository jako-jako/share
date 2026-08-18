using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UbuntuLikeTerminal
{
    public class CompletionResult
    {
        /// <summary>The text that should replace the token being completed.</summary>
        public string ReplacementText;
        /// <summary>All candidates matching the prefix (used to print a list on double-Tab when ambiguous).</summary>
        public List<string> Candidates = new List<string>();
    }

    public static class PathCompleter
    {
        private static readonly string[] BuiltinCommands = new[]
        {
            "ls", "pwd", "cd", "cp", "mv", "rm", "cat", "grep", "mkdir", "rmdir",
            "touch", "echo", "clear", "cls", "history", "alias", "unalias", "vim", "vi", "help", "exit", "quit"
        };

        /// <summary>Complete a command name (first word on the line).</summary>
        public static CompletionResult CompleteCommand(string prefix)
        {
            var matches = BuiltinCommands
                .Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new CompletionResult { Candidates = matches };
            if (matches.Count == 0)
            {
                result.ReplacementText = prefix;
            }
            else if (matches.Count == 1)
            {
                result.ReplacementText = matches[0] + " ";
            }
            else
            {
                string common = LongestCommonPrefix(matches);
                result.ReplacementText = common;
            }
            return result;
        }

        /// <summary>
        /// Complete a filesystem path fragment relative to the current directory.
        /// Appends a trailing backslash ("\") when the sole/extended match is a directory,
        /// matching the requested Windows-style path completion behavior.
        /// </summary>
        public static CompletionResult CompletePath(string token, string currentDirectory)
        {
            token = token ?? "";

            // A bare "~" is equivalent to "~\": both denote the home directory itself. Normalizing here
            // means namePrefix (derived below from the *expanded* token) never ends up longer than the
            // *original* token, which would otherwise make the tilde-form reconstruction below go negative.
            if (token == "~") token = "~\\";

            // Expand a leading ~ to the user's profile directory, like a Unix shell.
            string expandedToken = token;
            bool hadTilde = false;
            if (expandedToken == "~" || expandedToken.StartsWith("~\\") || expandedToken.StartsWith("~/"))
            {
                hadTilde = true;
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                expandedToken = home + expandedToken.Substring(1);
            }

            string dirPart;
            string namePrefix;
            SplitPath(expandedToken, out dirPart, out namePrefix);

            string searchDir;
            if (string.IsNullOrEmpty(dirPart))
            {
                searchDir = currentDirectory;
            }
            else if (IsRooted(dirPart))
            {
                searchDir = dirPart;
            }
            else
            {
                searchDir = Path.Combine(currentDirectory, dirPart);
            }

            var result = new CompletionResult();

            List<string> entries;
            try
            {
                entries = Directory.GetFileSystemEntries(searchDir)
                    .Select(Path.GetFileName)
                    .Where(n => n.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                result.ReplacementText = token;
                return result;
            }

            result.Candidates = entries;

            if (entries.Count == 0)
            {
                result.ReplacementText = token;
                return result;
            }

            // Prefix used to rebuild the token as typed by the user (preserve ~ form, original dirPart text).
            string originalDirPartForRebuild = hadTilde
                ? token.Substring(0, token.Length - namePrefix.Length)
                : dirPart;

            if (entries.Count == 1)
            {
                string full = Path.Combine(searchDir, entries[0]);
                bool isDir = Directory.Exists(full);
                result.ReplacementText = originalDirPartForRebuild + entries[0] + (isDir ? "\\" : "");
                return result;
            }

            // Multiple matches: extend to the longest common prefix among candidate names.
            string commonName = LongestCommonPrefix(entries);
            if (commonName.Length > namePrefix.Length)
            {
                result.ReplacementText = originalDirPartForRebuild + commonName;
            }
            else
            {
                // Can't extend further - leave token unchanged; caller may print candidate list.
                result.ReplacementText = token;
            }
            return result;
        }

        private static bool IsRooted(string path)
        {
            try { return Path.IsPathRooted(path); }
            catch { return false; }
        }

        private static void SplitPath(string token, out string dirPart, out string namePrefix)
        {
            int lastSep = token.LastIndexOfAny(new[] { '\\', '/' });
            if (lastSep < 0)
            {
                dirPart = "";
                namePrefix = token;
            }
            else
            {
                dirPart = token.Substring(0, lastSep + 1);
                namePrefix = token.Substring(lastSep + 1);
            }
        }

        private static string LongestCommonPrefix(List<string> values)
        {
            if (values.Count == 0) return "";
            string prefix = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                prefix = CommonPrefix(prefix, values[i]);
                if (prefix.Length == 0) break;
            }
            return prefix;
        }

        private static string CommonPrefix(string a, string b)
        {
            int len = Math.Min(a.Length, b.Length);
            int i = 0;
            while (i < len && char.ToLowerInvariant(a[i]) == char.ToLowerInvariant(b[i])) i++;
            return a.Substring(0, i);
        }
    }
}
