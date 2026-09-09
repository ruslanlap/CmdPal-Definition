// Copyright (c) ruslanlap
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DefinitionExtension.Helpers;

internal class EnglishDictionaryProvider : IDictionaryProvider
{
    private const string DatamuseDefinitionEndpoint = "https://api.datamuse.com/words?sp=";
    private static readonly TimeSpan MaximumPrimaryTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly string _apiEndpoint;
    private readonly string _fallbackApiEndpoint;

    public string LanguageCode => "en";
    public string DisplayName => "English (FreeDictionaryAPI.com)";

    public EnglishDictionaryProvider(HttpClient httpClient, string apiEndpoint = null, string fallbackApiEndpoint = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiEndpoint = apiEndpoint;
        _fallbackApiEndpoint = fallbackApiEndpoint ?? DatamuseDefinitionEndpoint;
    }

    public async Task<List<DictionaryEntry>> LookupAsync(string word, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(word))
            return new List<DictionaryEntry>();

        try
        {
            using var primaryTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            primaryTokenSource.CancelAfter(MaximumPrimaryTimeout);

            return await LookupConfiguredApiAsync(word, primaryTokenSource.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            Debug.WriteLine($"[EnglishProvider] Primary timed out for '{word}'. Trying Datamuse.");
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[EnglishProvider] Primary failed for '{word}': {ex.Message}. Trying Datamuse.");
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[EnglishProvider] Primary returned invalid JSON for '{word}': {ex.Message}. Trying Datamuse.");
        }

        token.ThrowIfCancellationRequested();
        return await LookupDatamuseAsync(word, token);
    }

    private async Task<List<DictionaryEntry>> LookupConfiguredApiAsync(string word, CancellationToken token)
    {
        var endpoint = _apiEndpoint ?? Settings.DefaultEnglishApiEndpoint;
        var requestUrl = $"{endpoint.TrimEnd('/')}/{Uri.EscapeDataString(word.Trim())}";

        using var response = await _httpClient.GetAsync(requestUrl, token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new List<DictionaryEntry>();

        if (!response.IsSuccessStatusCode)
        {
            var httpEx = new HttpRequestException($"HTTP {(int)response.StatusCode} {response.StatusCode}");
            httpEx.Data["StatusCode"] = response.StatusCode;
            throw httpEx;
        }

        var jsonString = await response.Content.ReadAsStringAsync(token);

        // FreeDictionaryAPI returns { word, entries: [...] }
        // Legacy dictionaryapi.dev returns [ { word, meanings: [...] }, ... ]
        using var doc = JsonDocument.Parse(jsonString);
        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var resp = JsonSerializer.Deserialize(
                jsonString,
                FreeDictionaryApiContext.Default.FreeDictionaryApiResponse);
            return ConvertResponse(resp);
        }

        // Legacy array shape
        var entries = JsonSerializer.Deserialize(
            jsonString,
            DictionaryEntryContext.Default.ListDictionaryEntry);
        return entries ?? new List<DictionaryEntry>();
    }

    private async Task<List<DictionaryEntry>> LookupDatamuseAsync(string word, CancellationToken token)
    {
        var requestUrl = $"{_fallbackApiEndpoint}{Uri.EscapeDataString(word.Trim())}&md=d&max=1";

        using var response = await _httpClient.GetAsync(requestUrl, token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new List<DictionaryEntry>();

        if (!response.IsSuccessStatusCode)
            return new List<DictionaryEntry>();

        var jsonString = await response.Content.ReadAsStringAsync(token);
        var words = JsonSerializer.Deserialize(
            jsonString,
            FreeDictionaryApiContext.Default.ListDatamuseDefinitionResponse);

        var match = words?.FirstOrDefault(result =>
            string.Equals(result.Word, word.Trim(), StringComparison.OrdinalIgnoreCase));

        if (match?.Definitions == null || match.Definitions.Count == 0)
            return new List<DictionaryEntry>();

        var meanings = match.Definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition))
            .Select(ParseDatamuseDefinition)
            .GroupBy(definition => definition.PartOfSpeech, StringComparer.OrdinalIgnoreCase)
            .Select(group => new Meaning
            {
                PartOfSpeech = group.Key,
                Definitions = group.Select(definition => new DefinitionItem
                {
                    Definition = definition.Text
                }).ToList()
            })
            .ToList();

