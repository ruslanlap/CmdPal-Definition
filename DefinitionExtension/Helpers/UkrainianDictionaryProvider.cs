// Copyright (c) ruslanlap
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace DefinitionExtension.Helpers;

/// <summary>
/// Ukrainian dictionary provider using Ukrainian Wiktionary API (primary) with goroh.pp.ua (fallback).
/// </summary>
internal class UkrainianDictionaryProvider : IDictionaryProvider
{
    private readonly HttpClient _httpClient;
    public string LanguageCode => "uk";
    public string DisplayName => "Українська (Вікісловник + goroh.pp.ua)";

    private const string WiktionaryApiBase = "https://uk.wiktionary.org/w/api.php";
    private const string GorohBaseUrl = "https://goroh.pp.ua/Тлумачення/";

    public UkrainianDictionaryProvider(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<List<DictionaryEntry>> LookupAsync(string word, CancellationToken token)
    {
        Debug.WriteLine($"[UkrainianProvider] Starting lookup for: '{word}'");

        try
        {
            var results = await LookupWiktionaryAsync(word, token);
            if (results.Count > 0)
            {
                Debug.WriteLine($"[UkrainianProvider] Wiktionary returned {results.Sum(e => e.Meanings.Sum(m => m.Definitions.Count))} definitions");
                return results;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UkrainianProvider] Wiktionary failed: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            var results = await LookupGorohAsync(word, token);
            if (results.Count > 0)
            {
                Debug.WriteLine($"[UkrainianProvider] goroh.pp.ua returned {results.Sum(e => e.Meanings.Sum(m => m.Definitions.Count))} definitions");
                return results;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UkrainianProvider] goroh.pp.ua fallback failed: {ex.GetType().Name}: {ex.Message}");
        }

        return new List<DictionaryEntry>();
    }

    #region Wiktionary API (primary)

    private async Task<List<DictionaryEntry>> LookupWiktionaryAsync(string word, CancellationToken token)
    {
        var encodedWord = Uri.EscapeDataString(word);
        var url = $"{WiktionaryApiBase}?action=parse&page={encodedWord}&prop=wikitext&format=json&redirects=1";

        using var response = await _httpClient.GetAsync(url, token);

        if (!response.IsSuccessStatusCode)
            return new List<DictionaryEntry>();

        var json = await response.Content.ReadAsStringAsync(token);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out _))
            return new List<DictionaryEntry>();

        if (!root.TryGetProperty("parse", out var parse) ||
            !parse.TryGetProperty("wikitext", out var wikitext) ||
            !wikitext.TryGetProperty("*", out var wikitextContent))
            return new List<DictionaryEntry>();

        var text = wikitextContent.GetString();
        if (string.IsNullOrEmpty(text))
            return new List<DictionaryEntry>();

