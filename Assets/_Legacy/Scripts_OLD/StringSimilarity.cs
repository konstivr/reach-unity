using System;
using System.Text.RegularExpressions;

public static class StringSimilarity
{
    public static bool Matches(string a, string b, float threshold01)
    {
        a = Normalize(a);
        b = Normalize(b);

        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        if (a == b) return true;

        float sim = Similarity01(a, b);
        return sim >= threshold01;
    }

    static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.ToLowerInvariant();
        s = Regex.Replace(s, @"[^\p{L}\p{N}\s]+", " "); // punctuation -> space
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    // Levenshtein similarity ratio
    static float Similarity01(string s1, string s2)
    {
        int dist = Levenshtein(s1, s2);
        int max = Math.Max(s1.Length, s2.Length);
        if (max == 0) return 1f;
        return 1f - (dist / (float)max);
    }

    static int Levenshtein(string s, string t)
    {
        int n = s.Length, m = t.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        for (int j = 1; j <= m; j++)
        {
            int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
            d[i, j] = Math.Min(
                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                d[i - 1, j - 1] + cost
            );
        }

        return d[n, m];
    }
}