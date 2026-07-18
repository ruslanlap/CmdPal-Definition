// Copyright (c) ruslanlap
// Licensed under the MIT license.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.Resources;
using DefinitionExtension.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DefinitionExtension.Pages;

internal sealed partial class DefinitionPage : DynamicListPage
{
    private List<IListItem> _items = new();
    private readonly DictionaryService _dictionaryService;
    private readonly SettingsManager _settingsManager;
    private CancellationTokenSource? _currentSearchCts;
    private string _lastSearch = string.Empty;
    private bool _isQueryRunning;

    private readonly IconInfo _logoIcon = new("\uE82D");
    private static readonly ResourceLoader _resourceLoader = new();

    public DefinitionPage(SettingsManager settingsManager, DictionaryService dictionaryService)
    {
        _settingsManager = settingsManager;
        _dictionaryService = dictionaryService;
        settingsManager.ExtensionHomePage = this;

        Icon = _logoIcon;
        Title = _resourceLoader.GetString("AppTitle");
        Name = "Open";
        ShowDetails = true;
        PlaceholderText = _resourceLoader.GetString("PlaceholderText");

        ReloadExtensionState();
    }

    public void ReloadExtensionState()
    {
        var langName = _settingsManager.Language switch
        {
            "en" => "English",
            "uk" => "Українська",
            "zh" => "中文",
            "fr" => "French",

            _ => "English"
        };

        Title = $"{_resourceLoader.GetString("AppTitle")} ({langName})";
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = _logoIcon,
            Title = _resourceLoader.GetString("WordDefinitionLookup"),
            Subtitle = string.Format(_resourceLoader.GetString("TypeWordToSearch"), langName),
        };
        RaiseItemsChanged(_items.Count);
    }

    public override async void UpdateSearchText(string oldSearch, string newSearch)
    {
        try
        {
            if (_lastSearch == newSearch)
                return;

            _lastSearch = newSearch;
            var trimmedSearch = newSearch.Trim();

            if (string.IsNullOrEmpty(trimmedSearch))
            {
                _items.Clear();
                RaiseItemsChanged(0);
                return;
            }

            // Debounce: wait briefly before sending the request
            _currentSearchCts?.Cancel();
            _currentSearchCts = new CancellationTokenSource();
            var token = _currentSearchCts.Token;

            await Task.Delay(300, token);

            if (token.IsCancellationRequested)
                return;

            await UpdateListAsync(trimmedSearch, token);
        }
        catch (OperationCanceledException)
        {
            // Expected from debouncing
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[Definition CmdPal] Network error: {ex.Message}");
            HandleError(_resourceLoader.GetString("Error"), _resourceLoader.GetString("NetworkError"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Definition CmdPal] Search error: {ex.Message}");
            HandleError(_resourceLoader.GetString("Error"), _resourceLoader.GetString("UnexpectedError"));
        }
        finally
        {
            IsLoading = false;
            _isQueryRunning = false;
        }
    }

    private async Task UpdateListAsync(string word, CancellationToken cancellationToken)
    {
        if (_isQueryRunning)
            return;

        _isQueryRunning = true;
        _items.Clear();
        IsLoading = true;
        RaiseItemsChanged(0);

        var entries = await _dictionaryService.LookupAsync(
            word,
            _settingsManager.ApiEndpoint,
            cancellationToken,
            _settingsManager.LatinLanguages);

        if (cancellationToken.IsCancellationRequested)
            return;

        if (entries.Count == 0)
        {
            // Fetch predictive spelling suggestions for Latin-script words
            var suggestions = await _dictionaryService.GetSpellingSuggestionsAsync(word, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return;

            if (suggestions.Count > 0)
            {
                foreach (var suggestion in suggestions)
                    _items.Add(new SuggestionListItem(suggestion, this));

                IsLoading = false;
                _isQueryRunning = false;
                Title = string.Format(_resourceLoader.GetString("SuggestionsForWord"), suggestions[0]);
                RaiseItemsChanged(_items.Count);
                return;
            }

            HandleError(
                string.Format(_resourceLoader.GetString("NoDefinitionsFor"), word),
                _resourceLoader.GetString("CheckSpelling"));
            return;
        }

        var items = BuildResultItems(entries, word);
        _items.Clear();
        _items.AddRange(items);
        IsLoading = false;
        _isQueryRunning = false;
        Title = string.Format(_resourceLoader.GetString("ResultsTitle"), word, _items.Count);
        RaiseItemsChanged(_items.Count);
    }

    private List<DefinitionListItem> BuildResultItems(
        List<DictionaryEntry> entries,
        string searchWord)
    {
        var items = new List<DefinitionListItem>();

        foreach (var entry in entries.Where(e => e != null))
        {
            // Get phonetic info
            var phoneticText = entry.Phonetics?
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p?.Text))?.Text
                ?? entry.Phonetic;

            var audioUrl = entry.Phonetics?
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p?.Audio))?.Audio;

            var sourceUrl = entry.SourceUrls?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

            // Word header with phonetics
            var headerTitle = entry.Word;
            if (_settingsManager.ShowPhonetics && !string.IsNullOrWhiteSpace(phoneticText))
            {
                headerTitle = $"{entry.Word}  {phoneticText}";
            }

            foreach (var meaning in entry.Meanings?.Where(m => m != null) ?? Enumerable.Empty<Meaning>())
            {
                var partOfSpeech = meaning.PartOfSpeech ?? "unknown";
                var posTag = new Tag(partOfSpeech);

                // Definitions
                var definitions = meaning.Definitions?
                    .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Definition))
                    .Take(_settingsManager.MaxResultsPerMeaning)
                    ?? Enumerable.Empty<DefinitionItem>();

                foreach (var def in definitions)
                {
                    items.Add(new DefinitionListItem(
                        title: $"{headerTitle} ({partOfSpeech})",
                        subtitle: def.Definition,
                        itemType: DefinitionItemType.Definition,
                        textToCopy: def.Definition,
                        audioUrl: audioUrl,
                        sourceUrl: sourceUrl,
                        word: entry.Word,
                        tags: new[] { posTag }));

                    // Examples
                    if (_settingsManager.ShowExamples && !string.IsNullOrWhiteSpace(def.Example))
                    {
                        items.Add(new DefinitionListItem(
                            title: $"Example ({partOfSpeech})",
                            subtitle: $"\"{def.Example}\"",
                            itemType: DefinitionItemType.Example,
                            textToCopy: def.Example,
                            sourceUrl: sourceUrl,
                            word: entry.Word,
                            tags: new[] { new Tag("example") }));
                    }
                }

                // Synonyms
                if (_settingsManager.ShowSynonyms)
                {
                    var synonyms = meaning.Synonyms?
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();

                    if (synonyms?.Count > 0)
                    {
                        var synonymsText = string.Join(", ", synonyms);
                        items.Add(new DefinitionListItem(
                            title: $"Synonyms ({partOfSpeech})",
                            subtitle: synonymsText,
                            itemType: DefinitionItemType.Synonym,
                            textToCopy: synonymsText,
                            sourceUrl: sourceUrl,
                            word: entry.Word,
                            tags: new[] { new Tag("synonyms") }));
                    }
                }

                // Antonyms
                if (_settingsManager.ShowAntonyms)
                {
                    var antonyms = meaning.Antonyms?
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .ToList();

                    if (antonyms?.Count > 0)
                    {
                        var antonymsText = string.Join(", ", antonyms);
                        items.Add(new DefinitionListItem(
                            title: $"Antonyms ({partOfSpeech})",
                            subtitle: antonymsText,
                            itemType: DefinitionItemType.Antonym,
                            textToCopy: antonymsText,
                            sourceUrl: sourceUrl,
                            word: entry.Word,
                            tags: new[] { new Tag("antonyms") }));
                    }
                }
            }
        }

        return items;
    }

    private void HandleError(string title, string message)
    {
        IsLoading = false;
        _isQueryRunning = false;
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = new IconInfo("\uE946"),
            Title = title,
            Subtitle = message,
        };
        _items.Clear();
        RaiseItemsChanged(0);
    }

    /// <summary>
    /// Triggers a definition lookup for the given word. Used by spelling suggestion
    /// items so the user can click a suggestion and immediately see its definition.
    /// </summary>
    public void LookupWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return;

        var trimmed = word.Trim();
        _currentSearchCts?.Cancel();
        _currentSearchCts = new CancellationTokenSource();
        _isQueryRunning = false;
        _lastSearch = trimmed;

        _ = UpdateListAsync(trimmed, _currentSearchCts.Token);
    }

    public override IListItem[] GetItems() => _items.ToArray();
}
