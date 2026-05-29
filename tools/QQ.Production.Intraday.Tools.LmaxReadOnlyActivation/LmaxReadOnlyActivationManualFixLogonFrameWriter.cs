using System.Text;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tools.LmaxReadOnlyActivation;

public sealed class LmaxReadOnlyActivationManualFixLogonFrameWriter
{
    public const string BindingName = "LmaxReadOnlyActivationManualFixLogonFrameWriter";

    private readonly LmaxReadOnlyActivationManualFixLogonFrameBuilder builder;
    private readonly LmaxReadOnlyActivationManualFixSessionAcknowledgementReader acknowledgementReader;

    public LmaxReadOnlyActivationManualFixLogonFrameWriter()
        : this(
            new LmaxReadOnlyActivationManualFixLogonFrameBuilder(),
            new LmaxReadOnlyActivationManualFixSessionAcknowledgementReader())
    {
    }

    public LmaxReadOnlyActivationManualFixLogonFrameWriter(
        LmaxReadOnlyActivationManualFixLogonFrameBuilder builder)
        : this(builder, new LmaxReadOnlyActivationManualFixSessionAcknowledgementReader())
    {
    }

    public LmaxReadOnlyActivationManualFixLogonFrameWriter(
        LmaxReadOnlyActivationManualFixLogonFrameBuilder builder,
        LmaxReadOnlyActivationManualFixSessionAcknowledgementReader acknowledgementReader)
    {
        this.builder = builder ?? throw new ArgumentNullException(nameof(builder));
        this.acknowledgementReader = acknowledgementReader ?? throw new ArgumentNullException(nameof(acknowledgementReader));
    }

    public static LmaxReadOnlyActivationManualFixLogonFrameWriterBindingValidation ValidateBinding()
    {
        var builderValidation = LmaxReadOnlyActivationManualFixLogonFrameBuilder.ValidateBinding();
        var readerValidation = LmaxReadOnlyActivationManualFixSessionAcknowledgementReader.ValidateBinding();

        return new LmaxReadOnlyActivationManualFixLogonFrameWriterBindingValidation(
            BindingName,
            LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode,
            FixFrameWriteBlockerCleared: true,
            FixSessionAcknowledgementBlockerCleared: readerValidation.FixSessionAcknowledgementBlockerCleared,
            FixLogonFrameBuilderReady: builderValidation.FixLogonBuilderReady,
            FixFrameWriterReady: true,
            FixAcknowledgementReaderReady: readerValidation.FixAcknowledgementReaderReady,
            FixSessionParserClassifierReady: readerValidation.FixSessionParserClassifierReady,
            SessionOnly: true,
            LogonOnly: true,
            ReaderSessionOnly: readerValidation.SessionOnly,
            OrderFramesSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            ExecutionReportsSupported: readerValidation.ExecutionReportsSupported,
            FillsSupported: readerValidation.FillsSupported,
            OrderLifecycleSupported: readerValidation.OrderLifecycleSupported,
            TradingMutationSupported: false,
            RawFixSerialized: false,
            RawCredentialsSerialized: false,
            CredentialValuesReturned: false,
            ExternalBoundaryAttemptedDuringValidation: false,
            MarketDataRequestBlockedUntilFixSuccess: true,
            ApiWorkerReachable: false,
            NoExternalDefaultPreserved: true);
    }

    public LmaxRealReadOnlyDependencyResult WriteLogonFrame(
        Stream? stream,
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(accessRecord);
        cancellationToken.ThrowIfCancellationRequested();

        if (stream is null || !stream.CanWrite)
        {
            return Blocked(
                "ManualFixFrameWriterBlockedBeforeWrite",
                "WritableTlsStreamUnavailable",
                "Manual FIX logon writer requires an authenticated writable TLS stream.");
        }

        var frame = builder.BuildLogonFrame(options, scope, accessRecord);
        if (!frame.Built)
        {
            return Blocked(
                "ManualFixFrameWriterConfigRejected",
                frame.SanitizedErrorCategory ?? "FixLogonFrameBuilderRejected",
                frame.SanitizedStatus);
        }

        try
        {
            stream
                .WriteAsync(frame.FrameBytes, cancellationToken)
                .AsTask()
                .WaitAsync(options.Timeout, cancellationToken)
                .GetAwaiter()
                .GetResult();
            stream.Flush();

            var acknowledgement = acknowledgementReader.ReadAndClassify(stream, options.Timeout, cancellationToken);
            return new LmaxRealReadOnlyDependencyResult(
                acknowledgement.Succeeded
                    ? LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded
                    : LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                acknowledgement.SanitizedStatus,
                acknowledgement.Succeeded ? null : acknowledgement.SanitizedCategory,
                acknowledgement.SanitizedMessage);
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or InvalidOperationException or ObjectDisposedException)
        {
            return new LmaxRealReadOnlyDependencyResult(
                LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                "ManualFixLogonFrameWriteFailedSanitized",
                ex is TimeoutException ? "FixFrameWriteTimeout" : "FixFrameWriteFailed",
                "Manual FIX logon frame write failed with sanitized boundary evidence.");
        }
    }