        if (meanings.Count == 0)
            return new List<DictionaryEntry>();

        return new List<DictionaryEntry>
        {
            new DictionaryEntry
            {
                Word = match.Word,
                Meanings = meanings,
                SourceUrls = new List<string> { "https://www.datamuse.com/api/" }
            }
        };
    }

    private static (string PartOfSpeech, string Text) ParseDatamuseDefinition(string definition)
    {
        var parts = definition.Split('\t', 2, StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return ("unknown", definition.Trim());

        var partOfSpeech = parts[0].ToLowerInvariant() switch
        {
            "n" => "noun",
            "v" => "verb",
            "adj" => "adjective",
            "adv" => "adverb",
            "u" => "unknown",
            _ => parts[0]
        };

        return (partOfSpeech, parts[1]);
    }

    private static List<DictionaryEntry> ConvertResponse(FreeDictionaryApiResponse response)
    {
        if (response?.Entries == null || response.Entries.Count == 0)
            return new List<DictionaryEntry>();

        var phonetics = response.Entries
            .Where(entry => entry != null)
            .SelectMany(entry => entry.Pronunciations ?? new List<FreeDictionaryPronunciation>())
            .Where(pronunciation => !string.IsNullOrWhiteSpace(pronunciation?.Text))
            .GroupBy(pronunciation => pronunciation.Text, StringComparer.Ordinal)
            .Select(group => new Phonetic { Text = group.Key })
            .ToList();

        var meanings = response.Entries
            .Where(entry => entry != null)
            .Select(entry => new Meaning
            {
                PartOfSpeech = entry.PartOfSpeech,
                Definitions = FlattenSenses(entry.Senses ?? new List<FreeDictionarySense>())
                    .Where(sense => !string.IsNullOrWhiteSpace(sense.Definition))
                    .Select(sense => new DefinitionItem
                    {
                        Definition = sense.Definition,
                        Example = sense.Examples?.FirstOrDefault(example => !string.IsNullOrWhiteSpace(example)),
                        Synonyms = CleanWords(sense.Synonyms),
                        Antonyms = CleanWords(sense.Antonyms)
                    })
                    .ToList(),
                Synonyms = CleanWords(entry.Synonyms),
                Antonyms = CleanWords(entry.Antonyms)
            })
            .Where(meaning => meaning.Definitions.Count > 0
                || meaning.Synonyms.Count > 0
                || meaning.Antonyms.Count > 0)
            .ToList();

        if (meanings.Count == 0)
            return new List<DictionaryEntry>();

        var dictionaryEntry = new DictionaryEntry
        {
            Word = string.IsNullOrWhiteSpace(response.Word) ? string.Empty : response.Word,
            Phonetic = phonetics.FirstOrDefault()?.Text,
            Phonetics = phonetics,
            Meanings = meanings,
            License = response.Source?.License == null
                ? null
                : new LicenseInfo
                {
                    Name = response.Source.License.Name,
                    Url = response.Source.License.Url
                },
            SourceUrls = string.IsNullOrWhiteSpace(response.Source?.Url)
                ? new List<string>()
                : new List<string> { response.Source.Url }
        };

        return new List<DictionaryEntry> { dictionaryEntry };
    }

    private static IEnumerable<FreeDictionarySense> FlattenSenses(IEnumerable<FreeDictionarySense> senses)
    {
        foreach (var sense in senses ?? Enumerable.Empty<FreeDictionarySense>())
        {
            if (sense == null)
                continue;

            yield return sense;

            foreach (var subsense in FlattenSenses(sense.Subsenses))
                yield return subsense;
        }
    }

    private static List<string> CleanWords(IEnumerable<string> words)
    {
        return (words ?? Enumerable.Empty<string>())
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}