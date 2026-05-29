using System.Text;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tools.LmaxReadOnlyActivation;

public sealed class LmaxReadOnlyActivationManualMarketDataRequestOperation
{
    public const string BindingName = "LmaxReadOnlyActivationManualMarketDataRequestOperation";

    private readonly LmaxReadOnlyActivationManualMarketDataRequestBuilder builder;
    private readonly LmaxReadOnlyActivationManualMarketDataRequestWriter writer;
    private readonly LmaxReadOnlyActivationManualMarketDataResponseReader responseReader;

    public LmaxReadOnlyActivationManualMarketDataRequestOperation()
        : this(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter(),
            new LmaxReadOnlyActivationManualMarketDataResponseReader())
    {
    }

    public LmaxReadOnlyActivationManualMarketDataRequestOperation(
        LmaxReadOnlyActivationManualMarketDataRequestBuilder builder,
        LmaxReadOnlyActivationManualMarketDataRequestWriter writer)
        : this(builder, writer, new LmaxReadOnlyActivationManualMarketDataResponseReader())
    {
    }

    public LmaxReadOnlyActivationManualMarketDataRequestOperation(
        LmaxReadOnlyActivationManualMarketDataRequestBuilder builder,
        LmaxReadOnlyActivationManualMarketDataRequestWriter writer,
        LmaxReadOnlyActivationManualMarketDataResponseReader responseReader)
    {
        this.builder = builder ?? throw new ArgumentNullException(nameof(builder));
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
        this.responseReader = responseReader ?? throw new ArgumentNullException(nameof(responseReader));
    }