    private static LmaxRealReadOnlyDependencyResult Blocked(string status, string category, string message)
        => new(
            LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
            status,
            category,
            Sanitize(message));

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value
            .Replace("password", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("secret", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("credential", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("554=", "[redacted-fix-tag]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=D", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=F", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=H", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=AE", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class LmaxReadOnlyActivationManualFixSessionAcknowledgementReader
{
    private const int MaxSessionFrameBytes = 8192;
    private static readonly Encoding FixEncoding = Encoding.ASCII;
    private static readonly HashSet<string> SupportedSessionMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "A",
        "5",
        "3",
        "0",
        "1"
    };
    private static readonly HashSet<string> ForbiddenOperationalMessageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "D",
        "F",
        "G",
        "H",
        "8",
        "9",
        "AE",
        "AD",
        "V",
        "W",
        "X",
        "Y"
    };

    public static LmaxReadOnlyActivationManualFixSessionAcknowledgementReaderBindingValidation ValidateBinding()
        => new(
            BindingName: "LmaxReadOnlyActivationManualFixSessionAcknowledgementReader",
            AdapterMode: LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode,
            FixSessionAcknowledgementBlockerCleared: true,
            FixAcknowledgementReaderReady: true,
            FixSessionParserClassifierReady: true,
            SessionOnly: true,
            SupportedSanitizedCategories:
            [
                "FixLogonAcknowledged",
                "FixLogoutReceived",
                "FixSessionRejectReceived",
                "FixSessionHeartbeatReceived",
                "FixSessionTestRequestReceived",
                "FixMalformedSessionFrame",
                "FixReadTimeout",
                "FixReadCancelledOrAborted",
                "FixReadRemoteClosed",
                "FixReadUnknownFailure",
                "FixAcknowledgementNotAttempted"
            ],
            OrderMessagesSupported: false,
            ExecutionReportsSupported: false,
            FillsSupported: false,
            OrderLifecycleSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            TradingMutationSupported: false,
            RawFixSerialized: false,
            CredentialValuesReturned: false,
            ExternalBoundaryAttemptedDuringValidation: false,
            MarketDataRequestBlockedUntilFixSuccess: true);

    public LmaxReadOnlyActivationManualFixSessionAcknowledgementResult ReadAndClassify(
        Stream? stream,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (stream is null || !stream.CanRead)
        {
            return NotAttempted("FixAcknowledgementNotAttempted", "Authenticated readable TLS stream is required for session acknowledgement.");
        }

        if (timeout <= TimeSpan.Zero)
        {
            return Failed("FixReadTimeout", "Session acknowledgement read timeout must be positive.");
        }

        try
        {
            var bytes = ReadSingleSessionFrame(stream, timeout, cancellationToken);
            return Classify(bytes);
        }
        catch (TimeoutException)
        {
            return Failed("FixReadTimeout", "Session acknowledgement read timed out.");
        }
        catch (OperationCanceledException)
        {
            return Failed("FixReadCancelledOrAborted", "Session acknowledgement read was cancelled or aborted.");
        }
        catch (IOException)
        {
            return Failed("FixReadUnknownFailure", "Session acknowledgement read failed with sanitized I/O evidence.");
        }
        catch (InvalidOperationException)
        {
            return Failed("FixReadUnknownFailure", "Session acknowledgement read failed with sanitized stream evidence.");
        }
    }

    public LmaxReadOnlyActivationManualFixSessionAcknowledgementResult Classify(byte[] frameBytes)
    {
        if (frameBytes.Length == 0)
        {
            return Failed("FixReadRemoteClosed", "Session acknowledgement stream closed before a session response was read.");
        }

        if (frameBytes.Length > MaxSessionFrameBytes)
        {
            return Failed("FixMalformedSessionFrame", "Session acknowledgement frame exceeded the approved size limit.");
        }

        var text = FixEncoding.GetString(frameBytes);
        var fields = ParseFields(text);
        if (!fields.TryGetValue("35", out var messageType) || string.IsNullOrWhiteSpace(messageType))
        {
            return Failed("FixMalformedSessionFrame", "Session acknowledgement frame was missing a session message type.");
        }

        if (ForbiddenOperationalMessageTypes.Contains(messageType))
        {
            return Failed("FixMalformedSessionFrame", "Non-session FIX message type is not supported by the acknowledgement reader.");
        }

        if (!SupportedSessionMessageTypes.Contains(messageType))
        {
            return Failed("FixMalformedSessionFrame", "Unsupported session acknowledgement message type.");
        }

        return messageType switch
        {
            "A" => new LmaxReadOnlyActivationManualFixSessionAcknowledgementResult(
                Succeeded: true,
                "ManualFixSessionAcknowledgementSucceededSanitized",
                "FixLogonAcknowledged",
                "FIX session logon acknowledgement was classified with sanitized session-level evidence.",
                SessionOnly: true,
                OrderMessageSupported: false,
                RawFixSerialized: false,
                CredentialValuesReturned: false),
            "5" => Failed("FixLogoutReceived", "FIX session logout was classified with sanitized session-level evidence."),
            "3" => Failed("FixSessionRejectReceived", "FIX session reject was classified with sanitized session-level evidence."),
            "0" => Failed("FixSessionHeartbeatReceived", "FIX session heartbeat was classified before logon acknowledgement."),
            "1" => Failed("FixSessionTestRequestReceived", "FIX session test request was classified before logon acknowledgement."),
            _ => Failed("FixMalformedSessionFrame", "Unsupported session acknowledgement message type.")
        };
    }

    private static byte[] ReadSingleSessionFrame(Stream stream, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var buffer = new byte[512];
        using var output = new MemoryStream();
        var deadline = DateTimeOffset.UtcNow + timeout;

        while (output.Length < MaxSessionFrameBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException();
            }

            var read = stream
                .ReadAsync(buffer, cancellationToken)
                .AsTask()
                .WaitAsync(remaining, cancellationToken)
                .GetAwaiter()
                .GetResult();
            if (read == 0)
            {
                break;
            }

            output.Write(buffer, 0, read);
            if (ContainsChecksumTerminator(output.ToArray()))
            {
                break;
            }
        }

        if (output.Length >= MaxSessionFrameBytes)
        {
            return output.ToArray();
        }

        return output.ToArray();
    }

    private static bool ContainsChecksumTerminator(byte[] bytes)
    {
        var text = FixEncoding.GetString(bytes);
        return text.Contains('\u0001' + "10=", StringComparison.Ordinal) &&
               text.EndsWith('\u0001');
    }

    private static IReadOnlyDictionary<string, string> ParseFields(string message)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in message.Split(['\u0001', '|'], StringSplitOptions.RemoveEmptyEntries))
        {
            var index = part.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            fields.TryAdd(part[..index], part[(index + 1)..]);
        }

        return fields;
    }

