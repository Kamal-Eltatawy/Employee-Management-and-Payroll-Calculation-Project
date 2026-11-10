namespace Payroll.Application.Common;

public sealed class ConflictException : Exception
{
    private static readonly IReadOnlyDictionary<string, string[]> NoErrors = new Dictionary<string, string[]>();

    public ConflictException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
        : base(message)
    {
        Errors = errors ?? NoErrors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public static ConflictException ForField(string field, string message) =>
        new(message, new Dictionary<string, string[]> { [field] = [message] });
}
