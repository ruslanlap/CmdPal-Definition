// Copyright (c) ruslanlap
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace DefinitionExtension.Helpers;

/// <summary>
/// French dictionary provider using Collins French-English Dictionary (primary) with French Wiktionary (fallback).
/// </summary>
internal class FrenchDictionaryProvider : IDictionaryProvider
{
    private readonly HttpClient _httpClient;
    private const string CollinsDictionaryBase = "https://www.collinsdictionary.com/dictionary/french-english/";
    private const string WiktionaryApiBase = "https://fr.wiktionary.org/w/api.php";

    public string LanguageCode => "fr";
    public string DisplayName => "Français (Collins + Wiktionnaire)";

    private static readonly string[] CollinsPartOfSpeechKeywords =
    {
        "noun", "verb", "adjective", "adverb", "pronoun", "preposition",
        "conjunction", "interjection", "determiner", "article", "exclamation",
        "phrase", "auxiliary"
    };

    private static readonly string[] WiktionaryPartOfSpeechKeywords =
    {
        "nom", "verbe", "adjectif", "adverbe", "pronom", "préposition",
        "conjonction", "interjection", "déterminant", "article", "particule",
        "locution", "onomatop"
    };

    public FrenchDictionaryProvider(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<List<DictionaryEntry>> LookupAsync(string word, CancellationToken token)
    {
        var candidates = BuildWordCandidates(word);

        foreach (var candidate in candidates)
        {
            try
            {
                var collinsEntries = await LookupCollinsAsync(candidate, token);
                if (collinsEntries.Count > 0)
                    return collinsEntries;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                Debug.WriteLine($"[FrenchProvider] Collins lookup failed for '{candidate}': {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                var wiktionaryEntries = await LookupWiktionaryAsync(candidate, token);
                if (wiktionaryEntries.Count > 0)
                    return wiktionaryEntries;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                Debug.WriteLine($"[FrenchProvider] Wiktionary lookup failed for '{candidate}': {ex.GetType().Name}: {ex.Message}");
            }
        }

        return new List<DictionaryEntry>();
    }

    private static List<string> BuildWordCandidates(string word)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(word))
            candidates.Add(word);

        var normalizedWord = NormalizeFrenchWord(word);
        if (!string.IsNullOrWhiteSpace(normalizedWord)
            && !string.Equals(normalizedWord, word, StringComparison.Ordinal))
            candidates.Add(normalizedWord);

        return candidates.Distinct(StringComparer.Ordinal).ToList();
    }

    private static string NormalizeFrenchWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return word;
        var normalized = word.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    #region Collins Dictionary (primary)

    private async Task<List<DictionaryEntry>> LookupCollinsAsync(string word, CancellationToken token)
    {
        var requestUrl = $"{CollinsDictionaryBase}{Uri.EscapeDataString(word)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
        request.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        request.Headers.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,fr;q=0.8");

        using var response = await _httpClient.SendAsync(request, token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new List<DictionaryEntry>();

        if (!response.IsSuccessStatusCode)
            return new List<DictionaryEntry>();

        var html = await response.Content.ReadAsStringAsync(token);
        return ParseCollinsHtml(word, requestUrl, html);
    }

    private static List<DictionaryEntry> ParseCollinsHtml(string queryWord, string sourceUrl, string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new List<DictionaryEntry>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var contentNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'content') and contains(@class, 'definitions')]")
            ?? doc.DocumentNode.SelectSingleNode("//main")
            ?? doc.DocumentNode;

        var meanings = ExtractCollinsMeanings(contentNode, queryWord);
        if (meanings.Count == 0)
            return new List<DictionaryEntry>();

        var entry = new DictionaryEntry
        {
            Word = ExtractCollinsWord(contentNode, queryWord),
            SourceUrls = new List<string> { sourceUrl }
        };