        return ParseWikitext(word, text);
    }

    private List<DictionaryEntry> ParseWikitext(string word, string wikitext)
    {
        var ukSection = ExtractUkrainianSection(wikitext);
        if (string.IsNullOrEmpty(ukSection))
            return new List<DictionaryEntry>();

        var entry = new DictionaryEntry
        {
            Word = word,
            SourceUrls = new List<string> { $"https://uk.wiktionary.org/wiki/{Uri.EscapeDataString(word)}" }
        };

        var pos = ExtractPartOfSpeech(ukSection);
        var definitions = ExtractDefinitions(ukSection);

        if (definitions.Count == 0)
            return new List<DictionaryEntry>();

        var meaning = new Meaning
        {
            PartOfSpeech = pos,
            Definitions = definitions
        };

        var synonyms = ExtractRelatedWords(ukSection, "синоніми", "Синоніми");
        if (synonyms.Count > 0)
            meaning.Synonyms = synonyms;

        var antonyms = ExtractRelatedWords(ukSection, "антоніми", "Антоніми");
        if (antonyms.Count > 0)
            meaning.Antonyms = antonyms;

        entry.Meanings.Add(meaning);
        return new List<DictionaryEntry> { entry };
    }

    private string? ExtractUkrainianSection(string wikitext)
    {
        var ukStart = wikitext.IndexOf("{{=uk=}}", StringComparison.Ordinal);
        if (ukStart < 0)
        {
            ukStart = wikitext.IndexOf("== Українська ==", StringComparison.Ordinal);
            if (ukStart < 0)
            {
                if (wikitext.Contains("Значення") || wikitext.Contains("Семантичні"))
                    return wikitext;
                return null;
            }
        }

        var nextLangPatterns = new[] { "{{=", "== " };
        var ukEnd = wikitext.Length;

        foreach (var pattern in nextLangPatterns)
        {
            var idx = wikitext.IndexOf(pattern, ukStart + 10, StringComparison.Ordinal);
            if (idx > 0)
            {
                var lineStart = wikitext.LastIndexOf('\n', idx);
                var line = wikitext.Substring(lineStart + 1, Math.Min(idx - lineStart - 1 + pattern.Length + 5, wikitext.Length - lineStart - 1));
                if (Regex.IsMatch(line.Trim(), @"^(\{\{=[a-z]+=\}\}|==\s*\p{Lu})"))
                {
                    ukEnd = Math.Min(ukEnd, idx);
                }
            }
        }

        return wikitext.Substring(ukStart, ukEnd - ukStart);
    }

    private static string ExtractPartOfSpeech(string section)
    {
        if (Regex.IsMatch(section, @"\{\{імен\s+uk")) return "іменник";
        if (Regex.IsMatch(section, @"\{\{-ння\|")) return "іменник";
        if (section.Contains("Іменник")) return "іменник";
        if (Regex.IsMatch(section, @"\{\{дієсл\s+uk")) return "дієслово";
        if (section.Contains("Дієслово")) return "дієслово";
        if (Regex.IsMatch(section, @"\{\{прикм\s+uk")) return "прикметник";
        if (section.Contains("Прикметник")) return "прикметник";
        if (Regex.IsMatch(section, @"\{\{присл\s+uk")) return "прислівник";
        if (section.Contains("Прислівник")) return "прислівник";
        if (section.Contains("Займенник")) return "займенник";
        if (section.Contains("Числівник")) return "числівник";
        if (section.Contains("Частка")) return "частка";
        if (section.Contains("Сполучник")) return "сполучник";
        if (section.Contains("Прийменник")) return "прийменник";
        if (section.Contains("Вигук")) return "вигук";
        return string.Empty;
    }

    private static List<DefinitionItem> ExtractDefinitions(string section)
    {
        var definitions = new List<DefinitionItem>();
        var znachStart = section.IndexOf("Значення", StringComparison.Ordinal);
        if (znachStart < 0)
            return definitions;

        var afterHeader = section.Substring(znachStart);
        var nextSection = Regex.Match(afterHeader, @"\n===+[^=]");
        var defSection = nextSection.Success
            ? afterHeader.Substring(0, nextSection.Index)
            : afterHeader;

        var lines = defSection.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("#") || trimmed.StartsWith("#*") || trimmed.StartsWith("#:"))
                continue;

            var defText = Regex.Replace(trimmed, @"^#+\s*", "");
            if (string.IsNullOrWhiteSpace(defText))
                continue;

            string? example = null;
            var exampleMatch = Regex.Match(defText, @"\{\{приклад\|([^|}]+)");
            if (exampleMatch.Success)
                example = exampleMatch.Groups[1].Value.Trim();

            defText = CleanWikitext(defText);
            if (string.IsNullOrWhiteSpace(defText))
                continue;

            var defItem = new DefinitionItem { Definition = defText };
            if (!string.IsNullOrEmpty(example))
                defItem.Example = CleanWikitext(example);

            definitions.Add(defItem);
            if (definitions.Count >= 5)
                break;
        }

        return definitions;
    }

    private static List<string> ExtractRelatedWords(string section, string semanticKey, string sectionHeader)
    {
        var words = new List<string>();

        foreach (Match m in Regex.Matches(section, $@"\{{\{{семантика\|[^}}]*{semanticKey}=([^|}}]+)"))
        {
            var items = m.Groups[1].Value.Split(',').Select(s => CleanWikitext(s).Trim())
                .Where(s => !string.IsNullOrEmpty(s));
            words.AddRange(items);
        }

        var headerStart = section.IndexOf(sectionHeader, StringComparison.Ordinal);
        if (headerStart >= 0)
        {
            var afterHeader = section.Substring(headerStart);
            var nextSectionMatch = Regex.Match(afterHeader, @"\n===+[^=]");
            var relSection = nextSectionMatch.Success
                ? afterHeader.Substring(0, nextSectionMatch.Index)
                : afterHeader;

            foreach (var line in relSection.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#"))
                {
                    var text = Regex.Replace(trimmed, @"^#+\s*", "");
                    var cleaned = CleanWikitext(text).Trim();
                    if (!string.IsNullOrEmpty(cleaned) && cleaned != "—")
                    {
                        words.AddRange(cleaned.Split(',').Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s)));
                    }
                }
            }
        }

        return words.Distinct().Take(5).ToList();
    }

    private static string CleanWikitext(string text)
    {
        text = Regex.Replace(text, @"\{\{семантика\|[^}]*\}\}", "");
        text = Regex.Replace(text, @"\{\{приклад\|[^}]*\}\}", "");
        text = Regex.Replace(text, @"\{\{списки семантичних зв'язків\}\}", "");
        text = Regex.Replace(text, @"\{\{(\w+)\.\|uk\}\}", "($1.)");
        text = Regex.Replace(text, @"\{\{позначка\|([^}]+)\}\}", "$1");
        text = Regex.Replace(text, @"\[\[([^|\]]*\|)?([^\]]+)\]\]", "$2");
        text = Regex.Replace(text, @"\{\{[^}]*\}\}", "");
        text = Regex.Replace(text, @"\s+", " ");
        text = text.Trim().TrimEnd(')').Trim();
        return text;
    }

    #endregion

    #region goroh.pp.ua (fallback)

    private async Task<List<DictionaryEntry>> LookupGorohAsync(string word, CancellationToken token)
    {
        var requestUrl = $"{GorohBaseUrl}{Uri.EscapeDataString(word.ToLowerInvariant())}";

        using var response = await _httpClient.GetAsync(requestUrl, token);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new List<DictionaryEntry>();

        if (!response.IsSuccessStatusCode)
            return new List<DictionaryEntry>();

        var html = await response.Content.ReadAsStringAsync(token);

        if (html.Contains("isNotFound: true"))
            return new List<DictionaryEntry>();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        return ParseGorohHtml(word, doc, requestUrl);
    }

    private static List<DictionaryEntry> ParseGorohHtml(string word, HtmlDocument doc, string sourceUrl)
    {
        var articleBlocks = doc.DocumentNode.SelectNodes("//div[contains(@class, 'article-block')]");
        if (articleBlocks == null || articleBlocks.Count == 0)
            return new List<DictionaryEntry>();

        var entry = new DictionaryEntry
        {
            Word = word,
            SourceUrls = new List<string> { sourceUrl }
        };

        foreach (var block in articleBlocks)
        {
            var titleNode = block.SelectSingleNode(".//h2[contains(@class, 'page__sub-header')]//span[contains(@class, 'uppercase')]");
            var wordTitle = HtmlEntity.DeEntitize(titleNode?.InnerText?.Trim() ?? word.ToUpper());
            wordTitle = wordTitle.Replace("\u0301", "");

            var remarkNode = block.SelectSingleNode(".//h2[contains(@class, 'page__sub-header')]//span[contains(@class, 'block-remark')]");
            var pos = ParseGorohPartOfSpeech(remarkNode);

            var formulaNodes = block.SelectNodes(".//span[contains(@class, 'interpret-formula')]");
            if (formulaNodes == null || formulaNodes.Count == 0)
                continue;

            var meaning = new Meaning { PartOfSpeech = pos };

            foreach (var formulaNode in formulaNodes.Take(5))
            {
                var defText = HtmlEntity.DeEntitize(formulaNode.InnerText?.Trim() ?? "");
                defText = defText.Replace("\u0301", "");

                if (!string.IsNullOrWhiteSpace(defText))
                {
                    meaning.Definitions.Add(new DefinitionItem { Definition = defText });
                }
            }

            if (meaning.Definitions.Count > 0)
            {
                entry.Meanings.Add(meaning);
            }
        }

        return entry.Meanings.Count > 0 ? new List<DictionaryEntry> { entry } : new List<DictionaryEntry>();
    }

    private static string ParseGorohPartOfSpeech(HtmlNode? remarkNode)
    {
        if (remarkNode == null) return string.Empty;
        var text = HtmlEntity.DeEntitize(remarkNode.InnerText?.Trim() ?? "").ToLowerInvariant();
        if (text.Contains("імен")) return "іменник";
        if (text.Contains("дієсл")) return "дієслово";
        if (text.Contains("прикм")) return "прикметник";
        if (text.Contains("присл")) return "прислівник";
        if (text.Contains("займ")) return "займенник";
        return text;
    }

    #endregion
}
