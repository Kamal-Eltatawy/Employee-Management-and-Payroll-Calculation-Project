using Payroll.Domain.Common;

namespace Payroll.Application.Common;

public sealed class ConflictException : Exception, ILocalizableError
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    public ConflictException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Errors = errors ?? NoErrors;
        Arguments = [];
    }

    private ConflictException(string code, string message, IReadOnlyDictionary<string, string[]>? errors, object[] arguments)
        : base(message)
    {
        Code = code;
        Errors = errors ?? NoErrors;
        Arguments = arguments;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public string? Code { get; }

    public IReadOnlyList<object> Arguments { get; }

    /// <summary>A conflict with a translatable code; when <paramref name="field"/> is set, the message is also put on that field (D42).</summary>
    public static ConflictException Coded(string code, string message, string? field = null, params object[] arguments) =>
        new(code, message, field is null ? null : new Dictionary<string, string[]> { [field] = [message] }, arguments);

    public static ConflictException ForField(string field, string message) =>
        new(message, new Dictionary<string, string[]> { [field] = [message] });
}