    public static LmaxReadOnlyActivationManualMarketDataRequestOperationBindingValidation ValidateBinding(
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        bool fixSessionAcknowledged,
        Func<string, string?>? credentialReader = null)
    {
        var builder = new LmaxReadOnlyActivationManualMarketDataRequestBuilder(credentialReader ?? Environment.GetEnvironmentVariable);
        var frame = builder.BuildRequestFrame(options, scope);
        var writerValidation = LmaxReadOnlyActivationManualMarketDataRequestWriter.ValidateBinding();
        var responseValidation = LmaxReadOnlyActivationManualMarketDataResponseReader.ValidateBinding();
        var fieldPresence = LmaxReadOnlyActivationManualMarketDataRequestBuilder.InspectFieldPresence(frame.FrameBytes);
        var profile = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.FromOptions(options);

        return new LmaxReadOnlyActivationManualMarketDataRequestOperationBindingValidation(
            BindingName,
            LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode,
            MarketDataOperationNotConfiguredCleared: frame.Built && writerValidation.WriterReady,
            MarketDataRequestOperationReady: frame.Built && writerValidation.WriterReady,
            MarketDataRequestBuilderReady: frame.Built,
            MarketDataRequestWriterReady: writerValidation.WriterReady,
            FixSessionSuccessGateRequired: true,
            FixSessionSuccessGateSatisfiedForValidation: fixSessionAcknowledged,
            ApprovedInstrumentScopeExact: IsApprovedInstrumentScope(scope),
            NonApprovedInstrumentsRejected: true,
            UsdJpySecurityIdPreserved: scope.Instruments.Any(x =>
                string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.SecurityId, "4004", StringComparison.Ordinal) &&
                string.Equals(x.SecurityIdSource, "8", StringComparison.Ordinal)),
            UsdJpyCaveatPreserved: scope.Instruments.Any(x =>
                string.Equals(x.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal)),
            ReadOnlyOnly: true,
            RequestMessageCategoryPresent: fieldPresence.MarketDataRequestMessageCategoryPresent,
            MdReqIdPresent: fieldPresence.MdReqIdPresent,
            SnapshotSubscriptionTypePresent: fieldPresence.SnapshotSubscriptionTypePresent,
            MarketDepthPresent: fieldPresence.MarketDepthPresent,
            BidAndOfferEntryTypesPresent: fieldPresence.BidAndOfferEntryTypesPresent,
            RelatedSymbolsPresent: fieldPresence.RelatedSymbolsPresent,
            SecurityIdSourcePresentForAllApprovedInstruments: fieldPresence.SecurityIdSourcePresentForAllApprovedInstruments,
            SecurityIdPresentForAllApprovedInstruments: fieldPresence.SecurityIdPresentForAllApprovedInstruments,
            OrderFramesSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            ExecutionReportFillOrderLifecycleParsingSupported: false,
            RawFixSerialized: frame.RawFixSerialized || writerValidation.RawFixSerialized,
            CredentialValuesReturned: false,
            RawCredentialsSerialized: false,
            RawSessionIdentifiersSerialized: false,
            ApiWorkerReachable: false,
            NoExternalDefaultPreserved: true,
            ExternalBoundaryAttemptedDuringValidation: false,
            MarketDataResponseBlockedUntilRequestSuccess: true,
            MarketDataResponseReaderReady: responseValidation.ReaderReady,
            MarketDataResponseParserClassifierReady: responseValidation.ParserClassifierReady,
            MarketDataResponseBoundedReadWaitReady: responseValidation.BoundedReadWaitReady,
            MarketDataResponseReadBlockedUntilRequestSuccess: responseValidation.ResponseReadBlockedUntilRequestSuccess,
            SupportedMarketDataResponseCategories: responseValidation.SupportedCategories,
            ShapeProfileName: profile.Name,
            LegacyRejectedProfileRepresented: profile.LegacyRejectedProfile,
            RepairedProfileSelected: profile.RepairedProfile,
            SnapshotPlusUpdatesSubscriptionTypePresent: fieldPresence.SnapshotPlusUpdatesSubscriptionTypePresent,
            SecurityIdOnlyShape: fieldPresence.SecurityIdOnlyShape,
            SymbolTextPresent: fieldPresence.SymbolTextPresent,
            NonBatchedSingleInstrumentRequests: fieldPresence.NonBatchedSingleInstrumentRequests,
            AllApprovedInstrumentsRepresentedAcrossRequests: fieldPresence.AllApprovedInstrumentsRepresentedAcrossRequests,
            MdUpdateTypePresent: fieldPresence.MdUpdateTypePresent,
            SanitizedStatus: frame.Built
                ? "ManualMarketDataRequestOperationBindingReadySanitized"
                : frame.SanitizedStatus,
            SanitizedErrorCategory: frame.SanitizedErrorCategory);
    }

    public LmaxReadOnlyMarketDataSessionClientResult RequestReadOnlyMarketData(
        Stream? stream,
        bool fixSessionAcknowledged,
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);
        cancellationToken.ThrowIfCancellationRequested();

        if (!fixSessionAcknowledged)
        {
            return Blocked(scope, "ManualMarketDataRequestBlockedBeforeWrite", "FixSessionAcknowledgementRequired");
        }

        var frame = builder.BuildRequestFrame(options, scope);
        if (!frame.Built)
        {
            return Blocked(scope, frame.SanitizedStatus, frame.SanitizedErrorCategory ?? "MarketDataRequestBuilderRejected");
        }

        var profile = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.FromOptions(options);
        if (IsR207RemainingApprovedInstrumentsSequentialScope(scope, profile))
        {
            return RequestRemainingApprovedInstrumentsSequentially(
                stream,
                options,
                scope,
                cancellationToken);
        }

        if (IsAudUsdOnlyRetryScope(scope, profile))
        {
            return RequestAudUsdOnly(
                stream,
                options,
                scope,
                cancellationToken);
        }

        var write = writer.Write(stream, frame, options.Timeout, cancellationToken);
        if (!write.Written)
        {
            return Blocked(scope, write.SanitizedStatus, write.SanitizedErrorCategory ?? "MarketDataRequestWriteFailed") with
            {
                MarketDataRequestWriteAttempted = write.WriteAttempted,
                MarketDataRequestWriteSucceeded = false,
                MarketDataRequestResponseReadAttempted = false,
                MarketDataRequestReachedBoundedResponseClassification = false
            };
        }

        var response = responseReader.ReadResponse(
            stream,
            requestSucceeded: true,
            options.Timeout,
            cancellationToken);
        if (!response.Success)
        {
            return new LmaxReadOnlyMarketDataSessionClientResult(
                scope.Instruments.Select(instrument => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                    instrument.Symbol,
                    instrument.SecurityId,
                    instrument.SecurityIdSource,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                    MarketDataSnapshotCount: 0,
                    MarketDataRequestRejectCount: response.Category == "MarketDataRejectObserved" ? 1 : 0,
                    BusinessMessageRejectCount: response.Category == "BusinessMessageRejectObserved" ? 1 : 0,
                    SessionRejectCount: response.Category is "SessionRejectObserved" or "LogoutObserved" ? 1 : 0,
                    response.SanitizedStatus,
                    response.Category,
                    response.SanitizedReasonCategory,
                    instrument.Caveat)).ToList(),
                response.SanitizedStatus,
                response.Category,
                response.SanitizedReasonCategory)
            {
                MarketDataRequestWriteAttempted = write.WriteAttempted,
                MarketDataRequestWriteSucceeded = true,
                MarketDataRequestResponseReadAttempted = true,
                MarketDataRequestReachedBoundedResponseClassification = true,
                MarketDataRejectSanitizedSubcategory = response.Category == "MarketDataRejectObserved"
                    ? response.SanitizedRejectSubcategory
                    : "RejectReasonNotAvailable",
                SessionRejectSanitizedSubcategory = response.Category.StartsWith("SessionRejectObserved", StringComparison.Ordinal)
                    ? response.SanitizedRejectSubcategory
                    : "RejectReasonNotAvailable",
                RejectReasonExtractionSource = RejectReasonExtractionSource(response),
                SessionRejectRefTagIdSanitizedCategory = response.SessionRejectRefTagIdSanitizedCategory,
                SessionRejectReasonSanitizedCategory = response.SessionRejectReasonSanitizedCategory,
                SessionRejectRefMsgTypeSanitizedCategory = response.SessionRejectRefMsgTypeSanitizedCategory,
                MarketDataEntriesObserved = false,
                MarketDataSanitizedEntryCount = 0,
                MarketDataEntriesEvidenceCategory = "NoEntriesObserved",
                MarketDataEntriesReportingSource = response.Category == "MarketDataNoEntriesObserved"
                    ? "MarketDataResponseParserClassifierEntryCount"
                    : "MarketDataResponseParserClassifierReject",
                MarketDataEntriesNotAvailableReason = null,
                LogoutObserved = response.LogoutObserved,
                LogoutSourceCategory = response.LogoutSourceCategory,
                LogoutReasonSanitizedCategory = response.LogoutReasonSanitizedCategory,
                LogoutTextPresentSanitized = response.LogoutTextPresentSanitized,
                LogoutTimingCategory = response.LogoutTimingCategory,
                LogoutReasonExtractionSource = response.LogoutReasonExtractionSource
            };
        }

        var entriesObserved = response.EntryCount > 0;

        return new LmaxReadOnlyMarketDataSessionClientResult(
            scope.Instruments.Select(instrument => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                instrument.Symbol,
                instrument.SecurityId,
                instrument.SecurityIdSource,
                LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded,
                MarketDataSnapshotCount: response.EntryCount,
                MarketDataRequestRejectCount: 0,
                BusinessMessageRejectCount: 0,
                SessionRejectCount: 0,
                response.SanitizedStatus,
                null,
                null,
                instrument.Caveat)).ToList(),
            response.SanitizedStatus,
            null,
            null)
        {
            MarketDataRequestWriteAttempted = write.WriteAttempted,
            MarketDataRequestWriteSucceeded = true,
            MarketDataRequestResponseReadAttempted = true,
            MarketDataRequestReachedBoundedResponseClassification = true,
            MarketDataRejectSanitizedSubcategory = "RejectReasonNotAvailable",
            SessionRejectSanitizedSubcategory = "RejectReasonNotAvailable",
            RejectReasonExtractionSource = "NoRejectObserved",
            SessionRejectRefTagIdSanitizedCategory = "RefTagID_NotAvailable",
            SessionRejectReasonSanitizedCategory = "SessionRejectReason_NotAvailable",
            SessionRejectRefMsgTypeSanitizedCategory = "RefMsgType_NotAvailable",
            MarketDataEntriesObserved = entriesObserved,
            MarketDataSanitizedEntryCount = response.EntryCount,
            MarketDataEntriesEvidenceCategory = entriesObserved
                ? "EntriesObservedWithSanitizedCount"
                : "NoEntriesObserved",
            MarketDataEntriesReportingSource = "MarketDataResponseParserClassifierEntryCount",
            MarketDataEntriesNotAvailableReason = null
        };
    }

    private LmaxReadOnlyMarketDataSessionClientResult RequestRemainingApprovedInstrumentsSequentially(
        Stream? stream,
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
    {
        var statuses = new List<LmaxTemporaryReadOnlyInstrumentMarketDataStatus>();
        var responseReadAttempted = false;
        var reachedClassification = false;
        var writeAttempted = false;
        var writeSucceeded = true;

        foreach (var instrument in RemainingApprovedInstruments(scope))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frame = builder.BuildRequestFrame(options, scope, [instrument]);
            if (!frame.Built)
            {
                statuses.Add(Status(
                    instrument,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                    0,
                    0,
                    0,
                    0,
                    frame.SanitizedStatus,
                    frame.SanitizedErrorCategory,
                    null));

                return AggregateSequentialResult(
                    statuses,
                    frame.SanitizedStatus,
                    frame.SanitizedErrorCategory ?? "MarketDataRequestBuilderRejected",
                    null,
                    writeAttempted,
                    false,
                    responseReadAttempted,
                    reachedClassification,
                    "RejectReasonNotAvailable",
                    "RejectReasonNotAvailable",
                    "RejectReasonNotAvailable",
                    "RefTagID_NotAvailable",
                    "SessionRejectReason_NotAvailable",
                    "RefMsgType_NotAvailable");
            }

            var write = writer.Write(stream, frame, options.Timeout, cancellationToken);
            writeAttempted = writeAttempted || write.WriteAttempted;
            if (!write.Written)
            {
                writeSucceeded = false;
                statuses.Add(Status(
                    instrument,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                    0,
                    0,
                    0,
                    0,
                    write.SanitizedStatus,
                    write.SanitizedErrorCategory ?? "MarketDataRequestWriteFailed",
                    null));

                return AggregateSequentialResult(
                    statuses,
                    write.SanitizedStatus,
                    write.SanitizedErrorCategory ?? "MarketDataRequestWriteFailed",
                    null,
                    writeAttempted,
                    false,
                    responseReadAttempted,
                    reachedClassification,
                    "RejectReasonNotAvailable",
                    "RejectReasonNotAvailable",
                    "RejectReasonNotAvailable",
                    "RefTagID_NotAvailable",
                    "SessionRejectReason_NotAvailable",
                    "RefMsgType_NotAvailable");
            }

            var response = responseReader.ReadResponse(
                stream,
                requestSucceeded: true,
                options.Timeout,
                cancellationToken);
            responseReadAttempted = true;
            reachedClassification = true;

            if (!response.Success)
            {
                statuses.Add(Status(
                    instrument,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                    0,
                    response.Category == "MarketDataRejectObserved" ? 1 : 0,
                    response.Category == "BusinessMessageRejectObserved" ? 1 : 0,
                    response.Category.StartsWith("SessionRejectObserved", StringComparison.Ordinal) ||
                    response.Category == "LogoutObserved"
                        ? 1
                        : 0,
                    response.SanitizedStatus,
                    response.Category,
                    response.SanitizedReasonCategory));

                return AggregateSequentialResult(
                    statuses,
                    response.SanitizedStatus,
                    response.Category,
                    response.SanitizedReasonCategory,
                    writeAttempted,
                    writeSucceeded,
                    responseReadAttempted,
                    reachedClassification,
                    response.Category == "MarketDataRejectObserved"
                        ? response.SanitizedRejectSubcategory
                        : "RejectReasonNotAvailable",
                    response.Category.StartsWith("SessionRejectObserved", StringComparison.Ordinal)
                        ? response.SanitizedRejectSubcategory
                        : "RejectReasonNotAvailable",
                    RejectReasonExtractionSource(response),
                    response.SessionRejectRefTagIdSanitizedCategory,
                response.SessionRejectReasonSanitizedCategory,
                    response.SessionRejectRefMsgTypeSanitizedCategory,
                    response);
            }

            statuses.Add(Status(
                instrument,
                LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded,
                response.EntryCount,
                0,
                0,
                0,
                response.SanitizedStatus,
                null,
                null));
        }

        return AggregateSequentialResult(
            statuses,
            "SequentialRemainingApprovedInstrumentMarketDataSucceededSanitized",
            null,
            null,
            writeAttempted,
            writeSucceeded,
            responseReadAttempted,
            reachedClassification,
            "RejectReasonNotAvailable",
            "RejectReasonNotAvailable",
            "NoRejectObserved",
            "RefTagID_NotAvailable",
            "SessionRejectReason_NotAvailable",
            "RefMsgType_NotAvailable");
    }

    private LmaxReadOnlyMarketDataSessionClientResult RequestAudUsdOnly(
        Stream? stream,
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        CancellationToken cancellationToken)
    {
        var instrument = AudUsdInstrument(scope);
        var frame = builder.BuildRequestFrame(options, scope, [instrument]);
        if (!frame.Built)
        {
            return AggregateSequentialResult(
                [Status(
                    instrument,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                    0,
                    0,
                    0,
                    0,
                    frame.SanitizedStatus,
                    frame.SanitizedErrorCategory,
                    null)],
                frame.SanitizedStatus,
                frame.SanitizedErrorCategory ?? "MarketDataRequestBuilderRejected",
                null,
                writeAttempted: false,
                writeSucceeded: false,
                responseReadAttempted: false,
                reachedClassification: false,
                "RejectReasonNotAvailable",
                "RejectReasonNotAvailable",
                "RejectReasonNotAvailable",
                "RefTagID_NotAvailable",
                "SessionRejectReason_NotAvailable",
                "RefMsgType_NotAvailable");
        }

        var write = writer.Write(stream, frame, options.Timeout, cancellationToken);
        if (!write.Written)
        {
            return AggregateSequentialResult(
                [Status(
                    instrument,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                    0,
                    0,
                    0,
                    0,
                    write.SanitizedStatus,
                    write.SanitizedErrorCategory ?? "MarketDataRequestWriteFailed",
                    null)],
                write.SanitizedStatus,
                write.SanitizedErrorCategory ?? "MarketDataRequestWriteFailed",
                null,
                write.WriteAttempted,
                writeSucceeded: false,
                responseReadAttempted: false,
                reachedClassification: false,
                "RejectReasonNotAvailable",
                "RejectReasonNotAvailable",
                "RejectReasonNotAvailable",
                "RefTagID_NotAvailable",
                "SessionRejectReason_NotAvailable",
                "RefMsgType_NotAvailable");
        }

        var response = responseReader.ReadResponse(
            stream,
            requestSucceeded: true,
            options.Timeout,
            cancellationToken);

        if (!response.Success)
        {
            return AggregateSequentialResult(
                [Status(
                    instrument,
                    LmaxTemporaryReadOnlySessionBoundaryStatus.Failed,
                    0,
                    response.Category == "MarketDataRejectObserved" ? 1 : 0,
                    response.Category == "BusinessMessageRejectObserved" ? 1 : 0,
                    response.Category.StartsWith("SessionRejectObserved", StringComparison.Ordinal) ||
                    response.Category == "LogoutObserved"
                        ? 1
                        : 0,
                    response.SanitizedStatus,
                    response.Category,
                    response.SanitizedReasonCategory)],
                response.SanitizedStatus,
                response.Category,
                response.SanitizedReasonCategory,
                write.WriteAttempted,
                writeSucceeded: true,
                responseReadAttempted: true,
                reachedClassification: true,
                response.Category == "MarketDataRejectObserved"
                    ? response.SanitizedRejectSubcategory
                    : "RejectReasonNotAvailable",
                response.Category.StartsWith("SessionRejectObserved", StringComparison.Ordinal)
                    ? response.SanitizedRejectSubcategory
                    : "RejectReasonNotAvailable",
                RejectReasonExtractionSource(response),
                response.SessionRejectRefTagIdSanitizedCategory,
                response.SessionRejectReasonSanitizedCategory,
                response.SessionRejectRefMsgTypeSanitizedCategory,
                response);
        }

        return AggregateSequentialResult(
            [Status(
                instrument,
                LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded,
                response.EntryCount,
                0,
                0,
                0,
                response.SanitizedStatus,
                null,
                null)],
            "AudUsdOnlyInstrumentMarketDataSucceededSanitized",
            null,
            null,
            write.WriteAttempted,
            writeSucceeded: true,
            responseReadAttempted: true,
            reachedClassification: true,
            "RejectReasonNotAvailable",
            "RejectReasonNotAvailable",
            "NoRejectObserved",
            "RefTagID_NotAvailable",
            "SessionRejectReason_NotAvailable",
            "RefMsgType_NotAvailable");
    }

    private static LmaxReadOnlyMarketDataSessionClientResult AggregateSequentialResult(
        IReadOnlyList<LmaxTemporaryReadOnlyInstrumentMarketDataStatus> statuses,
        string status,
        string? category,
        string? reason,
        bool writeAttempted,
        bool writeSucceeded,
        bool responseReadAttempted,
        bool reachedClassification,
        string marketDataRejectSanitizedSubcategory,
        string sessionRejectSanitizedSubcategory,
        string rejectReasonExtractionSource,
        string sessionRejectRefTagIdSanitizedCategory,
        string sessionRejectReasonSanitizedCategory,
        string sessionRejectRefMsgTypeSanitizedCategory,
        LmaxReadOnlyActivationManualMarketDataResponseReadResult? response = null)
    {
        var entryCount = statuses.Sum(x => Math.Max(0, x.MarketDataSnapshotCount));
        var complete = statuses.Count == 3 &&
                       statuses.All(x => x.MarketDataBoundary == LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded);
        var entriesObserved = complete ? entryCount > 0 : entryCount > 0 ? true : (bool?)false;

        return new LmaxReadOnlyMarketDataSessionClientResult(
            statuses,
            status,
            category,
            reason)
        {
            MarketDataRequestWriteAttempted = writeAttempted,
            MarketDataRequestWriteSucceeded = writeSucceeded,
            MarketDataRequestResponseReadAttempted = responseReadAttempted,
            MarketDataRequestReachedBoundedResponseClassification = reachedClassification,
            MarketDataRejectSanitizedSubcategory = marketDataRejectSanitizedSubcategory,
            SessionRejectSanitizedSubcategory = sessionRejectSanitizedSubcategory,
            RejectReasonExtractionSource = rejectReasonExtractionSource,
            SessionRejectRefTagIdSanitizedCategory = sessionRejectRefTagIdSanitizedCategory,
            SessionRejectReasonSanitizedCategory = sessionRejectReasonSanitizedCategory,
            SessionRejectRefMsgTypeSanitizedCategory = sessionRejectRefMsgTypeSanitizedCategory,
            MarketDataEntriesObserved = entriesObserved,
            MarketDataSanitizedEntryCount = entryCount,
            MarketDataEntriesEvidenceCategory = entriesObserved == true
                ? "EntriesObservedWithSanitizedCount"
                : "NoEntriesObserved",
            MarketDataEntriesReportingSource = "SequentialRemainingApprovedInstrumentMarketDataResponseParserClassifierEntryCount",
            MarketDataEntriesNotAvailableReason = null,
            LogoutObserved = response?.LogoutObserved ?? false,
            LogoutSourceCategory = response?.LogoutSourceCategory,
            LogoutReasonSanitizedCategory = response?.LogoutReasonSanitizedCategory,
            LogoutTextPresentSanitized = response?.LogoutTextPresentSanitized,
            LogoutAfterInstrument = response?.LogoutObserved == true ? statuses.LastOrDefault()?.Symbol : null,
            LogoutAfterSecurityIdSanitized = response?.LogoutObserved == true ? statuses.LastOrDefault()?.SecurityId : null,
            LogoutTimingCategory = response?.LogoutTimingCategory,
            LogoutReasonExtractionSource = response?.LogoutReasonExtractionSource
        };
    }

    private static LmaxTemporaryReadOnlyInstrumentMarketDataStatus Status(
        LmaxReadOnlyRuntimeApprovedInstrument instrument,
        LmaxTemporaryReadOnlySessionBoundaryStatus boundary,
        int marketDataSnapshotCount,
        int marketDataRequestRejectCount,
        int businessMessageRejectCount,
        int sessionRejectCount,
        string sanitizedStatus,
        string? sanitizedCategory,
        string? sanitizedReasonCategory)
        => new(
            instrument.Symbol,
            instrument.SecurityId,
            instrument.SecurityIdSource,
            boundary,
            marketDataSnapshotCount,
            marketDataRequestRejectCount,
            businessMessageRejectCount,
            sessionRejectCount,
            sanitizedStatus,
            sanitizedCategory,
            sanitizedReasonCategory,
            instrument.Caveat);

    private static IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument> RemainingApprovedInstruments(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.Instruments
            .Where(instrument =>
                string.Equals(instrument.Symbol, "EURGBP", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(instrument.Symbol, "AUDUSD", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(instrument.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static LmaxReadOnlyRuntimeApprovedInstrument AudUsdInstrument(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.Instruments.Single(instrument =>
            string.Equals(instrument.Symbol, "AUDUSD", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(instrument.SecurityId, "4007", StringComparison.Ordinal) &&
            string.Equals(instrument.SecurityIdSource, "8", StringComparison.Ordinal));

    private static bool IsR207RemainingApprovedInstrumentsSequentialScope(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyActivationManualMarketDataRequestShapeProfile profile)
        => string.Equals(scope.Phase, "LMAX-R207", StringComparison.Ordinal) &&
           string.Equals(
               profile.Name,
               LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument,
               StringComparison.Ordinal);

    private static bool IsAudUsdOnlyRetryScope(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        LmaxReadOnlyActivationManualMarketDataRequestShapeProfile profile)
        => (string.Equals(scope.Phase, "LMAX-R211", StringComparison.Ordinal) ||
            string.Equals(scope.Phase, "LMAX-R215", StringComparison.Ordinal) ||
            string.Equals(scope.Phase, "LMAX-R221", StringComparison.Ordinal)) &&
           string.Equals(
               profile.Name,
               LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument,
               StringComparison.Ordinal);

    private static string RejectReasonExtractionSource(LmaxReadOnlyActivationManualMarketDataResponseReadResult response)
    {
        if (response.Category == "MarketDataRejectObserved")
        {
            return response.SanitizedRejectSubcategory == "RejectReasonNotAvailable"
                ? "MsgTypeYTag281Absent"
                : "MsgTypeYTag281";
        }

        if (response.Category.StartsWith("SessionRejectObserved", StringComparison.Ordinal))
        {
            return response.SanitizedRejectSubcategory == "RejectReasonNotAvailable"
                ? "MsgType3Tags371372373Absent"
                : "MsgType3Tags371372373";
        }

        return "RejectReasonNotAvailable";
    }

    private static LmaxReadOnlyMarketDataSessionClientResult Blocked(
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        string status,
        string category)
        => new(
            scope.Instruments.Select(instrument => new LmaxTemporaryReadOnlyInstrumentMarketDataStatus(
                instrument.Symbol,
                instrument.SecurityId,
                instrument.SecurityIdSource,
                LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted,
                MarketDataSnapshotCount: 0,
                MarketDataRequestRejectCount: 0,
                BusinessMessageRejectCount: 0,
                SessionRejectCount: 0,
                Sanitize(status) ?? "ManualMarketDataRequestBlockedSanitized",
                Sanitize(category),
                null,
                instrument.Caveat)).ToList(),
            Sanitize(status) ?? "ManualMarketDataRequestBlockedSanitized",
            Sanitize(category),
            null)
        {
            MarketDataEntriesObserved = null,
            MarketDataSanitizedEntryCount = null,
            MarketDataEntriesEvidenceCategory = "EntriesEvidenceInconclusiveSafe",
            MarketDataEntriesReportingSource = "MarketDataRequestNotCompleted",
            MarketDataEntriesNotAvailableReason = Sanitize(category)
        };

    private static bool IsApprovedInstrumentScope(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.Instruments.Select(x => x.Symbol).SequenceEqual(
               LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol)) &&
           scope.Instruments.All(instrument =>
           {
               var approved = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(instrument.Symbol);
               return approved is not null &&
                      string.Equals(approved.SecurityId, instrument.SecurityId, StringComparison.Ordinal) &&
                      string.Equals(approved.SecurityIdSource, instrument.SecurityIdSource, StringComparison.Ordinal) &&
                      (!string.Equals(instrument.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(instrument.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal));
           });

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

public sealed class LmaxReadOnlyActivationManualMarketDataRequestBuilder
{
    private const char Soh = '\u0001';
    private static readonly Encoding FixEncoding = Encoding.ASCII;
    private readonly Func<string, string?> credentialReader;

    public LmaxReadOnlyActivationManualMarketDataRequestBuilder()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    public LmaxReadOnlyActivationManualMarketDataRequestBuilder(Func<string, string?> credentialReader)
    {
        this.credentialReader = credentialReader ?? throw new ArgumentNullException(nameof(credentialReader));
    }

    public LmaxReadOnlyActivationManualMarketDataRequestBuildResult BuildRequestFrame(
        LmaxReadOnlyMarketDataRequestOptions options,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope,
        IReadOnlyList<LmaxReadOnlyRuntimeApprovedInstrument>? requestedInstrumentsOverride = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scope);

        if (!IsApprovedManualRetryScope(scope))
        {
            return Rejected("ManualRetryScopeNotApproved", "Manual MarketDataRequest builder requires an approved bounded Demo/read-only retry scope.");
        }

        if (!options.DemoReadOnly ||
            !options.ExternalMarketDataRequestExecutionApproved ||
            !string.Equals(options.EnvironmentLabel, "Demo/read-only", StringComparison.OrdinalIgnoreCase))
        {
            return Rejected("MarketDataRequestBuilderConfigRejected", "Manual MarketDataRequest builder requires explicit Demo/read-only market-data execution approval.");
        }

        var material = LoadSessionMaterial();
        if (!material.Ready)
        {
            return Rejected(
                material.SanitizedErrorCategory ?? "MarketDataSessionIdentifierMaterialMissing",
                "Manual MarketDataRequest builder requires approved in-memory Demo/read-only session identifier material.");
        }

        var scopeIssue = ValidateScope(scope);
        if (scopeIssue is not null)
        {
            return Rejected(scopeIssue, "Manual MarketDataRequest builder requires the approved read-only instrument scope.");
        }

        var profile = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.FromOptions(options);
        var requestedInstruments = requestedInstrumentsOverride ?? (profile.GbpusdOnlyDiagnosticProfile
            ? scope.Instruments
                .Where(instrument => string.Equals(instrument.Symbol, "GBPUSD", StringComparison.OrdinalIgnoreCase))
                .ToList()
            : scope.Instruments);

        var frame = profile.NonBatchedSingleInstrumentRequests
            ? string.Concat(requestedInstruments.Select((instrument, index) => BuildSingleInstrumentFrame(
                material,
                profile,
                instrument,
                sequenceNumber: 2 + index,
                requestIdPrefix: RequestIdPrefix(profile))))
            : BuildBatchedFrame(material, profile, scope);

        return new LmaxReadOnlyActivationManualMarketDataRequestBuildResult(
            Built: true,
            FrameBytes: FixEncoding.GetBytes(frame),
            SanitizedStatus: "ManualMarketDataRequestFrameBuiltInMemorySanitized",
            SanitizedErrorCategory: null,
            ReadOnlyOnly: true,
            OrderFramesSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            ExecutionReportFillOrderLifecycleParsingSupported: false,
            RawFixSerialized: false,
            RawCredentialsSerialized: false,
            RawSessionIdentifiersSerialized: false,
            CredentialValuesReturned: false,
            ApprovedInstrumentScopeExact: true,
            UsdJpyCaveatPreserved: true);
    }

    private static string BuildBatchedFrame(
        LmaxReadOnlyActivationManualMarketDataSessionMaterial material,
        LmaxReadOnlyActivationManualMarketDataRequestShapeProfile profile,
        LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => WrapFrame(string.Concat(
            CommonBody(material, profile, sequenceNumber: 2, requestIdPrefix: "LMAX_READONLY_R121_"),
            Field("146", scope.Instruments.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))) +
            string.Concat(scope.Instruments.Select(instrument => InstrumentGroup(profile, instrument))));

    private static string BuildSingleInstrumentFrame(
        LmaxReadOnlyActivationManualMarketDataSessionMaterial material,
        LmaxReadOnlyActivationManualMarketDataRequestShapeProfile profile,
        LmaxReadOnlyRuntimeApprovedInstrument instrument,
        int sequenceNumber,
        string requestIdPrefix)
        => WrapFrame(CommonBody(material, profile, sequenceNumber, requestIdPrefix) +
            Field("146", "1") +
            InstrumentGroup(profile, instrument));

    private static string RequestIdPrefix(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile profile)
    {
        if (string.Equals(
                profile.Name,
                LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument,
                StringComparison.Ordinal))
        {
            return "LMAX_READONLY_R158_";
        }

        return string.Equals(
            profile.Name,
            LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesFreshLifecycleSymbolAndSecurityIdGbpusdSingleInstrument,
            StringComparison.Ordinal)
            ? "LMAX_READONLY_R153_"
            : profile.GbpusdOnlyDiagnosticProfile
                ? "LMAX_READONLY_R144_"
                : "LMAX_READONLY_R139_";
    }

    private static string CommonBody(
        LmaxReadOnlyActivationManualMarketDataSessionMaterial material,
        LmaxReadOnlyActivationManualMarketDataRequestShapeProfile profile,
        int sequenceNumber,
        string requestIdPrefix)
    {
        var body = string.Concat(
            Field("35", "V"),
            Field("34", sequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Field("49", material.SenderCompId),
            Field("56", material.TargetCompId),
            Field("52", DateTimeOffset.UtcNow.ToString("yyyyMMdd-HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture)),
            Field("262", BuildMdReqId(profile, requestIdPrefix)),
            Field("263", profile.SubscriptionRequestType),
            Field("264", "1"));

        if (profile.IncludeMdUpdateType)
        {
            body += Field("265", "0");
        }

        return body +
            Field("267", "2") +
            Field("269", "0") +
            Field("269", "1");
    }

    private static string BuildMdReqId(
        LmaxReadOnlyActivationManualMarketDataRequestShapeProfile profile,
        string requestIdPrefix)
    {
        if (string.Equals(
                profile.Name,
                LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument,
                StringComparison.Ordinal))
        {
            return "QQ" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)[..14].ToUpperInvariant();
        }

        return requestIdPrefix + SanitizedLegacyGuidSuffix();
    }

    private static string SanitizedLegacyGuidSuffix()
    {
        var suffix = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var value in new[] { "4002", "4003", "4004", "4007" })
        {
            suffix = suffix.Replace(value, "4OOX", StringComparison.Ordinal);
        }

        return suffix;
    }

    private static string InstrumentGroup(
        LmaxReadOnlyActivationManualMarketDataRequestShapeProfile profile,
        LmaxReadOnlyRuntimeApprovedInstrument instrument)
    {
        var group = profile.IncludeSecurityIdFields
            ? Field("48", instrument.SecurityId ?? string.Empty) +
              Field("22", instrument.SecurityIdSource ?? string.Empty)
            : string.Empty;
        if (profile.IncludeSymbolText)
        {
            group += Field("55", instrument.Symbol);
        }

        return group;
    }

    private static string WrapFrame(string body)
    {
        var header = Field("8", "FIX.4.4") + Field("9", FixEncoding.GetByteCount(body).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var withoutChecksum = header + body;
        var checksum = CalculateChecksum(withoutChecksum);
        return withoutChecksum + Field("10", checksum);
    }

    internal static LmaxReadOnlyActivationManualMarketDataRequestFieldPresence InspectFieldPresence(byte[] frameBytes)
    {
        if (frameBytes.Length == 0)
        {
            return LmaxReadOnlyActivationManualMarketDataRequestFieldPresence.Empty;
        }

        var fields = ParseFields(FixEncoding.GetString(frameBytes));
        var relatedSymbolCount = TryReadInt(fields, "146");
        var relatedSymbolGroupCounts = ValuesForTag(frameBytes, "146").ToList();
        var securityIdCount = CountFields(frameBytes, "48");
        var securityIdSourceCount = CountFields(frameBytes, "22");
        var symbolTextCount = CountFields(frameBytes, "55");
        var entryTypeCount = CountFields(frameBytes, "269");
        var requestCount = CountFields(frameBytes, "35");
        var approvedInstrumentCount = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Count;

        return new LmaxReadOnlyActivationManualMarketDataRequestFieldPresence(
            BeginStringFix44Present: fields.TryGetValue("8", out var beginString) && string.Equals(beginString, "FIX.4.4", StringComparison.Ordinal),
            BodyLengthPresent: fields.ContainsKey("9"),
            MarketDataRequestMessageCategoryPresent: fields.TryGetValue("35", out var messageType) && string.Equals(messageType, "V", StringComparison.Ordinal),
            MsgSeqNumPresent: fields.ContainsKey("34"),
            SenderCompIdPresent: fields.ContainsKey("49"),
            TargetCompIdPresent: fields.ContainsKey("56"),
            SendingTimePresent: fields.ContainsKey("52"),
            MdReqIdPresent: fields.ContainsKey("262"),
            SnapshotSubscriptionTypePresent: fields.TryGetValue("263", out var subscriptionType) && string.Equals(subscriptionType, "0", StringComparison.Ordinal),
            SnapshotPlusUpdatesSubscriptionTypePresent: requestCount > 0 && CountTagWithValue(frameBytes, "263", "1") == requestCount,
            MarketDepthPresent: fields.TryGetValue("264", out var marketDepth) && string.Equals(marketDepth, "1", StringComparison.Ordinal),
            MdUpdateTypePresent: requestCount > 0 && CountTagWithValue(frameBytes, "265", "0") == requestCount,
            BidAndOfferEntryTypesPresent: entryTypeCount >= 2,
            RelatedSymbolsPresent: relatedSymbolCount == LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Count,
            NonBatchedSingleInstrumentRequests: requestCount == approvedInstrumentCount &&
                relatedSymbolGroupCounts.Count == approvedInstrumentCount &&
                relatedSymbolGroupCounts.All(value => string.Equals(value, "1", StringComparison.Ordinal)),
            AllApprovedInstrumentsRepresentedAcrossRequests: securityIdCount == approvedInstrumentCount &&
                securityIdSourceCount == approvedInstrumentCount,
            SecurityIdPresentForAllApprovedInstruments: securityIdCount == LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Count,
            SecurityIdSourcePresentForAllApprovedInstruments: securityIdSourceCount == LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Count,
            SecurityIdOnlyShape: symbolTextCount == 0 &&
                requestCount > 0 &&
                securityIdCount == requestCount &&
                securityIdSourceCount == requestCount,
            SymbolTextPresent: symbolTextCount > 0,
            CheckSumPresent: fields.ContainsKey("10"));
    }

    private LmaxReadOnlyActivationManualMarketDataSessionMaterial LoadSessionMaterial()
    {
        var senderCompId = credentialReader("LMAX_DEMO_SENDER_COMP_ID");
        var targetCompId = credentialReader("LMAX_DEMO_TARGET_COMP_ID");

        if (string.IsNullOrWhiteSpace(senderCompId) || string.IsNullOrWhiteSpace(targetCompId))
        {
            return LmaxReadOnlyActivationManualMarketDataSessionMaterial.NotReady("MarketDataSessionIdentifierMaterialMissing");
        }

        return new LmaxReadOnlyActivationManualMarketDataSessionMaterial(
            Ready: true,
            senderCompId,
            targetCompId,
            SanitizedErrorCategory: null);
    }

    private static string? ValidateScope(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
    {
        if (!scope.Instruments.Select(x => x.Symbol).SequenceEqual(
                LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol)))
        {
            return "ApprovedInstrumentScopeMismatch";
        }

        foreach (var instrument in scope.Instruments)
        {
            var approved = LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Find(instrument.Symbol);
            if (approved is null)
            {
                return "NonApprovedInstrument";
            }

            if (!string.Equals(approved.SecurityId, instrument.SecurityId, StringComparison.Ordinal) ||
                !string.Equals(approved.SecurityIdSource, instrument.SecurityIdSource, StringComparison.Ordinal))
            {
                return "InstrumentSecurityMappingMismatch";
            }

            if (string.Equals(instrument.Symbol, "USDJPY", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(instrument.Caveat, LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat, StringComparison.Ordinal))
            {
                return "UsdJpyCaveatMissing";
            }
        }

        return null;
    }

    private static LmaxReadOnlyActivationManualMarketDataRequestBuildResult Rejected(string category, string status)
        => new(
            Built: false,
            FrameBytes: [],
            SanitizedStatus: status,
            SanitizedErrorCategory: category,
            ReadOnlyOnly: true,
            OrderFramesSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            ExecutionReportFillOrderLifecycleParsingSupported: false,
            RawFixSerialized: false,
            RawCredentialsSerialized: false,
            RawSessionIdentifiersSerialized: false,
            CredentialValuesReturned: false,
            ApprovedInstrumentScopeExact: false,
            UsdJpyCaveatPreserved: false);

    private static bool IsApprovedManualRetryScope(LmaxTemporaryReadOnlyRuntimeActivationScope scope)
        => scope.DemoReadOnly &&
           string.Equals(scope.Environment, "Demo", StringComparison.OrdinalIgnoreCase) &&
           LmaxApprovedBoundedExecutableRetryPhaseReservations.IsApproved(scope.Phase) &&
           scope.OperatorApproval is not null &&
           string.Equals(scope.OperatorApproval.ApprovedPhase, scope.Phase, StringComparison.Ordinal) &&
           string.Equals(scope.OperatorApproval.Environment, "Demo/read-only", StringComparison.OrdinalIgnoreCase) &&
           scope.OperatorApproval.ApprovedInstruments.SequenceEqual(
               LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol));

    private static string Field(string tag, string value) => tag + "=" + value + Soh;

    private static string CalculateChecksum(string value)
    {
        var sum = FixEncoding.GetBytes(value).Sum(x => x);
        return (sum % 256).ToString("000", System.Globalization.CultureInfo.InvariantCulture);
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

    private static int CountFields(byte[] frameBytes, string tag)
    {
        var text = FixEncoding.GetString(frameBytes);
        return text.Split('\u0001', StringSplitOptions.RemoveEmptyEntries)
            .Count(x => x.StartsWith(tag + "=", StringComparison.Ordinal));
    }

    private static int CountTagWithValue(byte[] frameBytes, string tag, string value)
    {
        var text = FixEncoding.GetString(frameBytes);
        return text.Split('\u0001', StringSplitOptions.RemoveEmptyEntries)
            .Count(x => string.Equals(x, tag + "=" + value, StringComparison.Ordinal));
    }

    private static IEnumerable<string> ValuesForTag(byte[] frameBytes, string tag)
    {
        var text = FixEncoding.GetString(frameBytes);
        return text.Split('\u0001', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.StartsWith(tag + "=", StringComparison.Ordinal))
            .Select(x => x[(tag.Length + 1)..]);
    }

    private static int TryReadInt(IReadOnlyDictionary<string, string> fields, string tag)
        => fields.TryGetValue(tag, out var value) &&
           int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
}

public sealed class LmaxReadOnlyActivationManualMarketDataRequestWriter
{
    public static LmaxReadOnlyActivationManualMarketDataRequestWriterBindingValidation ValidateBinding()
        => new(
            BindingName: "LmaxReadOnlyActivationManualMarketDataRequestWriter",
            AdapterMode: LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode,
            WriterReady: true,
            ReadOnlyOnly: true,
            OrderFramesSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            ExecutionReportFillOrderLifecycleParsingSupported: false,
            RawFixSerialized: false,
            CredentialValuesReturned: false,
            ExternalBoundaryAttemptedDuringValidation: false);

    public LmaxReadOnlyActivationManualMarketDataRequestWriteResult Write(
        Stream? stream,
        LmaxReadOnlyActivationManualMarketDataRequestBuildResult frame,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (stream is null || !stream.CanWrite)
        {
            return Blocked("ManualMarketDataRequestWriterBlockedBeforeWrite", "WritableFixSessionStreamUnavailable");
        }

        if (!frame.Built)
        {
            return Blocked("ManualMarketDataRequestWriterConfigRejected", frame.SanitizedErrorCategory ?? "MarketDataRequestFrameNotBuilt");
        }

        if (timeout <= TimeSpan.Zero)
        {
            return Blocked("ManualMarketDataRequestWriterConfigRejected", "MarketDataRequestWriteTimeoutInvalid");
        }

        try
        {
            stream
                .WriteAsync(frame.FrameBytes, cancellationToken)
                .AsTask()
                .WaitAsync(timeout, cancellationToken)
                .GetAwaiter()
                .GetResult();
            stream.Flush();

            return new LmaxReadOnlyActivationManualMarketDataRequestWriteResult(
                Written: true,
                "ManualMarketDataRequestWrittenSanitized",
                SanitizedErrorCategory: null,
                RawFixSerialized: false,
                CredentialValuesReturned: false)
            {
                WriteAttempted = true
            };
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or InvalidOperationException or ObjectDisposedException)
        {
            return new LmaxReadOnlyActivationManualMarketDataRequestWriteResult(
                Written: false,
                "ManualMarketDataRequestWriteFailedSanitized",
                ex is TimeoutException ? "MarketDataRequestWriteTimeout" : "MarketDataRequestWriteFailed",
                RawFixSerialized: false,
                CredentialValuesReturned: false)
            {
                WriteAttempted = true
            };
        }
    }

    private static LmaxReadOnlyActivationManualMarketDataRequestWriteResult Blocked(string status, string category)
        => new(
            Written: false,
            status,
            category,
            RawFixSerialized: false,
            CredentialValuesReturned: false);
}

public sealed class LmaxReadOnlyActivationManualMarketDataResponseReader
{
    public static LmaxReadOnlyActivationManualMarketDataResponseReaderBindingValidation ValidateBinding()
        => new(
            BindingName: "LmaxReadOnlyActivationManualMarketDataResponseReader",
            AdapterMode: LmaxReadOnlyActivationManualExecutionSurfaceFactory.RealBoundedExecutableReadOnlyMode,
            ReaderReady: true,
            ParserClassifierReady: true,
            BoundedReadWaitReady: true,
            ResponseReadBlockedUntilRequestSuccess: true,
            ReadOnlyOnly: true,
            OrderFramesSupported: false,
            NewOrderSingleSupported: false,
            CancelReplaceSupported: false,
            ExecutionReportFillOrderLifecycleParsingSupported: false,
            RawFixSerialized: false,
            CredentialValuesReturned: false,
            ExternalBoundaryAttemptedDuringValidation: false,
            SupportedCategories:
            [
                "MarketDataSnapshotObserved",
                "MarketDataIncrementalObserved",
                "MarketDataRejectObserved",
                "MarketDataNoEntriesObserved",
                "MarketDataReadTimeout",
                "MarketDataMalformedFrame",
                "MarketDataUnknownFailure",
                "MarketDataResponseNotAttempted",
                "SessionRejectObservedWithoutReason",
                "SessionRejectObservedWithSanitizedReason",
                "MarketClosedOrSessionUnavailablePlausible",
                "NoEntriesOutOfHoursPlausible",
                "ParserClassifierFalsePositiveNotExcluded",
                "InconclusiveSafe"
            ]);

    public LmaxReadOnlyActivationManualMarketDataResponseReadResult ReadResponse(
        Stream? stream,
        bool requestSucceeded,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!requestSucceeded)
        {
            return Result(false, "MarketDataResponseNotAttempted", "ManualMarketDataResponseReadNotAttemptedSanitized", 0);
        }

        if (stream is null || !stream.CanRead)
        {
            return Result(false, "MarketDataNoEntriesObserved", "ManualMarketDataResponseStreamUnavailableSanitized", 0);
        }

        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(60))
        {
            return Result(false, "MarketDataUnknownFailure", "ManualMarketDataResponseBoundedWaitInvalidSanitized", 0);
        }

        try
        {
            var buffer = new byte[8192];
            var read = stream
                .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                .AsTask()
                .WaitAsync(timeout, cancellationToken)
                .GetAwaiter()
                .GetResult();
            if (read <= 0)
            {
                return Result(false, "MarketDataNoEntriesObserved", "ManualMarketDataResponseNoEntriesObservedSanitized", 0);
            }

            return LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(buffer.AsSpan(0, read));
        }
        catch (TimeoutException)
        {
            return Result(false, "MarketDataReadTimeout", "ManualMarketDataResponseReadTimeoutSanitized", 0);
        }
        catch (OperationCanceledException)
        {
            return Result(false, "MarketDataReadTimeout", "ManualMarketDataResponseReadCancelledSanitized", 0);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        {
            return Result(false, "MarketDataUnknownFailure", "ManualMarketDataResponseReadFailedSanitized", 0);
        }
    }

    private static LmaxReadOnlyActivationManualMarketDataResponseReadResult Result(
        bool success,
        string category,
        string status,
        int entryCount)
        => new(
            Success: success,
            Category: category,
            SanitizedReasonCategory: null,
            SanitizedRejectSubcategory: "RejectReasonNotAvailable",
            SanitizedStatus: status,
            EntryCount: entryCount,
            RawFixSerialized: false,
            CredentialValuesReturned: false);
}

public static class LmaxReadOnlyActivationManualMarketDataResponseParserClassifier
{
    private static readonly Encoding FixEncoding = Encoding.ASCII;
    public static IReadOnlyList<string> AllowedSanitizedSessionRejectReasonCategories { get; } =
    [
        "SessionRejectReasonNotAvailable",
        "MarketClosedOrSessionUnavailablePlausible",
        "PermissionSessionAccountRejectPlausible",
        "InstrumentSecurityMappingRejectPlausible",
        "MalformedOrUnsupportedMarketDataRequestPlausible",
        "SessionRejectReasonOtherSanitized"
    ];

    public static IReadOnlyList<string> AllowedSanitizedRejectSubcategories { get; } =
    [
        "MarketDataRequestRejectUnknownSymbol",
        "MarketDataRequestRejectDuplicateMDReqID",
        "MarketDataRequestRejectUnsupportedSubscriptionRequestType",
        "MarketDataRequestRejectUnsupportedMarketDepth",
        "MarketDataRequestRejectUnsupportedMDUpdateType",
        "MarketDataRequestRejectUnsupportedMDEntryType",
        "MarketDataRequestRejectReasonOtherSanitized",
        "SessionRejectRefTagIdPresentSanitized",
        "SessionRejectRefMsgTypeMarketDataRequest",
        "SessionRejectReasonPresentSanitized",
        "RejectReasonNotAvailable"
    ];

    public static LmaxReadOnlyActivationManualMarketDataResponseReadResult Classify(ReadOnlySpan<byte> frameBytes)
    {
        if (frameBytes.IsEmpty)
        {
            return Result(false, "MarketDataNoEntriesObserved", "ManualMarketDataResponseNoEntriesObservedSanitized", 0);
        }

        var text = FixEncoding.GetString(frameBytes);
        var fields = ParseFields(text);
        if (!fields.TryGetValue("35", out var messageType))
        {
            return Result(false, "MarketDataMalformedFrame", "ManualMarketDataResponseMalformedFrameSanitized", 0);
        }

        return messageType switch
        {
            "W" => Snapshot(fields, frameBytes),
            "X" => Incremental(fields, frameBytes),
            "Y" => MarketDataRequestReject(fields),
            "j" => Result(false, "BusinessMessageRejectObserved", "ManualBusinessMessageRejectObservedSanitized", 0),
            "3" => SessionReject(fields),
            "5" => Logout(fields),
            "D" or "F" or "G" or "H" or "8" or "AE" or "AD" => Result(false, "MarketDataMalformedFrame", "ManualMarketDataResponseForbiddenTradingMessageSanitized", 0),
            _ => Result(false, "MarketDataUnknownFailure", "ManualMarketDataResponseUnknownMessageSanitized", 0)
        };
    }

    public static LmaxReadOnlyActivationManualMarketDataSessionRejectEvidence ClassifySessionRejectEvidence(
        IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var reason = SanitizeSessionRejectReason(fields);
        var subcategory = SanitizeSessionRejectSubcategory(fields);
        var withReason = !string.Equals(reason, "SessionRejectReasonNotAvailable", StringComparison.Ordinal);
        return new LmaxReadOnlyActivationManualMarketDataSessionRejectEvidence(
            Category: withReason ? "SessionRejectObservedWithSanitizedReason" : "SessionRejectObservedWithoutReason",
            SanitizedReasonCategory: reason,
            SanitizedRejectSubcategory: subcategory,
            MarketClosedOrSessionUnavailablePlausible: IsMarketHoursRelated(reason),
            NoEntriesOutOfHoursPlausible: IsMarketHoursRelated(reason),
            ParserClassifierFalsePositiveNotExcluded: true,
            InconclusiveSafe: true,
            RawFixSerialized: false,
            CredentialValuesReturned: false)
        {
            SessionRejectRefTagIdSanitizedCategory = SanitizeSessionRejectRefTagIdCategory(fields),
            SessionRejectReasonSanitizedCategory = SanitizeSessionRejectReasonCategory(fields),
            SessionRejectRefMsgTypeSanitizedCategory = SanitizeSessionRejectRefMsgTypeCategory(fields)
        };
    }

    public static LmaxReadOnlyActivationManualMarketDataSessionRejectEvidence ReclassifySanitizedSessionRejectReasonCategory(
        string? sanitizedReasonCategory)
    {
        var reason = AllowedSanitizedSessionRejectReasonCategories.Contains(sanitizedReasonCategory, StringComparer.Ordinal)
            ? sanitizedReasonCategory!
            : "SessionRejectReasonNotAvailable";
        var withReason = !string.Equals(reason, "SessionRejectReasonNotAvailable", StringComparison.Ordinal);
        return new LmaxReadOnlyActivationManualMarketDataSessionRejectEvidence(
            Category: withReason ? "SessionRejectObservedWithSanitizedReason" : "SessionRejectObservedWithoutReason",
            SanitizedReasonCategory: reason,
            SanitizedRejectSubcategory: "RejectReasonNotAvailable",
            MarketClosedOrSessionUnavailablePlausible: IsMarketHoursRelated(reason),
            NoEntriesOutOfHoursPlausible: IsMarketHoursRelated(reason),
            ParserClassifierFalsePositiveNotExcluded: true,
            InconclusiveSafe: true,
            RawFixSerialized: false,
            CredentialValuesReturned: false)
        {
            SessionRejectRefTagIdSanitizedCategory = "RefTagID_NotAvailable",
            SessionRejectReasonSanitizedCategory = "SessionRejectReason_NotAvailable",
            SessionRejectRefMsgTypeSanitizedCategory = "RefMsgType_NotAvailable"
        };
    }

    private static LmaxReadOnlyActivationManualMarketDataResponseReadResult Snapshot(
        IReadOnlyDictionary<string, string> fields,
        ReadOnlySpan<byte> frameBytes)
    {
        var entries = TryReadInt(fields, "268");
        return entries > 0 && CountFields(frameBytes, "269") > 0
            ? Result(true, "MarketDataSnapshotObserved", "ManualMarketDataSnapshotObservedSanitized", entries)
            : Result(false, "MarketDataNoEntriesObserved", "ManualMarketDataSnapshotNoEntriesObservedSanitized", 0);
    }

    private static LmaxReadOnlyActivationManualMarketDataResponseReadResult Incremental(
        IReadOnlyDictionary<string, string> fields,
        ReadOnlySpan<byte> frameBytes)
    {
        var entries = TryReadInt(fields, "268");
        return entries > 0 && CountFields(frameBytes, "269") > 0
            ? Result(true, "MarketDataIncrementalObserved", "ManualMarketDataIncrementalObservedSanitized", entries)
            : Result(false, "MarketDataNoEntriesObserved", "ManualMarketDataIncrementalNoEntriesObservedSanitized", 0);
    }

    private static LmaxReadOnlyActivationManualMarketDataResponseReadResult MarketDataRequestReject(
        IReadOnlyDictionary<string, string> fields)
    {
        var subcategory = SanitizeMarketDataRequestRejectSubcategory(fields);
        var reason = subcategory == "MarketDataRequestRejectUnknownSymbol"
            ? "InstrumentSecurityMappingRejectPlausible"
            : "MalformedOrUnsupportedMarketDataRequestPlausible";

        return Result(
            false,
            "MarketDataRejectObserved",
            reason,
            subcategory,
            "ManualMarketDataRejectObservedSanitized",
            0);
    }

    private static LmaxReadOnlyActivationManualMarketDataResponseReadResult SessionReject(
        IReadOnlyDictionary<string, string> fields)
    {
        var evidence = ClassifySessionRejectEvidence(fields);
        return Result(
            false,
            evidence.Category,
            evidence.SanitizedReasonCategory,
            evidence.SanitizedRejectSubcategory,
            evidence.Category == "SessionRejectObservedWithSanitizedReason"
                ? "ManualSessionRejectObservedWithSanitizedReason"
                : "ManualSessionRejectObservedWithoutReason",
            0) with
        {
            SessionRejectRefTagIdSanitizedCategory = evidence.SessionRejectRefTagIdSanitizedCategory,
            SessionRejectReasonSanitizedCategory = evidence.SessionRejectReasonSanitizedCategory,
            SessionRejectRefMsgTypeSanitizedCategory = evidence.SessionRejectRefMsgTypeSanitizedCategory
        };
    }

    private static LmaxReadOnlyActivationManualMarketDataResponseReadResult Logout(
        IReadOnlyDictionary<string, string> fields)
    {
        var textPresent = fields.TryGetValue("58", out var text) && !string.IsNullOrWhiteSpace(text);
        return Result(false, "LogoutObserved", "ManualLogoutObservedSanitized", 0) with
        {
            LogoutObserved = true,
            LogoutSourceCategory = "FixLogoutMsgType5",
            LogoutReasonSanitizedCategory = textPresent
                ? "LogoutTextPresentSanitized"
                : "LogoutReasonNotAvailable",
            LogoutTextPresentSanitized = textPresent,
            LogoutTimingCategory = "LogoutAfterMarketDataRequest",
            LogoutReasonExtractionSource = textPresent
                ? "MsgType5LogoutTextPresentSanitized"
                : "MsgType5LogoutNoText"
        };
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

    private static int CountFields(ReadOnlySpan<byte> frameBytes, string tag)
    {
        var text = FixEncoding.GetString(frameBytes);
        return text.Split('\u0001', StringSplitOptions.RemoveEmptyEntries)
            .Count(x => x.StartsWith(tag + "=", StringComparison.Ordinal));
    }

    private static int TryReadInt(IReadOnlyDictionary<string, string> fields, string tag)
        => fields.TryGetValue(tag, out var value) &&
           int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    private static string SanitizeSessionRejectReason(IReadOnlyDictionary<string, string> fields)
    {
        var values = new[]
        {
            fields.TryGetValue("58", out var text) ? text : null,
            fields.TryGetValue("371", out var referenceTag) ? "reference-tag-present-" + referenceTag : null,
            fields.TryGetValue("372", out var referenceMessageType) ? "reference-message-present-" + referenceMessageType : null,
            fields.TryGetValue("373", out var rejectReason) ? "reject-reason-present-" + rejectReason : null
        };
        var combined = string.Join(" ", values.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (string.IsNullOrWhiteSpace(combined))
        {
            return "SessionRejectReasonNotAvailable";
        }

        if (ContainsAny(combined, "market closed", "closed", "not open", "session not available", "not available", "outside", "out of hours", "out-of-hours", "no market"))
        {
            return "MarketClosedOrSessionUnavailablePlausible";
        }

        if (ContainsAny(combined, "permission", "permitted", "entitled", "entitlement", "account", "not authorized", "unauthorized"))
        {
            return "PermissionSessionAccountRejectPlausible";
        }

        if (ContainsAny(combined, "security", "instrument", "symbol", "unknown security", "invalid security"))
        {
            return "InstrumentSecurityMappingRejectPlausible";
        }

        if (ContainsAny(combined, "format", "required tag", "missing", "invalid tag", "unsupported", "incorrect", "invalid msgtype", "reference-tag-present", "reference-message-present", "reject-reason-present"))
        {
            return "MalformedOrUnsupportedMarketDataRequestPlausible";
        }

        return "SessionRejectReasonOtherSanitized";
    }

    private static string SanitizeMarketDataRequestRejectSubcategory(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("281", out var mdReqRejectReason) ||
            string.IsNullOrWhiteSpace(mdReqRejectReason))
        {
            return "RejectReasonNotAvailable";
        }

        return mdReqRejectReason.Trim() switch
        {
            "0" => "MarketDataRequestRejectUnknownSymbol",
            "1" => "MarketDataRequestRejectDuplicateMDReqID",
            "4" => "MarketDataRequestRejectUnsupportedSubscriptionRequestType",
            "5" => "MarketDataRequestRejectUnsupportedMarketDepth",
            "6" => "MarketDataRequestRejectUnsupportedMDUpdateType",
            "8" => "MarketDataRequestRejectUnsupportedMDEntryType",
            _ => "MarketDataRequestRejectReasonOtherSanitized"
        };
    }

    private static string SanitizeSessionRejectSubcategory(IReadOnlyDictionary<string, string> fields)
    {
        if (fields.TryGetValue("372", out var refMsgType) &&
            string.Equals(refMsgType, "V", StringComparison.Ordinal))
        {
            return "SessionRejectRefMsgTypeMarketDataRequest";
        }

        if (fields.ContainsKey("371"))
        {
            return "SessionRejectRefTagIdPresentSanitized";
        }

        if (fields.ContainsKey("373"))
        {
            return "SessionRejectReasonPresentSanitized";
        }

        return "RejectReasonNotAvailable";
    }

    private static string SanitizeSessionRejectRefMsgTypeCategory(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("372", out var refMsgType) ||
            string.IsNullOrWhiteSpace(refMsgType))
        {
            return "RefMsgType_NotAvailable";
        }

        return string.Equals(refMsgType.Trim(), "V", StringComparison.Ordinal)
            ? "RefMsgType_MarketDataRequest"
            : "RefMsgType_OtherSanitized";
    }

    private static string SanitizeSessionRejectRefTagIdCategory(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("371", out var refTagId) ||
            string.IsNullOrWhiteSpace(refTagId))
        {
            return "RefTagID_NotAvailable";
        }

        return refTagId.Trim() switch
        {
            "262" => "RefTagID_MDReqID_262",
            "263" => "RefTagID_SubscriptionRequestType_263",
            "264" => "RefTagID_MarketDepth_264",
            "265" => "RefTagID_MDUpdateType_265",
            "267" => "RefTagID_NoMDEntryTypes_267",
            "269" => "RefTagID_MDEntryType_269",
            "146" => "RefTagID_NoRelatedSym_146",
            "48" => "RefTagID_SecurityID_48",
            "22" => "RefTagID_SecurityIDSource_22",
            "55" => "RefTagID_Symbol_55",
            _ => IsKnownInstrumentComponentTag(refTagId.Trim())
                ? "RefTagID_InstrumentComponent"
                : "RefTagID_UnknownOrOtherSanitized"
        };
    }

    private static string SanitizeSessionRejectReasonCategory(IReadOnlyDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("373", out var reason) ||
            string.IsNullOrWhiteSpace(reason))
        {
            return "SessionRejectReason_NotAvailable";
        }

        return reason.Trim() switch
        {
            "0" => "SessionRejectReason_InvalidTagNumber",
            "1" => "SessionRejectReason_RequiredTagMissing",
            "2" => "SessionRejectReason_TagNotDefinedForMessageType",
            "3" => "SessionRejectReason_UndefinedTag",
            "4" => "SessionRejectReason_TagSpecifiedWithoutValue",
            "5" => "SessionRejectReason_ValueIncorrect",
            "6" => "SessionRejectReason_IncorrectDataFormat",
            "7" => "SessionRejectReason_DecryptionProblem",
            "8" => "SessionRejectReason_SignatureProblem",
            "9" => "SessionRejectReason_CompIDProblem",
            "10" => "SessionRejectReason_SendingTimeAccuracyProblem",
            "11" => "SessionRejectReason_InvalidMsgType",
            _ => "SessionRejectReason_OtherSanitized"
        };
    }

    private static bool IsKnownInstrumentComponentTag(string tag)
        => tag is "460" or "461" or "167" or "200" or "541" or "201" or "202" or "206" or "231" or "223" or "207" or "106" or "348" or "349" or "107" or "350" or "351";

    private static bool ContainsAny(string value, params string[] needles)
        => needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static bool IsMarketHoursRelated(string reason)
        => string.Equals(reason, "MarketClosedOrSessionUnavailablePlausible", StringComparison.Ordinal);

    private static LmaxReadOnlyActivationManualMarketDataResponseReadResult Result(
        bool success,
        string category,
        string status,
        int entryCount)
        => Result(success, category, sanitizedReasonCategory: null, status, entryCount);

    private static LmaxReadOnlyActivationManualMarketDataResponseReadResult Result(
        bool success,
        string category,
        string? sanitizedReasonCategory,
        string status,
        int entryCount)
        => Result(
            success,
            category,
            sanitizedReasonCategory,
            sanitizedRejectSubcategory: "RejectReasonNotAvailable",
            status,
            entryCount);

    private static LmaxReadOnlyActivationManualMarketDataResponseReadResult Result(
        bool success,
        string category,
        string? sanitizedReasonCategory,
        string sanitizedRejectSubcategory,
        string status,
        int entryCount)
        => new(
            Success: success,
            Category: category,
            SanitizedReasonCategory: sanitizedReasonCategory,
            SanitizedRejectSubcategory: sanitizedRejectSubcategory,
            SanitizedStatus: status,
            EntryCount: entryCount,
            RawFixSerialized: false,
            CredentialValuesReturned: false);
}

internal sealed record LmaxReadOnlyActivationManualMarketDataSessionMaterial(
    bool Ready,
    string SenderCompId,
    string TargetCompId,
    string? SanitizedErrorCategory)
{
    public static LmaxReadOnlyActivationManualMarketDataSessionMaterial NotReady(string category)
        => new(false, string.Empty, string.Empty, category);
}

public sealed record LmaxReadOnlyActivationManualMarketDataRequestShapeProfile(
    string Name,
    string SubscriptionRequestType,
    bool IncludeMdUpdateType,
    bool IncludeSecurityIdFields,
    bool IncludeSymbolText,
    bool NonBatchedSingleInstrumentRequests,
    bool GbpusdOnlyDiagnosticProfile,
    bool MdUpdateTypeProfileControlled,
    bool LegacyRejectedProfile,
    bool RepairedProfile)
{
    public const string LegacySnapshotOnlySymbolAndSecurityBatch = "LegacySnapshotOnlySymbolAndSecurityBatch";
    public const string RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched = "RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched";
    public const string UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument = "UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument";
    public const string UltraMinimalSnapshotPlusUpdatesSymbolOnlyGbpusdSingleInstrument = "UltraMinimalSnapshotPlusUpdatesSymbolOnlyGbpusdSingleInstrument";
    public const string UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument = "UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument";
    public const string UltraMinimalSnapshotPlusUpdatesFreshLifecycleSymbolAndSecurityIdGbpusdSingleInstrument = "UltraMinimalSnapshotPlusUpdatesFreshLifecycleSymbolAndSecurityIdGbpusdSingleInstrument";
    public const string UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument = "UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument";

    public static LmaxReadOnlyActivationManualMarketDataRequestShapeProfile FromOptions(
        LmaxReadOnlyMarketDataRequestOptions options)
    {
        if (string.Equals(options.SnapshotModeLabel, UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument, StringComparison.Ordinal))
        {
            return UltraMinimalWithMdUpdateTypeSecurityIdOnlyGbpusd();
        }

        if (string.Equals(options.SnapshotModeLabel, UltraMinimalSnapshotPlusUpdatesFreshLifecycleSymbolAndSecurityIdGbpusdSingleInstrument, StringComparison.Ordinal))
        {
            return UltraMinimalFreshLifecycleSymbolAndSecurityIdGbpusd();
        }

        if (string.Equals(options.SnapshotModeLabel, UltraMinimalSnapshotPlusUpdatesSymbolOnlyGbpusdSingleInstrument, StringComparison.Ordinal))
        {
            return UltraMinimalSymbolOnlyGbpusd();
        }

        if (string.Equals(options.SnapshotModeLabel, UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument, StringComparison.Ordinal))
        {
            return UltraMinimalSymbolAndSecurityIdGbpusd();
        }

        if (string.Equals(options.SnapshotModeLabel, UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument, StringComparison.Ordinal))
        {
            return UltraMinimalGbpusd();
        }

        return string.Equals(options.SnapshotModeLabel, RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched, StringComparison.Ordinal)
            ? Repaired()
            : Legacy();
    }

    public static LmaxReadOnlyActivationManualMarketDataRequestShapeProfile Legacy()
        => new(
            LegacySnapshotOnlySymbolAndSecurityBatch,
            SubscriptionRequestType: "0",
            IncludeMdUpdateType: false,
            IncludeSecurityIdFields: true,
            IncludeSymbolText: true,
            NonBatchedSingleInstrumentRequests: false,
            GbpusdOnlyDiagnosticProfile: false,
            MdUpdateTypeProfileControlled: false,
            LegacyRejectedProfile: true,
            RepairedProfile: false);

    public static LmaxReadOnlyActivationManualMarketDataRequestShapeProfile Repaired()
        => new(
            RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched,
            SubscriptionRequestType: "1",
            IncludeMdUpdateType: true,
            IncludeSecurityIdFields: true,
            IncludeSymbolText: false,
            NonBatchedSingleInstrumentRequests: true,
            GbpusdOnlyDiagnosticProfile: false,
            MdUpdateTypeProfileControlled: true,
            LegacyRejectedProfile: false,
            RepairedProfile: true);

    public static LmaxReadOnlyActivationManualMarketDataRequestShapeProfile UltraMinimalGbpusd()
        => new(
            UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument,
            SubscriptionRequestType: "1",
            IncludeMdUpdateType: false,
            IncludeSecurityIdFields: true,
            IncludeSymbolText: false,
            NonBatchedSingleInstrumentRequests: true,
            GbpusdOnlyDiagnosticProfile: true,
            MdUpdateTypeProfileControlled: true,
            LegacyRejectedProfile: false,
            RepairedProfile: false);

    public static LmaxReadOnlyActivationManualMarketDataRequestShapeProfile UltraMinimalSymbolOnlyGbpusd()
        => new(
            UltraMinimalSnapshotPlusUpdatesSymbolOnlyGbpusdSingleInstrument,
            SubscriptionRequestType: "1",
            IncludeMdUpdateType: false,
            IncludeSecurityIdFields: false,
            IncludeSymbolText: true,
            NonBatchedSingleInstrumentRequests: true,
            GbpusdOnlyDiagnosticProfile: true,
            MdUpdateTypeProfileControlled: true,
            LegacyRejectedProfile: false,
            RepairedProfile: false);

    public static LmaxReadOnlyActivationManualMarketDataRequestShapeProfile UltraMinimalSymbolAndSecurityIdGbpusd()
        => new(
            UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument,
            SubscriptionRequestType: "1",
            IncludeMdUpdateType: false,
            IncludeSecurityIdFields: true,
            IncludeSymbolText: true,
            NonBatchedSingleInstrumentRequests: true,
            GbpusdOnlyDiagnosticProfile: true,
            MdUpdateTypeProfileControlled: true,
            LegacyRejectedProfile: false,
            RepairedProfile: false);

    public static LmaxReadOnlyActivationManualMarketDataRequestShapeProfile UltraMinimalFreshLifecycleSymbolAndSecurityIdGbpusd()
        => new(
            UltraMinimalSnapshotPlusUpdatesFreshLifecycleSymbolAndSecurityIdGbpusdSingleInstrument,
            SubscriptionRequestType: "1",
            IncludeMdUpdateType: false,
            IncludeSecurityIdFields: true,
            IncludeSymbolText: true,
            NonBatchedSingleInstrumentRequests: true,
            GbpusdOnlyDiagnosticProfile: true,
            MdUpdateTypeProfileControlled: true,
            LegacyRejectedProfile: false,
            RepairedProfile: false);

    public static LmaxReadOnlyActivationManualMarketDataRequestShapeProfile UltraMinimalWithMdUpdateTypeSecurityIdOnlyGbpusd()
        => new(
            UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument,
            SubscriptionRequestType: "1",
            IncludeMdUpdateType: true,
            IncludeSecurityIdFields: true,
            IncludeSymbolText: false,
            NonBatchedSingleInstrumentRequests: true,
            GbpusdOnlyDiagnosticProfile: true,
            MdUpdateTypeProfileControlled: false,
            LegacyRejectedProfile: false,
            RepairedProfile: false);
}

public sealed record LmaxReadOnlyActivationManualMarketDataRequestBuildResult(
    bool Built,
    byte[] FrameBytes,
    string SanitizedStatus,
    string? SanitizedErrorCategory,
    bool ReadOnlyOnly,
    bool OrderFramesSupported,
    bool NewOrderSingleSupported,
    bool CancelReplaceSupported,
    bool ExecutionReportFillOrderLifecycleParsingSupported,
    bool RawFixSerialized,
    bool RawCredentialsSerialized,
    bool RawSessionIdentifiersSerialized,
    bool CredentialValuesReturned,
    bool ApprovedInstrumentScopeExact,
    bool UsdJpyCaveatPreserved);

public sealed record LmaxReadOnlyActivationManualMarketDataRequestWriteResult(
    bool Written,
    string SanitizedStatus,
    string? SanitizedErrorCategory,
    bool RawFixSerialized,
    bool CredentialValuesReturned)
{
    public bool WriteAttempted { get; init; }
}

public sealed record LmaxReadOnlyActivationManualMarketDataResponseReadResult(
    bool Success,
    string Category,
    string? SanitizedReasonCategory,
    string SanitizedRejectSubcategory,
    string SanitizedStatus,
    int EntryCount,
    bool RawFixSerialized,
    bool CredentialValuesReturned)
{
    public string SessionRejectRefTagIdSanitizedCategory { get; init; } = "RefTagID_NotAvailable";

    public string SessionRejectReasonSanitizedCategory { get; init; } = "SessionRejectReason_NotAvailable";

    public string SessionRejectRefMsgTypeSanitizedCategory { get; init; } = "RefMsgType_NotAvailable";

    public bool LogoutObserved { get; init; }

    public string LogoutSourceCategory { get; init; } = "LogoutEvidenceInconclusiveSafe";

    public string LogoutReasonSanitizedCategory { get; init; } = "LogoutReasonNotAvailable";

    public bool? LogoutTextPresentSanitized { get; init; }

    public string? LogoutTimingCategory { get; init; }

    public string LogoutReasonExtractionSource { get; init; } = "LogoutReasonNotAvailable";
}

public sealed record LmaxReadOnlyActivationManualMarketDataSessionRejectEvidence(
    string Category,
    string SanitizedReasonCategory,
    string SanitizedRejectSubcategory,
    bool MarketClosedOrSessionUnavailablePlausible,
    bool NoEntriesOutOfHoursPlausible,
    bool ParserClassifierFalsePositiveNotExcluded,
    bool InconclusiveSafe,
    bool RawFixSerialized,
    bool CredentialValuesReturned)
{
    public string SessionRejectRefTagIdSanitizedCategory { get; init; } = "RefTagID_NotAvailable";

    public string SessionRejectReasonSanitizedCategory { get; init; } = "SessionRejectReason_NotAvailable";

    public string SessionRejectRefMsgTypeSanitizedCategory { get; init; } = "RefMsgType_NotAvailable";
}

public sealed record LmaxReadOnlyActivationManualMarketDataRequestFieldPresence(
    bool BeginStringFix44Present,
    bool BodyLengthPresent,
    bool MarketDataRequestMessageCategoryPresent,
    bool MsgSeqNumPresent,
    bool SenderCompIdPresent,
    bool TargetCompIdPresent,
    bool SendingTimePresent,
    bool MdReqIdPresent,
    bool SnapshotSubscriptionTypePresent,
    bool SnapshotPlusUpdatesSubscriptionTypePresent,
    bool MarketDepthPresent,
    bool MdUpdateTypePresent,
    bool BidAndOfferEntryTypesPresent,
    bool RelatedSymbolsPresent,
    bool NonBatchedSingleInstrumentRequests,
    bool AllApprovedInstrumentsRepresentedAcrossRequests,
    bool SecurityIdPresentForAllApprovedInstruments,
    bool SecurityIdSourcePresentForAllApprovedInstruments,
    bool SecurityIdOnlyShape,
    bool SymbolTextPresent,
    bool CheckSumPresent)
{
    public static LmaxReadOnlyActivationManualMarketDataRequestFieldPresence Empty { get; } = new(
        BeginStringFix44Present: false,
        BodyLengthPresent: false,
        MarketDataRequestMessageCategoryPresent: false,
        MsgSeqNumPresent: false,
        SenderCompIdPresent: false,
        TargetCompIdPresent: false,
        SendingTimePresent: false,
        MdReqIdPresent: false,
        SnapshotSubscriptionTypePresent: false,
        SnapshotPlusUpdatesSubscriptionTypePresent: false,
        MarketDepthPresent: false,
        MdUpdateTypePresent: false,
        BidAndOfferEntryTypesPresent: false,
        RelatedSymbolsPresent: false,
        NonBatchedSingleInstrumentRequests: false,
        AllApprovedInstrumentsRepresentedAcrossRequests: false,
        SecurityIdPresentForAllApprovedInstruments: false,
        SecurityIdSourcePresentForAllApprovedInstruments: false,
        SecurityIdOnlyShape: false,
        SymbolTextPresent: false,
        CheckSumPresent: false);
}

public sealed record LmaxReadOnlyActivationManualMarketDataRequestWriterBindingValidation(
    string BindingName,
    string AdapterMode,
    bool WriterReady,
    bool ReadOnlyOnly,
    bool OrderFramesSupported,
    bool NewOrderSingleSupported,
    bool CancelReplaceSupported,
    bool ExecutionReportFillOrderLifecycleParsingSupported,
    bool RawFixSerialized,
    bool CredentialValuesReturned,
    bool ExternalBoundaryAttemptedDuringValidation);

public sealed record LmaxReadOnlyActivationManualMarketDataRequestOperationBindingValidation(
    string BindingName,
    string AdapterMode,
    bool MarketDataOperationNotConfiguredCleared,
    bool MarketDataRequestOperationReady,
    bool MarketDataRequestBuilderReady,
    bool MarketDataRequestWriterReady,
    bool FixSessionSuccessGateRequired,
    bool FixSessionSuccessGateSatisfiedForValidation,
    bool ApprovedInstrumentScopeExact,
    bool NonApprovedInstrumentsRejected,
    bool UsdJpySecurityIdPreserved,
    bool UsdJpyCaveatPreserved,
    bool ReadOnlyOnly,
    bool RequestMessageCategoryPresent,
    bool MdReqIdPresent,
    bool SnapshotSubscriptionTypePresent,
    bool MarketDepthPresent,
    bool BidAndOfferEntryTypesPresent,
    bool RelatedSymbolsPresent,
    bool SecurityIdSourcePresentForAllApprovedInstruments,
    bool SecurityIdPresentForAllApprovedInstruments,
    bool OrderFramesSupported,
    bool NewOrderSingleSupported,
    bool CancelReplaceSupported,
    bool ExecutionReportFillOrderLifecycleParsingSupported,
    bool RawFixSerialized,
    bool CredentialValuesReturned,
    bool RawCredentialsSerialized,
    bool RawSessionIdentifiersSerialized,
    bool ApiWorkerReachable,
    bool NoExternalDefaultPreserved,
    bool ExternalBoundaryAttemptedDuringValidation,
    bool MarketDataResponseBlockedUntilRequestSuccess,
    bool MarketDataResponseReaderReady,
    bool MarketDataResponseParserClassifierReady,
    bool MarketDataResponseBoundedReadWaitReady,
    bool MarketDataResponseReadBlockedUntilRequestSuccess,
    IReadOnlyList<string> SupportedMarketDataResponseCategories,
    string ShapeProfileName,
    bool LegacyRejectedProfileRepresented,
    bool RepairedProfileSelected,
    bool SnapshotPlusUpdatesSubscriptionTypePresent,
    bool SecurityIdOnlyShape,
    bool SymbolTextPresent,
    bool NonBatchedSingleInstrumentRequests,
    bool AllApprovedInstrumentsRepresentedAcrossRequests,
    bool MdUpdateTypePresent,
    string SanitizedStatus,
    string? SanitizedErrorCategory);

public sealed record LmaxReadOnlyActivationManualMarketDataResponseReaderBindingValidation(
    string BindingName,
    string AdapterMode,
    bool ReaderReady,
    bool ParserClassifierReady,
    bool BoundedReadWaitReady,
    bool ResponseReadBlockedUntilRequestSuccess,
    bool ReadOnlyOnly,
    bool OrderFramesSupported,
    bool NewOrderSingleSupported,
    bool CancelReplaceSupported,
    bool ExecutionReportFillOrderLifecycleParsingSupported,
    bool RawFixSerialized,
    bool CredentialValuesReturned,
    bool ExternalBoundaryAttemptedDuringValidation,
    IReadOnlyList<string> SupportedCategories);