        entry.Meanings.AddRange(meanings);
        return new List<DictionaryEntry> { entry };
    }

    private static string ExtractCollinsWord(HtmlNode contentNode, string fallbackWord)
    {
        var titleNode = contentNode.SelectSingleNode(".//h1");
        var titleText = NormalizeHtmlText(titleNode?.InnerText);
        if (!string.IsNullOrWhiteSpace(titleText))
        {
            var quotedWordMatch = Regex.Match(titleText, "[''"](?<word>[^''"]+)[''""]", RegexOptions.IgnoreCase);
            if (quotedWordMatch.Success)
                return quotedWordMatch.Groups["word"].Value.Trim();
        }

        var headingNodes = contentNode.SelectNodes(".//h2");
        if (headingNodes != null)
        {
            foreach (var headingNode in headingNodes)
            {
                var headingText = NormalizeHtmlText(headingNode.InnerText);
                if (IsLikelyHeadword(headingText))
                    return headingText;
            }
        }

        return fallbackWord;
    }

    private static bool IsLikelyHeadword(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 40) return false;
        if (text.StartsWith("Examples of", StringComparison.OrdinalIgnoreCase)
            || text.Contains("translation", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Collins", StringComparison.OrdinalIgnoreCase))
            return false;
        return Regex.IsMatch(text, @"^[\p{L}\p{M}\-\'\u2019\s]+$");
    }

    private static List<Meaning> ExtractCollinsMeanings(HtmlNode contentNode, string lookupWord)
    {
        var meaningMap = new Dictionary<string, List<DefinitionItem>>(StringComparer.OrdinalIgnoreCase);
        var totalDefinitions = 0;

        var definitionNodes = contentNode.SelectNodes(".//div[contains(@class,'hom') or contains(@class,'sense')]//span[contains(@class,'def') or contains(@class,'quote')]")
            ?? contentNode.SelectNodes(".//span[contains(@class,'def') or contains(@class,'quote')]");

        if (definitionNodes == null || definitionNodes.Count == 0)
            return new List<Meaning>();

        foreach (var definitionNode in definitionNodes)
        {
            if (IsInsideExamplesArea(definitionNode))
                continue;

            var definitionText = NormalizeHtmlText(definitionNode.InnerText);
            if (!IsUsableCollinsDefinition(definitionText, lookupWord))
                continue;

            var partOfSpeech = ExtractCollinsPartOfSpeech(definitionNode);
            if (!meaningMap.TryGetValue(partOfSpeech, out var definitions))
            {
                definitions = new List<DefinitionItem>();
                meaningMap[partOfSpeech] = definitions;
            }

            definitions.Add(new DefinitionItem { Definition = definitionText });
            totalDefinitions++;

            if (totalDefinitions >= 10)
                break;
        }

        return meaningMap
            .Where(kvp => kvp.Value.Count > 0)
            .Select(kvp => new Meaning
            {
                PartOfSpeech = kvp.Key,
                Definitions = kvp.Value
            })
            .ToList();
    }

    private static bool IsInsideExamplesArea(HtmlNode node)
    {
        var parent = node.ParentNode;
        while (parent != null)
        {
            var cls = parent.GetAttributeValue("class", "");
            if (cls.Contains("examples", StringComparison.OrdinalIgnoreCase)
                || cls.Contains("thesaurus", StringComparison.OrdinalIgnoreCase))
                return true;
            parent = parent.ParentNode;
        }
        return false;
    }

    private static bool IsUsableCollinsDefinition(string? text, string lookupWord)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 5) return false;
        if (text.Length > 500) return false;
        if (string.Equals(text, lookupWord, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static string ExtractCollinsPartOfSpeech(HtmlNode defNode)
    {
        var ancestor = defNode;
        for (int i = 0; i < 6 && ancestor != null; i++)
        {
            var cls = ancestor.GetAttributeValue("class", "");
            if (cls.Contains("hom") || cls.Contains("sense"))
            {
                var posNode = ancestor.SelectSingleNode(".//span[contains(@class,'pos')]");
                if (posNode != null)
                {
                    var posText = NormalizeHtmlText(posNode.InnerText)?.ToLowerInvariant() ?? "";
                    foreach (var keyword in CollinsPartOfSpeechKeywords)
                    {
                        if (posText.Contains(keyword))
                            return keyword;
                    }
                }
            }
            ancestor = ancestor.ParentNode;
        }
        return string.Empty;
    }

    private static string NormalizeHtmlText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = HtmlEntity.DeEntitize(text);
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    #endregion

    #region French Wiktionary (fallback)

    private async Task<List<DictionaryEntry>> LookupWiktionaryAsync(string word, CancellationToken token)
    {
        var encodedWord = Uri.EscapeDataString(word);
        var url = $"{WiktionaryApiBase}?action=parse&page={encodedWord}&prop=wikitext&format=json&redirects=1";

        using var response = await _httpClient.GetAsync(url, token);

        if (!response.IsSuccessStatusCode)
            return new List<DictionaryEntry>();

        var json = await response.Content.ReadAsStringAsync(token);
        using var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        if (root.TryGetProperty("error", out _))
            return new List<DictionaryEntry>();

        if (!root.TryGetProperty("parse", out var parse) ||
            !parse.TryGetProperty("wikitext", out var wikitext) ||
            !wikitext.TryGetProperty("*", out var wikitextContent))
            return new List<DictionaryEntry>();

        var text = wikitextContent.GetString();
        if (string.IsNullOrEmpty(text))
            return new List<DictionaryEntry>();

        return ParseFrenchWikitext(word, text);
    }

    private static List<DictionaryEntry> ParseFrenchWikitext(string word, string wikitext)
    {
        var frSection = ExtractFrenchSection(wikitext);
        if (string.IsNullOrEmpty(frSection))
            return new List<DictionaryEntry>();

        var entry = new DictionaryEntry
        {
            Word = word,
            SourceUrls = new List<string> { $"https://fr.wiktionary.org/wiki/{Uri.EscapeDataString(word)}" }
        };

        var pos = ExtractWiktionaryPartOfSpeech(frSection);
        var definitions = ExtractWiktionaryDefinitions(frSection);

        if (definitions.Count == 0)
            return new List<DictionaryEntry>();

        entry.Meanings.Add(new Meaning
        {
            PartOfSpeech = pos,
            Definitions = definitions
        });

        return new List<DictionaryEntry> { entry };
    }

    private static string? ExtractFrenchSection(string wikitext)
    {
        var frStart = wikitext.IndexOf("{{langue|fr}}", StringComparison.Ordinal);
        if (frStart < 0)
        {
            frStart = wikitext.IndexOf("== {{langue|fr}} ==", StringComparison.Ordinal);
            if (frStart < 0)
            {
                // If no explicit French section, assume entire page is French
                if (wikitext.Contains("{{S|nom|fr") || wikitext.Contains("{{S|verbe|fr"))
                    return wikitext;
                return null;
            }
        }

        // Find end of French section
        var nextLang = Regex.Match(wikitext.Substring(frStart + 15), @"\{\{langue\|(?!fr)");
        return nextLang.Success
            ? wikitext.Substring(frStart, nextLang.Index)
            : wikitext.Substring(frStart);
    }

    private static string ExtractWiktionaryPartOfSpeech(string section)
    {
        foreach (var keyword in WiktionaryPartOfSpeechKeywords)
        {
            if (section.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return keyword;
        }
        return string.Empty;
    }

    private static List<DefinitionItem> ExtractWiktionaryDefinitions(string section)
    {
        var definitions = new List<DefinitionItem>();
        var lines = section.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("#") || trimmed.StartsWith("#*") || trimmed.StartsWith("#:"))
                continue;

            var defText = Regex.Replace(trimmed, @"^#+\s*", "");
            if (string.IsNullOrWhiteSpace(defText))
                continue;

            string? example = null;
            var exampleMatch = Regex.Match(defText, @"\{\{exemple\|([^|}]+)");
            if (exampleMatch.Success)
                example = exampleMatch.Groups[1].Value.Trim();

            defText = CleanFrenchWikitext(defText);
            if (string.IsNullOrWhiteSpace(defText) || defText.Length < 3)
                continue;

            var defItem = new DefinitionItem { Definition = defText };
            if (!string.IsNullOrEmpty(example))
                defItem.Example = CleanFrenchWikitext(example);

            definitions.Add(defItem);
            if (definitions.Count >= 5)
                break;
        }

        return definitions;
    }

    private static string CleanFrenchWikitext(string text)
    {
        text = Regex.Replace(text, @"\{\{exemple\|[^}]*\}\}", "");
        text = Regex.Replace(text, @"\[\[([^|\]]*\|)?([^\]]+)\]\]", "$2");
        text = Regex.Replace(text, @"\{\{lien\|([^|}]+)[^}]*\}\}", "$1");
        text = Regex.Replace(text, @"\{\{[^}]*\}\}", "");
        text = Regex.Replace(text, @"'{2,}", "");
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    #endregion
}
