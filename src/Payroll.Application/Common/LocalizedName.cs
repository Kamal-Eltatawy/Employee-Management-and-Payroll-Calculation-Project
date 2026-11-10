using System.Globalization;

namespace Payroll.Application.Common;

/// <summary>
/// Master data has an English name and an optional Arabic one (D58). The request language
/// (CultureInfo.CurrentUICulture, set from Accept-Language) decides which one the API returns.
/// </summary>
public static class LocalizedName
{
    public static string Pick(string name, string? nameAr) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" && !string.IsNullOrWhiteSpace(nameAr) ? nameAr : name;
}
