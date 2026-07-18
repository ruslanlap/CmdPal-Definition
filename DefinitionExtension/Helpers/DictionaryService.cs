// Copyright (c) ruslanlap
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DefinitionExtension.Helpers;

public class DictionaryService
{
    private static readonly HttpClient _httpClient = CreateHttpClient();
    private readonly Dictionary<string, List<DictionaryEntry>> _cache = new();
    private const int MaxCacheSize = 100;

    private readonly Dictionary<string, IDictionaryProvider> _providers;
    private readonly SpellCheckService _spellCheckService;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return client;
    }

    public DictionaryService()
    {
        _providers = new Dictionary<string, IDictionaryProvider>(StringComparer.OrdinalIgnoreCase)
        {
            { "en", new EnglishDictionaryProvider(_httpClient) },
            { "fr", new FrenchDictionaryProvider(_httpClient) },
            { "uk", new UkrainianDictionaryProvider(_httpClient) },
            { "zh", new ChineseDictionaryProvider(_httpClient) }
        };
        _spellCheckService = new SpellCheckService(_httpClient);
    }

    /// <summary>
    /// Lookup using script auto-detection and parallel provider queries.
    /// </summary>
    public async Task<List<DictionaryEntry>> LookupAsync(
        string word,
        string apiEndpoint,
        CancellationToken cancellationToken = default,
        string? latinLanguages = null)
    {
        if (string.IsNullOrWhiteSpace(word))
            return new List<DictionaryEntry>();

        var cacheKey = $"{word.ToLowerInvariant()}";

        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var script = ScriptDetector.DetectScript(word);
            var providers = GetProvidersForScript(script, latinLanguages).ToList();

            Debug.WriteLine($"[DictionaryService] Script: {script}, providers: {string.Join(", ", providers.Select(p => p.LanguageCode))}");

            var tasks = providers.Select(async provider =>
            {
                try
                {
                    return await provider.LookupAsync(word.Trim(), cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DictionaryService] Provider {provider.LanguageCode} failed for '{word}': {ex.GetType().Name}: {ex.Message}");
                    return new List<DictionaryEntry>();
                }
            }).ToList();

            var resultsList = await Task.WhenAll(tasks);
            var allEntries = resultsList.SelectMany(e => e ?? Enumerable.Empty<DictionaryEntry>()).ToList();

            // Cache management
            if (_cache.Count >= MaxCacheSize)
            {
                var firstKey = _cache.Keys.First();
                _cache.Remove(firstKey);
            }
            _cache[cacheKey] = allEntries;

            return allEntries;
        }
        catch (OperationCanceledException)
        {
            return new List<DictionaryEntry>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DictionaryService] Lookup error for '{word}': {ex.Message}");
            return new List<DictionaryEntry>();
        }
    }

    private IEnumerable<IDictionaryProvider> GetProvidersForScript(ScriptType script, string? latinLanguages)
    {
        return script switch
        {
            ScriptType.Cyrillic => _providers.Values.Where(p => p.LanguageCode == "uk"),
            ScriptType.Cjk => _providers.Values.Where(p => p.LanguageCode == "zh"),
            ScriptType.Latin => GetLatinProviders(latinLanguages),
            _ => _providers.Values // Mixed — query all
        };
    }

    private IEnumerable<IDictionaryProvider> GetLatinProviders(string? latinLanguages)
    {
        var langs = (latinLanguages ?? "en")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var providers = _providers.Values
            .Where(p => langs.Any(l => string.Equals(l, p.LanguageCode, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return providers.Count > 0 ? providers : _providers.Values.Where(p => p.LanguageCode == "en");
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Returns predictive spelling suggestions for a Latin-script word when no
    /// exact dictionary match is found.  Only fires for Latin-script input.
    /// </summary>
    public async Task<List<string>> GetSpellingSuggestionsAsync(
        string word,
        CancellationToken cancellationToken = default)
    {
        var script = ScriptDetector.DetectScript(word);
        if (script != ScriptType.Latin && script != ScriptType.Mixed)
            return new List<string>();

        return await _spellCheckService.GetSuggestionsAsync(word, cancellationToken);
    }
}
