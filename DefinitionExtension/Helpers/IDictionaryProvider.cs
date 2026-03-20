// Copyright (c) ruslanlap
// Licensed under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DefinitionExtension.Helpers;

internal interface IDictionaryProvider
{
    Task<List<DictionaryEntry>> LookupAsync(string word, CancellationToken token);
    string LanguageCode { get; }
    string DisplayName { get; }
}
