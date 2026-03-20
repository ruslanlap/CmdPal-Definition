// Copyright (c) ruslanlap
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DefinitionExtension.Helpers;

internal class EnglishDictionaryProvider : IDictionaryProvider
{
    private readonly HttpClient _httpClient;
    private const string ApiBase = "https://api.dictionaryapi.dev/api/v2/entries/en/";

    public string LanguageCode => "en";
    public string DisplayName => "English (dictionaryapi.dev)";

    public EnglishDictionaryProvider(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<List<DictionaryEntry>> LookupAsync(string word, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(word))
            return new List<DictionaryEntry>();

        try
        {
            var requestUrl = $"{ApiBase}{Uri.EscapeDataString(word.Trim())}";
            using var response = await _httpClient.GetAsync(requestUrl, token);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new List<DictionaryEntry>();

            if (!response.IsSuccessStatusCode)
                return new List<DictionaryEntry>();

            var jsonString = await response.Content.ReadAsStringAsync(token);
            var entries = JsonSerializer.Deserialize(
                jsonString,
                DictionaryEntryContext.Default.ListDictionaryEntry);

            return entries ?? new List<DictionaryEntry>();
        }
        catch (OperationCanceledException)
        {
            return new List<DictionaryEntry>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EnglishProvider] Lookup error for '{word}': {ex.Message}");
            return new List<DictionaryEntry>();
        }
    }
}
