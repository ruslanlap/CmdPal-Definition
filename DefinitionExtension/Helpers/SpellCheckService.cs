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
/// Provides predictive spelling suggestions using the Datamuse API.
/// Returns candidate words when the exact word is not found in the dictionary.
/// </summary>
internal class SpellCheckService
{
    private readonly HttpClient _httpClient;
    private const string DatamuseSpellingBase = "https://api.datamuse.com/words?sp=";
    private const int MaxSuggestions = 6;

    public SpellCheckService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Fetches spelling suggestions for the given word from the Datamuse API.
    /// Only returns words that differ from the input (not exact matches).
    /// </summary>
    public async Task<List<string>> GetSuggestionsAsync(string word, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(word))
            return new List<string>();

        try
        {
            var url = $"{DatamuseSpellingBase}{Uri.EscapeDataString(word.Trim())}&max={MaxSuggestions}";
            using var response = await _httpClient.GetAsync(url, token);

            if (!response.IsSuccessStatusCode)
                return new List<string>();

            var json = await response.Content.ReadAsStringAsync(token);
            var words = JsonSerializer.Deserialize(
                json,
                DatamuseContext.Default.ListDatamuseWord);

            return words?
                .Where(w => !string.IsNullOrWhiteSpace(w.Word) &&
                            !string.Equals(w.Word, word, StringComparison.OrdinalIgnoreCase))
                .Select(w => w.Word!)
                .Take(MaxSuggestions)
                .ToList()
                ?? new List<string>();
        }
        catch (OperationCanceledException)
        {
            return new List<string>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SpellCheckService] Error getting suggestions for '{word}': {ex.Message}");
            return new List<string>();
        }
    }
}
