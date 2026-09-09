// Copyright (c) ruslanlap
// Licensed under the MIT license.

namespace DefinitionExtension.Helpers;

internal static class ApiSettings
{
    public const string DefaultEnglishApiEndpoint = "https://freedictionaryapi.com/api/v1/entries/en/";
    public const string LegacyEnglishApiEndpoint = "https://api.dictionaryapi.dev/api/v2/entries/en/";

    public static string NormalizeEnglishApiEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) ||
            string.Equals(
                endpoint.Trim().TrimEnd('/'),
                LegacyEnglishApiEndpoint.TrimEnd('/'),
                System.StringComparison.OrdinalIgnoreCase))
        {
            return DefaultEnglishApiEndpoint;
        }

        return endpoint.Trim();
    }
}