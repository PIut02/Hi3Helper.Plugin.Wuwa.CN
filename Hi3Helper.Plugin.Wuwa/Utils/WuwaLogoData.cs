using System;
using Hi3Helper.Plugin.Core;

// ReSharper disable IdentifierTypo

namespace Hi3Helper.Plugin.Wuwa.Utils;

/// <summary>
/// Game logo lockups shown in the top-left corner, keyed by the launcher's UI language.
/// <br/><br/>
/// These are embedded instead of being fetched from a CDN on purpose: the assets are tiny, and
/// a CDN based one would have to be region specific (the mainland and the global builds ship
/// their own artwork). They are the very same lockups the official launcher renders, taken from
/// its web resource bundle (<c>krfeapp.dat</c>).
/// </summary>
internal static class WuwaLogoData
{
    /// <summary>
    /// Returns the lockup matching the launcher's current UI language: the Chinese lockups for a
    /// Chinese UI (simplified or traditional) and the latin wordmark for everything else.
    /// </summary>
    internal static ReadOnlySpan<byte> GetLockupPng()
    {
        string localeCode = SharedStatic.PluginLocaleCode;
        if (!localeCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return LatinLockup;
        }

        return IsTraditionalChinese(localeCode) ? TraditionalChineseLockup : SimplifiedChineseLockup;
    }

    private static bool IsTraditionalChinese(ReadOnlySpan<char> localeCode)
    {
        ReadOnlySpan<string> traditionalRegions = ["tw", "hk", "mo", "hant"];
        foreach (string region in traditionalRegions)
        {
            if (localeCode.Contains(region, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // logo-315d94fb.png (6,867 bytes)
    private static readonly byte[] SimplifiedChineseLockup = Convert.FromBase64String(
        "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" +
        "CLGBj9XWbT3W4/Xn+U2AKvG/mFeHsUYi/nAb/aC34hLyVdDt62mI7+ZHSBPweR8MBgc70jUobCa2is3MyqctT2Ca+15KXMYbJeGwFGDJx1b10l6fCFhKMS/y" +
        "vZk1t4/dZ1Z7b/bcBuN3jWB7Bb8g/7FA8a1mE75CbG5sCztrlMMkEOybbS6xjbsuLY2f7osnYsmG1sn47XFpgDRjA9HtDt2m8El7sVi/Urw5gwNd0lQfY2Hc" +
        "HJlw5/CVBrGlJH8sLulSqamxisySjYZIv9x8p3w2bmVX/e8jnUtFvmf3jOtVqIJVfRY4fJj2EmvciYi79116LB5wyxWkQVIpmR28IYeLfdl/DLpCrEFf77k+" +
        "G8l4Q9jFxH44VtcZBuVpyS//xiesWINki/rnAetW3pXzYsw8D74rs5+1qQ01NWBfOa/VAdA9YguCU9x/PiUd22JoDEOqfnsVOlls7dDn3Zf5jQ20DEnLrrGX" +
        "JYyT2LXotsGg7hPqpD2hqneHPQu0kfHSIKmVdMYO3uj21U7Q7I7v3d1fX+D7F57bqFR52ALOmz0I63qftNtnyP46pXet78vg+5SY9ZiuOsD2PzjQRcL3d4fY" +
        "bpXr4movbt2ZrrpWLx0z+VJIN7nWPuusxwc9SzzD8/fdX1Kq+lm5fbZoVzL7FI9rwXzCATLV15I1upXF8/5Xne9XX+PuedV6bOOojl4yF+tOj1mHD4vK+fHc" +
        "5vvklzj+CZJjrRv7zYNTAAAAAElFTkSuQmCC");

    // logo-c77691f0.png (7,037 bytes)
    private static readonly byte[] TraditionalChineseLockup = Convert.FromBase64String(
        "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" +
        "EpGIEHcAAAAASUVORK5CYII=");

    // logo-4ad25a1e.png (13,069 bytes)
    private static readonly byte[] LatinLockup = Convert.FromBase64String(
        "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" +
        "zj/uvtw2OgAAAABJRU5ErkJggg==");

}
