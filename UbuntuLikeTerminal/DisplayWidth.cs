using System;

namespace UbuntuLikeTerminal
{
    /// <summary>
    /// Computes the on-screen column width of text, treating full-width
    /// (CJK / Hiragana / Katakana / full-width punctuation, etc.) characters
    /// as occupying 2 columns and everything else as 1 column.
    /// This is needed so the cursor lands in the correct screen column when
    /// the line contains Japanese text.
    /// </summary>
    public static class DisplayWidth
    {
        public static int Of(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int width = 0;
            for (int i = 0; i < text.Length; i++)
            {
                width += CharWidth(text[i]);
            }
            return width;
        }

        public static int CharWidth(char c)
        {
            // Control characters have no width in our editor (we never render them raw)
            if (char.IsControl(c)) return 0;

            int code = c;

            // Common full-width ranges (approximation of Unicode East Asian Width = W/F)
            if ((code >= 0x1100 && code <= 0x115F) ||  // Hangul Jamo
                (code >= 0x2E80 && code <= 0xA4CF && code != 0x303F) || // CJK Radicals .. Yi
                (code >= 0xAC00 && code <= 0xD7A3) ||  // Hangul Syllables
                (code >= 0xF900 && code <= 0xFAFF) ||  // CJK Compatibility Ideographs
                (code >= 0xFE30 && code <= 0xFE4F) ||  // CJK Compatibility Forms
                (code >= 0xFF00 && code <= 0xFF60) ||  // Fullwidth forms
                (code >= 0xFFE0 && code <= 0xFFE6))    // Fullwidth signs
            {
                return 2;
            }

            return 1;
        }
    }
}
