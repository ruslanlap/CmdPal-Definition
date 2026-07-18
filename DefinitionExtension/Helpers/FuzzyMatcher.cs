// Copyright (c) ruslanlap
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace DefinitionExtension.Helpers;

/// <summary>
/// Provides offline fuzzy string matching using Levenshtein distance
/// and prefix-based scoring. Used to rank and filter word suggestions
/// when online suggestion APIs are unavailable or for quick local ranking.
/// </summary>
internal static class FuzzyMatcher
{
    private const int DefaultMaxDistance = 3;
    private const int DefaultMaxResults = 8;

    /// <summary>
    /// Computes the Levenshtein edit distance between two strings.
    /// Uses O(min(a,b)) space optimization.
    /// </summary>
    public static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        // Ensure a is the shorter string for space optimization
        if (a.Length > b.Length) (a, b) = (b, a);

        var prev = new int[a.Length + 1];
        var curr = new int[a.Length + 1];

        for (int i = 0; i <= a.Length; i++)
            prev[i] = i;

        for (int j = 1; j <= b.Length; j++)
        {
            curr[0] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[i] = Math.Min(
                    Math.Min(curr[i - 1] + 1, prev[i] + 1),
                    prev[i - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[a.Length];
    }

    /// <summary>
    /// Returns a normalized similarity score between 0 and 1,
    /// where 1 means identical and 0 means completely different.
    /// </summary>
    public static double GetSimilarityScore(string input, string candidate)
    {
        if (string.IsNullOrEmpty(input) && string.IsNullOrEmpty(candidate))
            return 1.0;
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(candidate))
            return 0.0;

        // Case-insensitive comparison
        input = input.ToLowerInvariant();
        candidate = candidate.ToLowerInvariant();

        // Exact match
        if (input == candidate)
            return 1.0;

        // Prefix match bonus — if candidate starts with input, high score
        if (candidate.StartsWith(input, StringComparison.Ordinal))
        {
            var prefixRatio = (double)input.Length / candidate.Length;
            return 0.5 + 0.5 * prefixRatio; // 0.5..1.0
        }

        // Contains match — input is substring of candidate
        if (candidate.Contains(input, StringComparison.Ordinal))
        {
            var containsRatio = (double)input.Length / candidate.Length;
            return 0.3 + 0.4 * containsRatio; // 0.3..0.7
        }

        // Levenshtein-based score
        int maxLen = Math.Max(input.Length, candidate.Length);
        int distance = LevenshteinDistance(input, candidate);

        // Early exit: if distance is too large, score is very low
        if (distance > DefaultMaxDistance)
            return 0.0;

        return 1.0 - (double)distance / maxLen;
    }

    /// <summary>
    /// Ranks candidates by similarity to the input and returns the top matches.
    /// </summary>
    public static List<string> RankBySimilarity(
        string input,
        IEnumerable<string> candidates,
        int maxResults = DefaultMaxResults,
        double minScore = 0.3)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new List<string>();

        return candidates
            .Select(c => (word: c, score: GetSimilarityScore(input, c)))
            .Where(x => x.score >= minScore)
            .OrderByDescending(x => x.score)
            .Take(maxResults)
            .Select(x => x.word)
            .ToList();
    }

    /// <summary>
    /// Returns true if the input could be a misspelling of the candidate.
    /// Uses a distance threshold proportional to word length.
    /// </summary>
    public static bool IsLikelyMisspelling(string input, string candidate)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(candidate))
            return false;

        input = input.ToLowerInvariant();
        candidate = candidate.ToLowerInvariant();

        if (input == candidate)
            return false;

        int maxLen = Math.Max(input.Length, candidate.Length);
        int threshold = maxLen <= 3 ? 1 : maxLen <= 6 ? 2 : 3;

        return LevenshteinDistance(input, candidate) <= threshold;
    }
}
