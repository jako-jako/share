using System.Collections.Generic;
using System.Text;

namespace UbuntuLikeTerminal
{
    public static class Tokenizer
    {
        /// <summary>Splits a command line into tokens, honoring "double quoted sections" as a single token.</summary>
        public static List<string> Tokenize(string line)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(line)) return tokens;

            var current = new StringBuilder();
            bool inQuotes = false;
            bool hasToken = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    hasToken = true;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (hasToken)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                        hasToken = false;
                    }
                    continue;
                }

                current.Append(c);
                hasToken = true;
            }

            if (hasToken) tokens.Add(current.ToString());
            return tokens;
        }
    }
}
