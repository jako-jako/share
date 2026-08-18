using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UbuntuLikeTerminal
{
    /// <summary>
    /// Manages user-defined command aliases (e.g. "ll" -> "ls -l"),
    /// persisting them to a file across sessions like CommandHistory does.
    /// </summary>
    public class AliasManager
    {
        private readonly Dictionary<string, string> _aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _filePath;

        public AliasManager(string filePath)
        {
            _filePath = filePath;
            Load();
        }

        public IEnumerable<KeyValuePair<string, string>> Entries { get { return _aliases; } }

        public bool TryGet(string name, out string value)
        {
            return _aliases.TryGetValue(name, out value);
        }

        public void Set(string name, string value)
        {
            _aliases[name] = value;
            Save();
        }

        public bool Remove(string name)
        {
            bool removed = _aliases.Remove(name);
            if (removed) Save();
            return removed;
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return;
                foreach (var line in File.ReadAllLines(_filePath, System.Text.Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    _aliases[line.Substring(0, eq)] = line.Substring(eq + 1);
                }
            }
            catch
            {
                // ignore alias load failures - not fatal
            }
        }

        private void Save()
        {
            try
            {
                var lines = _aliases.Select(kv => kv.Key + "=" + kv.Value);
                File.WriteAllLines(_filePath, lines, System.Text.Encoding.UTF8);
            }
            catch
            {
                // ignore alias save failures - not fatal
            }
        }
    }
}