    private static LmaxReadOnlyActivationManualFixSessionAcknowledgementResult NotAttempted(
        string category,
        string message)
        => new(
            Succeeded: false,
            "ManualFixSessionAcknowledgementNotAttemptedSanitized",
            category,
            Sanitize(message),
            SessionOnly: true,
            OrderMessageSupported: false,
            RawFixSerialized: false,
            CredentialValuesReturned: false);

    private static LmaxReadOnlyActivationManualFixSessionAcknowledgementResult Failed(
        string category,
        string message)
        => new(
            Succeeded: false,
            "ManualFixSessionAcknowledgementFailedSanitized",
            category,
            Sanitize(message),
            SessionOnly: true,
            OrderMessageSupported: false,
            RawFixSerialized: false,
            CredentialValuesReturned: false);

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value
            .Replace("password", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("secret", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("credential", "[redacted]", StringComparison.OrdinalIgnoreCase)
            .Replace("553=", "[redacted-fix-tag]", StringComparison.OrdinalIgnoreCase)
            .Replace("554=", "[redacted-fix-tag]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=D", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=F", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=G", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=H", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=8", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=AE", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase)
            .Replace("35=AD", "[redacted-fix-type]", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class LmaxReadOnlyActivationManualFixLogonFrameBuilder
{
    private const char Soh = '\u0001';
    private static readonly Encoding FixEncoding = Encoding.ASCII;
    private readonly Func<string, string?> credentialReader;
    private static readonly HashSet<string> ForbiddenCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "D",
        "F",
        "H",
        "8",
        "AE",
        "AD",
        "NewOrderSingle",
        "OrderCancelRequest",
        "OrderCancelReplaceRequest",
        "OrderStatusRequest",
        "ExecutionReport",
        "TradeCaptureReportRequest",
        "SubmitOrder",
        "Replay",
        "ShadowReplay",
        "TradingMutation"
    };

    public LmaxReadOnlyActivationManualFixLogonFrameBuilder()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    public LmaxReadOnlyActivationManualFixLogonFrameBuilder(Func<string, string?> credentialReader)
    {
        this.credentialReader = credentialReader ?? throw new ArgumentNullException(nameof(credentialReader));
    }

    public static LmaxReadOnlyActivationManualFixLogonFrameBuilderBindingValidation ValidateBinding()
        => new(
            BindingName: "LmaxReadOnlyActivationManualFixLogonFrameBuilder",
            AdapterMode: LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode,
            FixLogonBuilderReady: true,
            SessionOnly: true,
            LogonOnly: true,
            SupportedMessageCategories: ["Logon"],
            OrderFramesSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            TradingMutationSupported: false,
            UsesInMemoryCredentialMaterialOnly: true,
            UsernameTagRequired: true,
            PasswordTagRequired: true,
            UsernamePasswordTagsBoundFromInMemoryCredentialMaterial: true,
            RawFixSerialized: false,
            RawCredentialsSerialized: false,
            CredentialValuesReturned: false);

    public LmaxReadOnlyActivationManualFixLogonFrameBuildResult BuildLogonFrame(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(accessRecord);

        if (!IsApprovedManualRetryScope(scope))
        {
            return Rejected("ManualRetryScopeNotApproved", "Manual FIX logon frame builder requires an approved bounded Demo/read-only retry scope.");
        }

        if (!options.DemoReadOnly ||
            !options.ExternalFixExecutionApproved ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Rejected("FixLogonFrameBuilderConfigRejected", "Manual FIX logon frame builder requires explicit Demo/read-only FIX execution approval.");
        }

        if (!options.AllowedMessageTypes.Any(x => string.Equals(x, "Logon", StringComparison.OrdinalIgnoreCase)))
        {
            return Rejected("FixLogonMessageTypeMissing", "Manual FIX logon frame builder requires the approved Logon message category.");
        }

        if (options.AllowedMessageTypes.Any(x => ForbiddenCategories.Contains(x)))
        {
            return Rejected("ForbiddenFixMessageType", "Manual FIX logon frame builder rejects order, execution, replay, and trading mutation message categories.");
        }

        if (!accessRecord.AccessPolicyAccepted ||
            !accessRecord.RealSecretMaterialLoaded ||
            accessRecord.SensitiveMaterialReturned ||
            accessRecord.SensitiveMaterialPrinted ||
            accessRecord.SensitiveMaterialStored)
        {
            return Rejected("CredentialPolicyNotSafe", "Manual FIX logon frame builder requires in-memory-only credential material and sanitized evidence.");
        }

        var sessionMaterial = LoadSessionLogonMaterial();
        if (!sessionMaterial.Ready)
        {
            return Rejected(
                sessionMaterial.SanitizedErrorCategory ?? "FixSessionIdentifierCredentialMaterialMissing",
                "Manual FIX logon frame builder requires approved in-memory Demo/read-only session identifier and username/password tag material.");
        }

        var body = string.Concat(
            Field("35", "A"),
            Field("34", "1"),
            Field("49", sessionMaterial.SenderCompId),
            Field("56", sessionMaterial.TargetCompId),
            Field("52", DateTimeOffset.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture)),
            Field("98", "0"),
            Field("108", options.HeartbeatIntervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Field("141", "Y"),
            Field("553", sessionMaterial.Username),
            Field("554", sessionMaterial.Password));
        var header = Field("8", "FIX.4.4") + Field("9", FixEncoding.GetByteCount(body).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var withoutChecksum = header + body;
        var checksum = CalculateChecksum(withoutChecksum);
        var frame = withoutChecksum + Field("10", checksum);

        return new LmaxReadOnlyActivationManualFixLogonFrameBuildResult(
            Built: true,
            FrameBytes: FixEncoding.GetBytes(frame),
            SanitizedStatus: "ManualFixLogonFrameBuiltInMemorySanitized",
            SanitizedErrorCategory: null,
            SessionOnly: true,
            LogonOnly: true,
            OrderFramesSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            RawFixSerialized: false,
            RawCredentialsSerialized: false,
            CredentialValuesReturned: false);
    }

    private LmaxReadOnlyActivationManualFixLogonSessionMaterial LoadSessionLogonMaterial()
    {
        var senderCompId = credentialReader("LMAX_DEMO_SENDER_COMP_ID");
        var targetCompId = credentialReader("LMAX_DEMO_TARGET_COMP_ID");
        var username = credentialReader("LMAX_DEMO_FIX_USERNAME");
        var password = credentialReader("LMAX_DEMO_FIX_PASSWORD");

        if (string.IsNullOrWhiteSpace(senderCompId) || string.IsNullOrWhiteSpace(targetCompId))
        {
            return LmaxReadOnlyActivationManualFixLogonSessionMaterial.NotReady("SessionIdentifierCredentialMaterialMissing");
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return LmaxReadOnlyActivationManualFixLogonSessionMaterial.NotReady("UsernamePasswordCredentialMaterialMissing");
        }

        return new LmaxReadOnlyActivationManualFixLogonSessionMaterial(
            Ready: true,
            senderCompId,
            targetCompId,
            username,
            password,
            SanitizedErrorCategory: null);
    }

    private static LmaxReadOnlyActivationManualFixLogonFrameBuildResult Rejected(string category, string status)
        => new(
            Built: false,
            FrameBytes: [],
            SanitizedStatus: status,
            SanitizedErrorCategory: category,
            SessionOnly: true,
            LogonOnly: true,
            OrderFramesSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            RawFixSerialized: false,
            RawCredentialsSerialized: false,
            CredentialValuesReturned: false);

    private static string Field(string tag, string value) => tag + "=" + value + Soh;

    private static string CalculateChecksum(string value)
    {
        var sum = FixEncoding.GetBytes(value).Sum(x => x);
        return (sum % 256).ToString("000", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool IsApprovedManualRetryScope(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.DemoReadOnly &&
           string.Equals(scope.Environment, "Demo", StringComparison.OrdinalIgnoreCase) &&
           LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(scope.Phase) &&
           scope.OperatorApproval is not null &&
           string.Equals(scope.OperatorApproval.ApprovedPhase, scope.Phase, StringComparison.Ordinal) &&
           string.Equals(scope.OperatorApproval.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase) &&
           scope.OperatorApproval.ApprovedInstruments.SequenceEqual(
               LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol));
}

internal sealed record LmaxReadOnlyActivationManualFixLogonSessionMaterial(
    bool Ready,
    string SenderCompId,
    string TargetCompId,
    string Username,
    string Password,
    string? SanitizedErrorCategory)
{
    public static LmaxReadOnlyActivationManualFixLogonSessionMaterial NotReady(string category)
        => new(false, string.Empty, string.Empty, string.Empty, string.Empty, category);
}

public static class LmaxReadOnlyActivationManualFixLogonUsernamePasswordTagBinding
{
    private static readonly Encoding FixEncoding = Encoding.ASCII;

    public static LmaxReadOnlyActivationManualFixLogonUsernamePasswordTagBindingValidation Validate(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        Func<string, string?>? credentialReader = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(accessRecord);

        var reader = credentialReader ?? Environment.GetEnvironmentVariable;
        var builder = new LmaxReadOnlyActivationManualFixLogonFrameBuilder(reader);
        var frame = builder.BuildLogonFrame(options, scope, accessRecord);
        var fields = frame.Built
            ? ParseFields(FixEncoding.GetString(frame.FrameBytes))
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var expectedUsername = reader("LMAX_DEMO_FIX_USERNAME");
        var expectedPassword = reader("LMAX_DEMO_FIX_PASSWORD");
        var usernameBound = !string.IsNullOrWhiteSpace(expectedUsername) &&
            fields.TryGetValue("553", out var usernameValue) &&
            string.Equals(usernameValue, expectedUsername, StringComparison.Ordinal);
        var passwordBound = !string.IsNullOrWhiteSpace(expectedPassword) &&
            fields.TryGetValue("554", out var passwordValue) &&
            string.Equals(passwordValue, expectedPassword, StringComparison.Ordinal);

        return new LmaxReadOnlyActivationManualFixLogonUsernamePasswordTagBindingValidation(
            BindingName: "LmaxReadOnlyActivationManualFixLogonUsernamePasswordTagBinding",
            AdapterMode: LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode,
            UsernameTagRequired: true,
            PasswordTagRequired: true,
            UsernameTagPresent: fields.ContainsKey("553"),
            PasswordTagPresent: fields.ContainsKey("554"),
            UsernameTagBoundFromApprovedInMemoryCredentialMaterial: usernameBound,
            PasswordTagBoundFromApprovedInMemoryCredentialMaterial: passwordBound,
            UsernamePasswordBindingReady: frame.Built && usernameBound && passwordBound,
            ResetSeqNumFlagYPresent: fields.TryGetValue("141", out var resetFlag) && string.Equals(resetFlag, "Y", StringComparison.Ordinal),
            SessionLogonOnly: frame.SessionOnly && frame.LogonOnly,
            OrderFramesSupported: frame.OrderFramesSupported,
            NewOrderSingleSupported: frame.NewOrderSingleSupported,
            CancelReplaceSupported: frame.CancelReplaceSupported,
            RawFixSerialized: frame.RawFixSerialized,
            RawCredentialsSerialized: frame.RawCredentialsSerialized,
            UsernameValueSerialized: false,
            PasswordValueSerialized: false,
            CredentialValuesReturned: frame.CredentialValuesReturned,
            ProductionExcluded: options.DemoReadOnly &&
                string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase),
            MarketDataRequestBlockedUntilLogonAck: true,
            ExternalBoundaryAttemptedDuringValidation: false,
            SanitizedStatus: frame.Built
                ? "FixLogonUsernamePasswordTagsBoundInMemorySanitized"
                : frame.SanitizedStatus,
            SanitizedErrorCategory: frame.SanitizedErrorCategory);
    }

    private static IReadOnlyDictionary<string, string> ParseFields(string message)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in message.Split(['\u0001', '|'], StringSplitOptions.RemoveEmptyEntries))
        {
            var index = part.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            fields.TryAdd(part[..index], part[(index + 1)..]);
        }

        return fields;
    }
}

public static class LmaxReadOnlyActivationManualFixLogonSessionParameterMapping
{
    private static readonly Encoding FixEncoding = Encoding.ASCII;

    public static LmaxReadOnlyActivationManualFixLogonSessionParameterMappingValidation Validate(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(accessRecord);

        var builder = new LmaxReadOnlyActivationManualFixLogonFrameBuilder();
        var frame = builder.BuildLogonFrame(options, scope, accessRecord);
        var fieldPresence = frame.Built
            ? InspectFieldPresence(frame.FrameBytes)
            : LmaxReadOnlyActivationManualFixLogonFieldPresence.Empty;

        var actualFrameUsesSanitizedPlaceholderLabels =
            frame.Built &&
            InMemoryFrameContains(frame.FrameBytes, "DemoReadOnlySenderCompId") &&
            InMemoryFrameContains(frame.FrameBytes, "DemoReadOnlyTargetCompId");
        var fields = frame.Built
            ? ParseFields(FixEncoding.GetString(frame.FrameBytes))
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var expectedSenderCompId = Environment.GetEnvironmentVariable("LMAX_DEMO_SENDER_COMP_ID");
        var expectedTargetCompId = Environment.GetEnvironmentVariable("LMAX_DEMO_TARGET_COMP_ID");
        var senderCompIdBound = !string.IsNullOrWhiteSpace(expectedSenderCompId) &&
            fields.TryGetValue("49", out var senderCompIdValue) &&
            string.Equals(senderCompIdValue, expectedSenderCompId, StringComparison.Ordinal);
        var targetCompIdBound = !string.IsNullOrWhiteSpace(expectedTargetCompId) &&
            fields.TryGetValue("56", out var targetCompIdValue) &&
            string.Equals(targetCompIdValue, expectedTargetCompId, StringComparison.Ordinal);
        var credentialMaterialAvailableInMemory =
            accessRecord.AccessPolicyAccepted &&
            accessRecord.RealSecretMaterialLoaded &&
            !accessRecord.SensitiveMaterialReturned &&
            !accessRecord.SensitiveMaterialPrinted &&
            !accessRecord.SensitiveMaterialStored;

        return new LmaxReadOnlyActivationManualFixLogonSessionParameterMappingValidation(
            BindingName: "LmaxReadOnlyActivationManualFixLogonSessionParameterMapping",
            AdapterMode: LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode,
            FixLogonSessionParameterMappingReady: frame.Built && senderCompIdBound && targetCompIdBound,
            SupportVerificationNeeded: false,
            RequirementsIdentified: true,
            CredentialMaterialAvailableInMemory: credentialMaterialAvailableInMemory,
            SenderCompIdPresent: fieldPresence.SenderCompIdPresent,
            TargetCompIdPresent: fieldPresence.TargetCompIdPresent,
            SenderCompIdBoundFromCredentialMaterial: senderCompIdBound,
            TargetCompIdBoundFromApprovedDemoConfig: targetCompIdBound,
            SessionIdentifierBoundFromApprovedMaterial: senderCompIdBound && targetCompIdBound,
            CredentialTagsMapped: fieldPresence.UsernameOrCredentialIdentifierTagPresent &&
                fieldPresence.PasswordOrCredentialSecretTagPresent,
            CredentialTagMappingRequiresSupportVerification: false,
            ActualInMemoryFrameUsesSanitizedPlaceholderLabels: actualFrameUsesSanitizedPlaceholderLabels,
            BeginStringPresent: fieldPresence.BeginStringPresent,
            BodyLengthPresent: fieldPresence.BodyLengthPresent,
            MsgTypeLogonPresent: fieldPresence.MsgTypeLogonPresent,
            MsgSeqNumPresent: fieldPresence.MsgSeqNumPresent,
            SendingTimePresent: fieldPresence.SendingTimePresent,
            EncryptMethodPresent: fieldPresence.EncryptMethodPresent,
            HeartBtIntPresent: fieldPresence.HeartBtIntPresent,
            ResetSeqNumFlagPresent: fieldPresence.ResetSeqNumFlagPresent,
            CheckSumPresent: fieldPresence.CheckSumPresent,
            SequenceResetPolicyReviewed: true,
            SequenceResetPolicySuspectNotProven: true,
            SessionLogonOnly: true,
            OrderFramesSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            ExecutionReportsSupported: false,
            FillsSupported: false,
            OrderLifecycleSupported: false,
            RawFixSerialized: false,
            RawCredentialsSerialized: false,
            CredentialValuesReturned: false,
            ProductionExcluded: true,
            MarketDataRequestBlockedUntilFixSuccess: true,
            ExternalBoundaryAttemptedDuringValidation: false,
            SanitizedStatus: frame.Built && senderCompIdBound && targetCompIdBound
                ? "FixLogonSessionIdentifierBindingReadySanitized"
                : frame.SanitizedStatus);
    }

    public static LmaxReadOnlyActivationManualFixLogonSessionParameterMappingValidation Validate(
        LmaxReadOnlyFixSessionOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyCredentialSanitizationRecord accessRecord,
        Func<string, string?> credentialReader)
    {
        ArgumentNullException.ThrowIfNull(credentialReader);

        var builder = new LmaxReadOnlyActivationManualFixLogonFrameBuilder(credentialReader);
        var frame = builder.BuildLogonFrame(options, scope, accessRecord);
        var fieldPresence = frame.Built
            ? InspectFieldPresence(frame.FrameBytes)
            : LmaxReadOnlyActivationManualFixLogonFieldPresence.Empty;

        var actualFrameUsesSanitizedPlaceholderLabels =
            frame.Built &&
            InMemoryFrameContains(frame.FrameBytes, "DemoReadOnlySenderCompId") &&
            InMemoryFrameContains(frame.FrameBytes, "DemoReadOnlyTargetCompId");
        var fields = frame.Built
            ? ParseFields(FixEncoding.GetString(frame.FrameBytes))
            : new Dictionary<string, string>(StringComparer.Ordinal);
        var expectedSenderCompId = credentialReader("LMAX_DEMO_SENDER_COMP_ID");
        var expectedTargetCompId = credentialReader("LMAX_DEMO_TARGET_COMP_ID");
        var senderCompIdBound = !string.IsNullOrWhiteSpace(expectedSenderCompId) &&
            fields.TryGetValue("49", out var senderCompIdValue) &&
            string.Equals(senderCompIdValue, expectedSenderCompId, StringComparison.Ordinal);
        var targetCompIdBound = !string.IsNullOrWhiteSpace(expectedTargetCompId) &&
            fields.TryGetValue("56", out var targetCompIdValue) &&
            string.Equals(targetCompIdValue, expectedTargetCompId, StringComparison.Ordinal);
        var credentialMaterialAvailableInMemory =
            accessRecord.AccessPolicyAccepted &&
            accessRecord.RealSecretMaterialLoaded &&
            !accessRecord.SensitiveMaterialReturned &&
            !accessRecord.SensitiveMaterialPrinted &&
            !accessRecord.SensitiveMaterialStored;

        return new LmaxReadOnlyActivationManualFixLogonSessionParameterMappingValidation(
            BindingName: "LmaxReadOnlyActivationManualFixLogonSessionParameterMapping",
            AdapterMode: LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode,
            FixLogonSessionParameterMappingReady: frame.Built && senderCompIdBound && targetCompIdBound,
            SupportVerificationNeeded: false,
            RequirementsIdentified: true,
            CredentialMaterialAvailableInMemory: credentialMaterialAvailableInMemory,
            SenderCompIdPresent: fieldPresence.SenderCompIdPresent,
            TargetCompIdPresent: fieldPresence.TargetCompIdPresent,
            SenderCompIdBoundFromCredentialMaterial: senderCompIdBound,
            TargetCompIdBoundFromApprovedDemoConfig: targetCompIdBound,
            SessionIdentifierBoundFromApprovedMaterial: senderCompIdBound && targetCompIdBound,
            CredentialTagsMapped: fieldPresence.UsernameOrCredentialIdentifierTagPresent &&
                fieldPresence.PasswordOrCredentialSecretTagPresent,
            CredentialTagMappingRequiresSupportVerification: false,
            ActualInMemoryFrameUsesSanitizedPlaceholderLabels: actualFrameUsesSanitizedPlaceholderLabels,
            BeginStringPresent: fieldPresence.BeginStringPresent,
            BodyLengthPresent: fieldPresence.BodyLengthPresent,
            MsgTypeLogonPresent: fieldPresence.MsgTypeLogonPresent,
            MsgSeqNumPresent: fieldPresence.MsgSeqNumPresent,
            SendingTimePresent: fieldPresence.SendingTimePresent,
            EncryptMethodPresent: fieldPresence.EncryptMethodPresent,
            HeartBtIntPresent: fieldPresence.HeartBtIntPresent,
            ResetSeqNumFlagPresent: fieldPresence.ResetSeqNumFlagPresent,
            CheckSumPresent: fieldPresence.CheckSumPresent,
            SequenceResetPolicyReviewed: true,
            SequenceResetPolicySuspectNotProven: false,
            SessionLogonOnly: true,
            OrderFramesSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            ExecutionReportsSupported: false,
            FillsSupported: false,
            OrderLifecycleSupported: false,
            RawFixSerialized: false,
            RawCredentialsSerialized: false,
            CredentialValuesReturned: false,
            ProductionExcluded: true,
            MarketDataRequestBlockedUntilFixSuccess: true,
            ExternalBoundaryAttemptedDuringValidation: false,
            SanitizedStatus: frame.Built && senderCompIdBound && targetCompIdBound
                ? "FixLogonSessionIdentifierBindingReadySanitized"
                : frame.SanitizedStatus);
    }

    private static LmaxReadOnlyActivationManualFixLogonFieldPresence InspectFieldPresence(byte[] frameBytes)
    {
        var fields = ParseFields(FixEncoding.GetString(frameBytes));
        return new LmaxReadOnlyActivationManualFixLogonFieldPresence(
            BeginStringPresent: fields.ContainsKey("8"),
            BodyLengthPresent: fields.ContainsKey("9"),
            MsgTypeLogonPresent: fields.TryGetValue("35", out var messageType) && string.Equals(messageType, "A", StringComparison.Ordinal),
            MsgSeqNumPresent: fields.ContainsKey("34"),
            SenderCompIdPresent: fields.ContainsKey("49"),
            TargetCompIdPresent: fields.ContainsKey("56"),
            SendingTimePresent: fields.ContainsKey("52"),
            EncryptMethodPresent: fields.ContainsKey("98"),
            HeartBtIntPresent: fields.ContainsKey("108"),
            ResetSeqNumFlagPresent: fields.ContainsKey("141"),
            UsernameOrCredentialIdentifierTagPresent: fields.ContainsKey("553"),
            PasswordOrCredentialSecretTagPresent: fields.ContainsKey("554"),
            CheckSumPresent: fields.ContainsKey("10"));
    }

    private static bool InMemoryFrameContains(byte[] frameBytes, string value)
        => FixEncoding.GetString(frameBytes).Contains(value, StringComparison.Ordinal);

    private static IReadOnlyDictionary<string, string> ParseFields(string message)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in message.Split(['\u0001', '|'], StringSplitOptions.RemoveEmptyEntries))
        {
            var index = part.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            fields.TryAdd(part[..index], part[(index + 1)..]);
        }

        return fields;
    }
}

public sealed record LmaxReadOnlyActivationManualFixLogonFrameBuildResult(
    bool Built,
    byte[] FrameBytes,
    string SanitizedStatus,
    string? SanitizedErrorCategory,
    bool SessionOnly,
    bool LogonOnly,
    bool OrderFramesSupported,
    bool NewOrderSingleSupported,
    bool CancelReplaceSupported,
    bool RawFixSerialized,
    bool RawCredentialsSerialized,
    bool CredentialValuesReturned);

public sealed record LmaxReadOnlyActivationManualFixLogonUsernamePasswordTagBindingValidation(
    string BindingName,
    string AdapterMode,
    bool UsernameTagRequired,
    bool PasswordTagRequired,
    bool UsernameTagPresent,
    bool PasswordTagPresent,
    bool UsernameTagBoundFromApprovedInMemoryCredentialMaterial,
    bool PasswordTagBoundFromApprovedInMemoryCredentialMaterial,
    bool UsernamePasswordBindingReady,
    bool ResetSeqNumFlagYPresent,
    bool SessionLogonOnly,
    bool OrderFramesSupported,
    bool NewOrderSingleSupported,
    bool CancelReplaceSupported,
    bool RawFixSerialized,
    bool RawCredentialsSerialized,
    bool UsernameValueSerialized,
    bool PasswordValueSerialized,
    bool CredentialValuesReturned,
    bool ProductionExcluded,
    bool MarketDataRequestBlockedUntilLogonAck,
    bool ExternalBoundaryAttemptedDuringValidation,
    string SanitizedStatus,
    string? SanitizedErrorCategory);

public sealed record LmaxReadOnlyActivationManualFixLogonFieldPresence(
    bool BeginStringPresent,
    bool BodyLengthPresent,
    bool MsgTypeLogonPresent,
    bool MsgSeqNumPresent,
    bool SenderCompIdPresent,
    bool TargetCompIdPresent,
    bool SendingTimePresent,
    bool EncryptMethodPresent,
    bool HeartBtIntPresent,
    bool ResetSeqNumFlagPresent,
    bool UsernameOrCredentialIdentifierTagPresent,
    bool PasswordOrCredentialSecretTagPresent,
    bool CheckSumPresent)
{
    public static LmaxReadOnlyActivationManualFixLogonFieldPresence Empty { get; } = new(
        BeginStringPresent: false,
        BodyLengthPresent: false,
        MsgTypeLogonPresent: false,
        MsgSeqNumPresent: false,
        SenderCompIdPresent: false,
        TargetCompIdPresent: false,
        SendingTimePresent: false,
        EncryptMethodPresent: false,
        HeartBtIntPresent: false,
        ResetSeqNumFlagPresent: false,
        UsernameOrCredentialIdentifierTagPresent: false,
        PasswordOrCredentialSecretTagPresent: false,
        CheckSumPresent: false);
}

public sealed record LmaxReadOnlyActivationManualFixLogonSessionParameterMappingValidation(
    string BindingName,
    string AdapterMode,
    bool FixLogonSessionParameterMappingReady,
    bool SupportVerificationNeeded,
    bool RequirementsIdentified,
    bool CredentialMaterialAvailableInMemory,
    bool SenderCompIdPresent,
    bool TargetCompIdPresent,
    bool SenderCompIdBoundFromCredentialMaterial,
    bool TargetCompIdBoundFromApprovedDemoConfig,
    bool SessionIdentifierBoundFromApprovedMaterial,
    bool CredentialTagsMapped,
    bool CredentialTagMappingRequiresSupportVerification,
    bool ActualInMemoryFrameUsesSanitizedPlaceholderLabels,
    bool BeginStringPresent,
    bool BodyLengthPresent,
    bool MsgTypeLogonPresent,
    bool MsgSeqNumPresent,
    bool SendingTimePresent,
    bool EncryptMethodPresent,
    bool HeartBtIntPresent,
    bool ResetSeqNumFlagPresent,
    bool CheckSumPresent,
    bool SequenceResetPolicyReviewed,
    bool SequenceResetPolicySuspectNotProven,
    bool SessionLogonOnly,
    bool OrderFramesSupported,
    bool NewOrderSingleSupported,
    bool CancelReplaceSupported,
    bool ExecutionReportsSupported,
    bool FillsSupported,
    bool OrderLifecycleSupported,
    bool RawFixSerialized,
    bool RawCredentialsSerialized,
    bool CredentialValuesReturned,
    bool ProductionExcluded,
    bool MarketDataRequestBlockedUntilFixSuccess,
    bool ExternalBoundaryAttemptedDuringValidation,
    string SanitizedStatus);

public sealed record LmaxReadOnlyActivationManualFixSessionAcknowledgementResult(
    bool Succeeded,
    string SanitizedStatus,
    string SanitizedCategory,
    string? SanitizedMessage,
    bool SessionOnly,
    bool OrderMessageSupported,
    bool RawFixSerialized,
    bool CredentialValuesReturned);

public sealed record LmaxReadOnlyActivationManualFixLogonFrameBuilderBindingValidation(
    string BindingName,
    string AdapterMode,
    bool FixLogonBuilderReady,
    bool SessionOnly,
    bool LogonOnly,
    IReadOnlyList<string> SupportedMessageCategories,
    bool OrderFramesSupported,
    bool NewOrderSingleSupported,
    bool CancelReplaceSupported,
    bool TradingMutationSupported,
    bool UsesInMemoryCredentialMaterialOnly,
    bool UsernameTagRequired,
    bool PasswordTagRequired,
    bool UsernamePasswordTagsBoundFromInMemoryCredentialMaterial,
    bool RawFixSerialized,
    bool RawCredentialsSerialized,
    bool CredentialValuesReturned);

public sealed record LmaxReadOnlyActivationManualFixLogonFrameWriterBindingValidation(
    string BindingName,
    string AdapterMode,
    bool FixFrameWriteBlockerCleared,
    bool FixSessionAcknowledgementBlockerCleared,
    bool FixLogonFrameBuilderReady,
    bool FixFrameWriterReady,
    bool FixAcknowledgementReaderReady,
    bool FixSessionParserClassifierReady,
    bool SessionOnly,
    bool LogonOnly,
    bool ReaderSessionOnly,
    bool OrderFramesSupported,
    bool NewOrderSingleSupported,
    bool CancelReplaceSupported,
    bool ExecutionReportsSupported,
    bool FillsSupported,
    bool OrderLifecycleSupported,
    bool TradingMutationSupported,
    bool RawFixSerialized,
    bool RawCredentialsSerialized,
    bool CredentialValuesReturned,
    bool ExternalBoundaryAttemptedDuringValidation,
    bool MarketDataRequestBlockedUntilFixSuccess,
    bool ApiWorkerReachable,
    bool NoExternalDefaultPreserved);

public sealed record LmaxReadOnlyActivationManualFixSessionAcknowledgementReaderBindingValidation(
    string BindingName,
    string AdapterMode,
    bool FixSessionAcknowledgementBlockerCleared,
    bool FixAcknowledgementReaderReady,
    bool FixSessionParserClassifierReady,
    bool SessionOnly,
    IReadOnlyList<string> SupportedSanitizedCategories,
    bool OrderMessagesSupported,
    bool ExecutionReportsSupported,
    bool FillsSupported,
    bool OrderLifecycleSupported,
    bool NewOrderSingleSupported,
    bool CancelReplaceSupported,
    bool TradingMutationSupported,
    bool RawFixSerialized,
    bool CredentialValuesReturned,
    bool ExternalBoundaryAttemptedDuringValidation,
    bool MarketDataRequestBlockedUntilFixSuccess);
