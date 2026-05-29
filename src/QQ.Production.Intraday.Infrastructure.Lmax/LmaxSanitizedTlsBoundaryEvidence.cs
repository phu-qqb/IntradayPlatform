namespace QQ.Production.Intraday.Infrastructure.Lmax;

public sealed record LmaxSanitizedTlsBoundaryEvidence(
    bool TlsAttempted,
    bool TlsSucceeded,
    string TlsBoundaryStatus,
    string TlsResultCategory,
    string? TlsFailureCategory,
    bool TlsTimedOut,
    string? TlsExceptionCategory,
    bool TlsStreamAvailableForFix,
    bool TlsRawMaterialSerialized)
{
    public static LmaxSanitizedTlsBoundaryEvidence NotAttempted { get; } = new(
        TlsAttempted: false,
        TlsSucceeded: false,
        "NotAttempted",
        "NotAttempted",
        null,
        TlsTimedOut: false,
        null,
        TlsStreamAvailableForFix: false,
        TlsRawMaterialSerialized: false);
}

public static class LmaxSanitizedTlsBoundaryClassifier
{
    public static IReadOnlyList<string> SupportedCategories { get; } =
    [
        "Succeeded",
        "AttemptedOnly",
        "Timeout",
        "HandshakeException",
        "CertificateValidationFailure",
        "StreamUnavailable",
        "CancelledOrAborted",
        "UnknownFailure",
        "NotAttempted"
    ];

    public static LmaxSanitizedTlsBoundaryEvidence Classify(LmaxReadOnlyBoundaryStepResult result)
    {
        var status = result.Status.ToString();
        if (result.Status == LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted)
        {
            return LmaxSanitizedTlsBoundaryEvidence.NotAttempted with
            {
                TlsFailureCategory = NormalizeFailureCategory(result.SanitizedErrorCategory)
            };
        }

        if (result.Succeeded)
        {
            return new LmaxSanitizedTlsBoundaryEvidence(
                TlsAttempted: true,
                TlsSucceeded: true,
                status,
                "Succeeded",
                null,
                TlsTimedOut: false,
                null,
                TlsStreamAvailableForFix: true,
                TlsRawMaterialSerialized: false);
        }

        var failureCategory = NormalizeFailureCategory(result.SanitizedErrorCategory);
        var resultCategory = failureCategory switch
        {
            "Timeout" => "Timeout",
            "CertificateValidationFailure" => "CertificateValidationFailure",
            "StreamUnavailable" => "StreamUnavailable",
            "CancelledOrAborted" => "CancelledOrAborted",
            "HandshakeException" => "HandshakeException",
            "AttemptedOnly" => "AttemptedOnly",
            _ => "UnknownFailure"
        };

        return new LmaxSanitizedTlsBoundaryEvidence(
            TlsAttempted: true,
            TlsSucceeded: false,
            status,
            resultCategory,
            failureCategory,
            TlsTimedOut: resultCategory == "Timeout",
            ExceptionCategory(resultCategory),
            TlsStreamAvailableForFix: false,
            TlsRawMaterialSerialized: false);
    }

    private static string? ExceptionCategory(string resultCategory)
        => resultCategory switch
        {
            "Timeout" => "Timeout",
            "HandshakeException" => "HandshakeException",
            "CertificateValidationFailure" => "CertificateValidationFailure",
            "StreamUnavailable" => "StreamUnavailable",
            "CancelledOrAborted" => "CancelledOrAborted",
            "UnknownFailure" => "UnknownFailure",
            _ => null
        };

    private static string? NormalizeFailureCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        if (category.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "Timeout";
        }

        if (category.Contains("Certificate", StringComparison.OrdinalIgnoreCase) ||
            category.Contains("Validation", StringComparison.OrdinalIgnoreCase))
        {
            return "CertificateValidationFailure";
        }

        if (category.Contains("StreamUnavailable", StringComparison.OrdinalIgnoreCase) ||
            category.Contains("TcpBoundaryNotOpened", StringComparison.OrdinalIgnoreCase) ||
            category.Contains("Stream", StringComparison.OrdinalIgnoreCase))
        {
            return "StreamUnavailable";
        }

        if (category.Contains("Cancel", StringComparison.OrdinalIgnoreCase) ||
            category.Contains("Abort", StringComparison.OrdinalIgnoreCase))
        {
            return "CancelledOrAborted";
        }

        if (category.Contains("AttemptedOnly", StringComparison.OrdinalIgnoreCase))
        {
            return "AttemptedOnly";
        }

        if (category.Contains("Handshake", StringComparison.OrdinalIgnoreCase) ||
            category.Contains("Authentication", StringComparison.OrdinalIgnoreCase))
        {
            return "HandshakeException";
        }

        return "UnknownFailure";
    }
}
