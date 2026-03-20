// Copyright (c) ruslanlap
// Licensed under the MIT license.

namespace DefinitionExtension.Helpers;

internal enum ScriptType
{
    Latin,
    Cyrillic,
    Cjk,
    Mixed
}

internal static class ScriptDetector
{
    public static ScriptType DetectScript(string text)
    {
        bool hasCyrillic = false, hasCjk = false, hasLatin = false;

        foreach (var c in text)
        {
            if (c >= 0x0400 && c <= 0x04FF) hasCyrillic = true;
            else if ((c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF)) hasCjk = true;
            else if (IsLatinCharacter(c)) hasLatin = true;
        }

        if (hasCyrillic && !hasCjk && !hasLatin) return ScriptType.Cyrillic;
        if (hasCjk && !hasCyrillic && !hasLatin) return ScriptType.Cjk;
        if (hasLatin && !hasCyrillic && !hasCjk) return ScriptType.Latin;
        return ScriptType.Mixed;
    }

    private static bool IsLatinCharacter(char c)
    {
        return (c >= 'a' && c <= 'z')
            || (c >= 'A' && c <= 'Z')
            || (c >= 0x00C0 && c <= 0x024F)
            || (c >= 0x1E00 && c <= 0x1EFF);
    }
}
