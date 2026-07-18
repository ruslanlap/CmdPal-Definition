// Copyright (c) ruslanlap
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DefinitionExtension.Helpers;

/// <summary>
/// Provides word suggestions (predictive spelling) using multiple strategies:
/// 1. Datamuse API (online, supports English & Spanish) — rich suggestions
/// 2. Wiktionary opensearch (online, supports many languages) — prefix-based
/// 3. Local Levenshtein fallback from a small built-in common-word list
/// Results are merged, deduplicated, and ranked by fuzzy similarity score.
/// </summary>
internal class SuggestionService
{
    private readonly HttpClient _httpClient;

    // Language codes supported by Datamuse for spelling suggestions
    private static readonly HashSet<string> DatamuseSupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "es"
    };

    public SuggestionService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Gets word suggestions for the given input, using the specified language.
    /// Returns a ranked list of candidate words that the user might be trying to type.
    /// The original input word is excluded from the results.
    /// </summary>
    public async Task<List<string>> GetSuggestionsAsync(
        string input,
        string language,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length < 2)
            return new List<string>();

        var trimmedInput = input.Trim();

        // Gather suggestions from multiple sources in parallel
        var tasks = new List<Task<List<string>>>();

        // 1. Datamuse API (English & Spanish) — only for Latin/mixed script
        if (DatamuseSupportedLanguages.Contains(language))
        {
            var script = ScriptDetector.DetectScript(trimmedInput);
            if (script == ScriptType.Latin || script == ScriptType.Mixed)
            {
                tasks.Add(GetDatamuseSuggestionsAsync(trimmedInput, cancellationToken));
            }
        }

        // 2. Wiktionary opensearch (many languages)
        tasks.Add(GetWiktionarySuggestionsAsync(trimmedInput, language, cancellationToken));

        // 3. Local common-word fallback
        tasks.Add(Task.FromResult(GetLocalSuggestions(trimmedInput, language)));

        try
        {
            var results = await Task.WhenAll(tasks);
            var allSuggestions = results
                .SelectMany(s => s)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                // Exclude the original word from suggestions
                .Where(s => !string.Equals(s, trimmedInput, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Rank by fuzzy similarity to the input
            return FuzzyMatcher.RankBySimilarity(trimmedInput, allSuggestions, maxResults: 8, minScore: 0.2);
        }
        catch (OperationCanceledException)
        {
            return new List<string>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SuggestionService] Error getting suggestions for '{input}': {ex.Message}");
            return new List<string>();
        }
    }

    #region Datamuse API

    private async Task<List<string>> GetDatamuseSuggestionsAsync(
        string trimmedWord, CancellationToken token)
    {
        try
        {
            // Datamuse spelling suggestion endpoint
            // sp=* (spelled like), max=10
            var url = $"https://api.datamuse.com/words?sp={Uri.EscapeDataString(trimmedWord)}*&max=10&md=s";

            using var response = await _httpClient.GetAsync(url, token);
            if (!response.IsSuccessStatusCode)
                return new List<string>();

            var json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);

            var suggestions = new List<string>();
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("word", out var wordProp))
                {
                    var word = wordProp.GetString();
                    if (!string.IsNullOrWhiteSpace(word))
                        suggestions.Add(word);
                }
            }

            return suggestions;
        }
        catch (OperationCanceledException)
        {
            return new List<string>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SuggestionService] Datamuse error: {ex.Message}");
            return new List<string>();
        }
    }

    #endregion

    #region Wiktionary opensearch

    private async Task<List<string>> GetWiktionarySuggestionsAsync(
        string trimmedWord, string language, CancellationToken token)
    {
        try
        {
            // Use the appropriate Wiktionary language edition
            var wiktionaryDomain = language switch
            {
                "uk" => "uk.wiktionary.org",
                "fr" => "fr.wiktionary.org",
                "zh" => "zh.wiktionary.org",
                "es" => "es.wiktionary.org",
                "de" => "de.wiktionary.org",
                "it" => "it.wiktionary.org",
                "pt-BR" or "pt" => "pt.wiktionary.org",
                "ja" => "ja.wiktionary.org",
                "ko" => "ko.wiktionary.org",
                "tr" => "tr.wiktionary.org",
                "ar" => "ar.wiktionary.org",
                "hi" => "hi.wiktionary.org",
                _ => "en.wiktionary.org"
            };

            var url = $"https://{wiktionaryDomain}/w/api.php?action=opensearch&search={Uri.EscapeDataString(trimmedWord)}&limit=10&namespace=0&format=json";

            using var response = await _httpClient.GetAsync(url, token);
            if (!response.IsSuccessStatusCode)
                return new List<string>();

            var json = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(json);

            // opensearch returns: ["search term", ["title1", "title2", ...], ["desc1", ...], ["url1", ...]]
            if (doc.RootElement.ValueKind == JsonValueKind.Array &&
                doc.RootElement.GetArrayLength() >= 2)
            {
                var titles = doc.RootElement[1];
                var suggestions = new List<string>();
                foreach (var title in titles.EnumerateArray())
                {
                    var t = title.GetString();
                    if (!string.IsNullOrWhiteSpace(t))
                        suggestions.Add(t);
                }
                return suggestions;
            }

            return new List<string>();
        }
        catch (OperationCanceledException)
        {
            return new List<string>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SuggestionService] Wiktionary opensearch error: {ex.Message}");
            return new List<string>();
        }
    }

    #endregion

    #region Local common-word fallback

    // A small built-in list of common English words for offline fallback
    private static readonly string[] CommonEnglishWords =
    {
        "the", "be", "to", "of", "and", "a", "in", "that", "have", "i",
        "it", "for", "not", "on", "with", "he", "as", "you", "do", "at",
        "this", "but", "his", "by", "from", "they", "we", "say", "her", "she",
        "or", "an", "will", "my", "one", "all", "would", "there", "their", "what",
        "so", "up", "out", "if", "about", "who", "get", "which", "go", "me",
        "when", "make", "can", "like", "time", "no", "just", "him", "know", "take",
        "people", "into", "year", "your", "good", "some", "could", "them", "see", "other",
        "than", "then", "now", "look", "only", "come", "its", "over", "think", "also",
        "back", "after", "use", "two", "how", "our", "work", "first", "well", "way",
        "even", "new", "want", "because", "any", "these", "give", "day", "most", "us",
        "hello", "world", "help", "love", "life", "home", "hand", "part", "child", "eye",
        "woman", "place", "case", "point", "government", "company", "number", "group", "problem", "fact"
    };

    // Common Ukrainian words for offline fallback
    private static readonly string[] CommonUkrainianWords =
    {
        "і", "в", "на", "не", "що", "з", "до", "як", "це", "за",
        "від", "у", "про", "для", "його", "бути", "один", "я", "вони", "ми",
        "він", "вона", "все", "якщо", "або", "коли", "так", "там", "тут", "де",
        "дуже", "добре", "слово", "людина", "час", "річ", "світ", "дім", "рука", "очі",
        "любов", "життя", "день", "ніч", "рік", "друг", "син", "донька", "мати", "батько",
        "привіт", "дякую", "може", "треба", "було", "буде", "немає"
    };

    private static List<string> GetLocalSuggestions(string input, string language)
    {
        var wordList = language switch
        {
            "uk" => CommonUkrainianWords,
            _ => CommonEnglishWords
        };

        return FuzzyMatcher.RankBySimilarity(input, wordList, maxResults: 5, minScore: 0.3);
    }

    #endregion
}
