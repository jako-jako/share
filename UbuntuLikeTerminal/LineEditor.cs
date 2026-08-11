using System;
using System.Text;

namespace UbuntuLikeTerminal
{
    /// <summary>
    /// A minimal readline-style line editor built on Console.ReadKey, because the
    /// stock Console.ReadLine() supports none of: Tab completion, Ctrl+K / Ctrl+U,
    /// or Up/Down history recall.
    /// </summary>
    public class LineEditor
    {
        private readonly CommandHistory _history;
        private readonly Func<string> _currentDirectoryProvider;

        public LineEditor(CommandHistory history, Func<string> currentDirectoryProvider)
        {
            _history = history;
            _currentDirectoryProvider = currentDirectoryProvider;
        }

        public string ReadLine(string prompt)
        {
            Console.Write(prompt);

            var buffer = new StringBuilder();
            int cursor = 0;              // index into buffer, in characters
            int lastRenderedWidth = 0;   // display columns used by the previous render, for clearing leftovers
            string lastTabToken = null;  // for detecting a second consecutive Tab press on an ambiguous match
            System.Collections.Generic.List<string> lastCandidates = null;

            int startLeft = Console.CursorLeft;
            int startTop = Console.CursorTop;

            _history.ResetCursor();

            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                bool ctrl = (key.Modifiers & ConsoleModifiers.Control) != 0;

                // Any key other than Tab cancels the "double tab" ambiguity tracking.
                if (key.Key != ConsoleKey.Tab) { lastTabToken = null; lastCandidates = null; }

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return buffer.ToString();
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (cursor > 0)
                    {
                        buffer.Remove(cursor - 1, 1);
                        cursor--;
                        Redraw(buffer, cursor, startLeft, startTop, ref lastRenderedWidth);
                    }
                }
                else if (key.Key == ConsoleKey.Delete)
                {
                    if (cursor < buffer.Length)
                    {
                        buffer.Remove(cursor, 1);
                        Redraw(buffer, cursor, startLeft, startTop, ref lastRenderedWidth);
                    }
                }
                else if (key.Key == ConsoleKey.LeftArrow)
                {
                    if (cursor > 0) { cursor--; PositionCursor(buffer, cursor, startLeft, startTop); }
                }
                else if (key.Key == ConsoleKey.RightArrow)
                {
                    if (cursor < buffer.Length) { cursor++; PositionCursor(buffer, cursor, startLeft, startTop); }
                }
                else if (key.Key == ConsoleKey.Home || (ctrl && key.Key == ConsoleKey.A))
                {
                    cursor = 0;
                    PositionCursor(buffer, cursor, startLeft, startTop);
                }
                else if (key.Key == ConsoleKey.End || (ctrl && key.Key == ConsoleKey.E))
                {
                    cursor = buffer.Length;
                    PositionCursor(buffer, cursor, startLeft, startTop);
                }
                else if (ctrl && key.Key == ConsoleKey.K)
                {
                    // Kill from cursor to end of line
                    if (cursor < buffer.Length)
                    {
                        buffer.Remove(cursor, buffer.Length - cursor);
                        Redraw(buffer, cursor, startLeft, startTop, ref lastRenderedWidth);
                    }
                }
                else if (ctrl && key.Key == ConsoleKey.U)
                {
                    // Kill from start of line to cursor
                    if (cursor > 0)
                    {
                        buffer.Remove(0, cursor);
                        cursor = 0;
                        Redraw(buffer, cursor, startLeft, startTop, ref lastRenderedWidth);
                    }
                }
                else if (ctrl && key.Key == ConsoleKey.C)
                {
                    Console.WriteLine("^C");
                    buffer.Clear();
                    cursor = 0;
                    return "";
                }
                else if (key.Key == ConsoleKey.UpArrow)
                {
                    string prev = _history.Previous();
                    if (prev != null)
                    {
                        buffer.Clear();
                        buffer.Append(prev);
                        cursor = buffer.Length;
                        Redraw(buffer, cursor, startLeft, startTop, ref lastRenderedWidth);
                    }
                }
                else if (key.Key == ConsoleKey.DownArrow)
                {
                    string next = _history.Next();
                    if (next != null)
                    {
                        buffer.Clear();
                        buffer.Append(next);
                        cursor = buffer.Length;
                        Redraw(buffer, cursor, startLeft, startTop, ref lastRenderedWidth);
                    }
                }
                else if (key.Key == ConsoleKey.Tab)
                {
                    HandleTab(buffer, ref cursor, ref lastTabToken, ref lastCandidates);
                    Redraw(buffer, cursor, startLeft, startTop, ref lastRenderedWidth);
                }
                else if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
                {
                    buffer.Insert(cursor, key.KeyChar);
                    cursor++;
                    Redraw(buffer, cursor, startLeft, startTop, ref lastRenderedWidth);
                }
                // else: unrecognized/unsupported key - ignore
            }
        }

        private void HandleTab(StringBuilder buffer, ref int cursor, ref string lastTabToken, ref System.Collections.Generic.List<string> lastCandidates)
        {
            int tokenStart = cursor;
            while (tokenStart > 0 && buffer[tokenStart - 1] != ' ') tokenStart--;
            int tokenEnd = cursor;
            while (tokenEnd < buffer.Length && buffer[tokenEnd] != ' ') tokenEnd++;

            string token = buffer.ToString(tokenStart, tokenEnd - tokenStart);
            bool isFirstToken = tokenStart == 0;

            CompletionResult result = isFirstToken
                ? PathCompleter.CompleteCommand(token)
                : PathCompleter.CompletePath(token, _currentDirectoryProvider());

            if (result.Candidates.Count > 1 && result.ReplacementText == token && lastTabToken == token)
            {
                // Second consecutive Tab on an ambiguous, non-extendable match: show candidates.
                Console.WriteLine();
                Console.WriteLine(string.Join("  ", result.Candidates));
                lastTabToken = null;
                lastCandidates = null;
                return;
            }

            buffer.Remove(tokenStart, tokenEnd - tokenStart);
            buffer.Insert(tokenStart, result.ReplacementText);
            cursor = tokenStart + result.ReplacementText.Length;

            lastTabToken = result.Candidates.Count > 1 ? result.ReplacementText : null;
            lastCandidates = result.Candidates;
        }

        private void Redraw(StringBuilder buffer, int cursor, int startLeft, int startTop, ref int lastRenderedWidth)
        {
            Console.SetCursorPosition(startLeft, startTop);
            string text = buffer.ToString();
            Console.Write(text);

            int written = DisplayWidth.Of(text);
            if (written < lastRenderedWidth)
            {
                Console.Write(new string(' ', lastRenderedWidth - written));
            }
            lastRenderedWidth = written;

            PositionCursor(buffer, cursor, startLeft, startTop);
        }

        private void PositionCursor(StringBuilder buffer, int cursor, int startLeft, int startTop)
        {
            string upToCursor = buffer.ToString(0, cursor);
            int offset = startLeft + DisplayWidth.Of(upToCursor);
            int bufferWidth = Console.BufferWidth;
            int col = offset % bufferWidth;
            int rowOffset = offset / bufferWidth;
            int row = startTop + rowOffset;
            if (row >= Console.BufferHeight) row = Console.BufferHeight - 1;
            Console.SetCursorPosition(col, row);
        }
    }
}
