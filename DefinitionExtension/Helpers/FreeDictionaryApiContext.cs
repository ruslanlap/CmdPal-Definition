// Copyright (c) ruslanlap
// Licensed under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DefinitionExtension.Helpers;

[JsonSerializable(typeof(FreeDictionaryApiResponse))]
[JsonSerializable(typeof(List<DatamuseDefinitionResponse>))]
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true)]
internal partial class FreeDictionaryApiContext : JsonSerializerContext
{
}