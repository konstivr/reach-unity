using System.Text;

namespace Reach.Framework.Interaction
{
    /// <summary>
    /// String similarity utilities for fuzzy passphrase matching.
    /// Used by the Gate system to compare what Whisper transcribed
    /// against the expected passphrase.
    /// </summary>
    public static class StringSimilarity
    {
        /// <summary>
        /// Returns true if 'spoken' matches 'expected' above the given similarity threshold (0..1).
        /// Empty strings never match.
        /// </summary>
        public static bool Matches(string spoken, string expected, float threshold01)
        {
            string a = Normalize(spoken);
            string b = Normalize(expected);

            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            if (a == b) return true;
            if (a.Contains(b) || b.Contains(a)) return true;

            float sim = Similarity01(a, b);
            return sim >= threshold01;
        }

        /// <summary>0..1 similarity score, 1 = identical.</summary>
        public static float Similarity01(string a, string b)
        {
            int dist = Levenshtein(a, b);
            int max = a.Length > b.Length ? a.Length : b.Length;
            if (max == 0) return 1f;
            return 1f - (float)dist / max;
        }

        /// <summary>
        /// Normalize: lowercase, strip non-alphanumeric (except whitespace), collapse spaces, trim.
        /// </summary>
        public static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.ToLowerInvariant().Trim();

            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                    sb.Append(c);
            }
            return CollapseSpaces(sb.ToString());
        }

        static string CollapseSpaces(string s)
        {
            var sb = new StringBuilder(s.Length);
            bool lastSpace = false;
            foreach (char c in s)
            {
                bool space = char.IsWhiteSpace(c);
                if (space)
                {
                    if (!lastSpace) sb.Append(' ');
                    lastSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastSpace = false;
                }
            }
            return sb.ToString().Trim();
        }

        static int Levenshtein(string s, string t)
        {
            int n = s.Length, m = t.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            int[] prev = new int[m + 1];
            int[] curr = new int[m + 1];
            for (int j = 0; j <= m; j++) prev[j] = j;

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                char sc = s[i - 1];
                for (int j = 1; j <= m; j++)
                {
                    int cost = (sc == t[j - 1]) ? 0 : 1;
                    int del = prev[j] + 1;
                    int ins = curr[j - 1] + 1;
                    int sub = prev[j - 1] + cost;
                    int min = del < ins ? del : ins;
                    curr[j] = min < sub ? min : sub;
                }
                var tmp = prev; prev = curr; curr = tmp;
            }
            return prev[m];
        }
    }
}