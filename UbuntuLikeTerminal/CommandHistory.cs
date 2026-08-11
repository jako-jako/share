using System;
using System.Collections.Generic;
using System.IO;

namespace UbuntuLikeTerminal
{
    /// <summary>
    /// Keeps track of entered commands, supports Up/Down navigation like bash,
    /// and persists to a history file across sessions.
    /// </summary>
    public class CommandHistory
    {
        private readonly List<string> _entries = new List<string>();
        private int _cursor; // index into _entries while navigating with Up/Down; == _entries.Count means "not navigating / new line"
        private readonly string _historyFilePath;

        public CommandHistory(string historyFilePath)
        {
            _historyFilePath = historyFilePath;
            Load();
            _cursor = _entries.Count;
        }

        public IReadOnlyList<string> Entries { get { return _entries; } }

        public void Add(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) { _cursor = _entries.Count; return; }

            // avoid saving immediate duplicate of the last entry
            if (_entries.Count == 0 || _entries[_entries.Count - 1] != command)
            {
                _entries.Add(command);
                AppendToFile(command);
            }
            _cursor = _entries.Count;
        }

        public void ResetCursor()
        {
            _cursor = _entries.Count;
        }

        /// <summary>Move to previous (older) entry. Returns null if already at the oldest.</summary>
        public string Previous()
        {
            if (_entries.Count == 0) return null;
            if (_cursor > 0) _cursor--;
            return _cursor >= 0 && _cursor < _entries.Count ? _entries[_cursor] : null;
        }

        /// <summary>Move to next (newer) entry. Returns "" when moving past the newest (i.e. back to an empty line), or null if already there.</summary>
        public string Next()
        {
            if (_entries.Count == 0) return null;
            if (_cursor >= _entries.Count) return null;
            _cursor++;
            if (_cursor >= _entries.Count) return "";
            return _entries[_cursor];
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    foreach (var line in File.ReadAllLines(_historyFilePath, System.Text.Encoding.UTF8))
                    {
                        if (!string.IsNullOrWhiteSpace(line)) _entries.Add(line);
                    }
                }
            }
            catch
            {
                // ignore history load failures - not fatal
            }
        }

        private void AppendToFile(string command)
        {
            try
            {
                File.AppendAllText(_historyFilePath, command + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch
            {
                // ignore history write failures - not fatal
            }
        }
    }
}
