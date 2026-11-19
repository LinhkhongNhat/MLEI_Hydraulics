using System;

namespace MLEI_Hydraulics
{
    /// <summary>
    /// Flexible search for the filter boxes. Every word typed must be found in the text,
    /// in the same order, but not next to each other. Case is ignored, and dashes, underscores,
    /// dots and similar marks count as spaces.
    /// Example: "drainage zone 10" finds "Drainage-Level 2-Zone 10_REV01", but "zone 10 drainage" does not.
    /// </summary>
    public static class SearchMatcher
    {
        private static readonly char[] Separators = " \t-_.,;:/\\()[]".ToCharArray();

        public static bool MatchesInOrder(string text, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            string haystack = Normalize(text);
            int position = 0;

            foreach (string word in query.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                int index = haystack.IndexOf(word, position, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return false;

                position = index + word.Length;
            }

            return true;
        }

        /// <summary>Turns separator characters into spaces so "Level 2-Zone 10_REV01" reads as separate words.</summary>
        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(Separators, chars[i]) >= 0)
                    chars[i] = ' ';
            }

            return new string(chars);
        }
    }
}
