using System.Globalization;
using System.Resources;

namespace Payroll.Api.Localization;

/// <summary>Translations of user-facing API messages (D56). Injected, so consumers depend on the abstraction (DIP, D62).</summary>
public interface IApiText
{
    /// <summary>The translated text for a code, formatted with the arguments, or the fallback when the code is unknown.</summary>
    string Error(string? code, IReadOnlyList<object> arguments, string fallback);

    string Error(string code, string fallback);

    /// <summary>Display name of a request property for FluentValidation's built-in messages, or null for its default.</summary>
    string? FieldName(string? propertyName);
}

/// <summary>
/// From Resources/ErrorMessages*.resx and Resources/FieldNames*.resx. The culture is CultureInfo.CurrentUICulture,
/// set per request from Accept-Language by RequestLocalization.
/// </summary>
public sealed class ResxApiText : IApiText
{
    public static readonly string[] SupportedLanguages = ["en", "ar"];

    private static readonly ResourceManager Errors = new("Payroll.Api.Resources.ErrorMessages", typeof(ResxApiText).Assembly);
    private static readonly ResourceManager Fields = new("Payroll.Api.Resources.FieldNames", typeof(ResxApiText).Assembly);

    public string Error(string? code, IReadOnlyList<object> arguments, string fallback)
    {
        var format = code is null ? null : Errors.GetString(code, CultureInfo.CurrentUICulture);
        return format is null ? fallback : string.Format(CultureInfo.CurrentCulture, format, arguments.ToArray());
    }

    public string Error(string code, string fallback) => Error(code, [], fallback);

    public string? FieldName(string? propertyName) =>
        propertyName is null ? null : Fields.GetString(propertyName, CultureInfo.CurrentUICulture);
}
