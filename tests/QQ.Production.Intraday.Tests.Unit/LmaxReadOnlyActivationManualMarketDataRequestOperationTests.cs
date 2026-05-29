using System.Text;
using System.Text.Json;
using QQ.Production.Intraday.Infrastructure.Lmax;
using QQ.Production.Intraday.Tools.LmaxReadOnlyActivation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlyActivationManualMarketDataRequestOperationTests
{
    private static readonly Encoding FixEncoding = Encoding.ASCII;

    [Fact]
    public void R120_root_cause_is_reproduced_by_unbound_marketdata_client()
    {
        var client = new LmaxRealReadOnlyMarketDataFrameClient();

        var result = client.RequestReadOnlyStatus(ApprovedMarketDataOptions(), ApprovedRetryScope("LMAX-R119"));

        Assert.Equal("MarketDataClientExecutionDependencyMissing", result.SanitizedStatus);
        Assert.Equal("MarketDataOperationNotConfigured", result.SanitizedErrorCategory);
        Assert.All(result.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, x.MarketDataBoundary));
    }

    [Fact]
    public void Approved_marketdata_request_builder_and_writer_are_ready_for_manual_real_bounded_path()
    {
        var validation = LmaxReadOnlyActivationManualMarketDataRequestOperation.ValidateBinding(
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            fixSessionAcknowledged: true,
            SyntheticCredentialReader);
        var json = JsonSerializer.Serialize(validation);

        Assert.True(validation.MarketDataOperationNotConfiguredCleared);
        Assert.True(validation.MarketDataRequestOperationReady);
        Assert.True(validation.MarketDataRequestBuilderReady);
        Assert.True(validation.MarketDataRequestWriterReady);
        Assert.True(validation.FixSessionSuccessGateRequired);
        Assert.True(validation.FixSessionSuccessGateSatisfiedForValidation);
        Assert.True(validation.ApprovedInstrumentScopeExact);
        Assert.True(validation.NonApprovedInstrumentsRejected);
        Assert.True(validation.UsdJpySecurityIdPreserved);
        Assert.True(validation.UsdJpyCaveatPreserved);
        Assert.True(validation.ReadOnlyOnly);
        Assert.True(validation.RequestMessageCategoryPresent);
        Assert.True(validation.MdReqIdPresent);
        Assert.True(validation.SnapshotSubscriptionTypePresent);
        Assert.True(validation.MarketDepthPresent);
        Assert.True(validation.BidAndOfferEntryTypesPresent);
        Assert.True(validation.RelatedSymbolsPresent);
        Assert.True(validation.SecurityIdPresentForAllApprovedInstruments);
        Assert.True(validation.SecurityIdSourcePresentForAllApprovedInstruments);
        Assert.False(validation.OrderFramesSupported);
        Assert.False(validation.NewOrderSingleSupported);
        Assert.False(validation.CancelReplaceSupported);
        Assert.False(validation.ExecutionReportFillOrderLifecycleParsingSupported);
        Assert.False(validation.RawFixSerialized);
        Assert.False(validation.CredentialValuesReturned);
        Assert.False(validation.RawCredentialsSerialized);
        Assert.False(validation.RawSessionIdentifiersSerialized);
        Assert.False(validation.ApiWorkerReachable);
        Assert.True(validation.NoExternalDefaultPreserved);
        Assert.False(validation.ExternalBoundaryAttemptedDuringValidation);
        Assert.True(validation.MarketDataResponseBlockedUntilRequestSuccess);
        Assert.True(validation.MarketDataResponseReaderReady);
        Assert.True(validation.MarketDataResponseParserClassifierReady);
        Assert.True(validation.MarketDataResponseBoundedReadWaitReady);
        Assert.True(validation.MarketDataResponseReadBlockedUntilRequestSuccess);
        Assert.Contains("MarketDataSnapshotObserved", validation.SupportedMarketDataResponseCategories);
        Assert.Contains("MarketDataIncrementalObserved", validation.SupportedMarketDataResponseCategories);
        Assert.Contains("MarketDataRejectObserved", validation.SupportedMarketDataResponseCategories);
        Assert.Contains("MarketDataNoEntriesObserved", validation.SupportedMarketDataResponseCategories);
        Assert.Contains("MarketDataReadTimeout", validation.SupportedMarketDataResponseCategories);
        Assert.Contains("MarketDataMalformedFrame", validation.SupportedMarketDataResponseCategories);
        Assert.Contains("MarketDataUnknownFailure", validation.SupportedMarketDataResponseCategories);
        Assert.Contains("MarketDataResponseNotAttempted", validation.SupportedMarketDataResponseCategories);
        Assert.DoesNotContain("r121-synthetic-sender", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("r121-synthetic-target", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Marketdata_request_frame_contains_only_approved_readonly_scope_in_memory()
    {
        var builder = new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader);

        var frame = builder.BuildRequestFrame(ApprovedMarketDataOptions(), ApprovedRetryScope("LMAX-R119"));
        var fields = ParseFields(frame.FrameBytes);
        var text = FixEncoding.GetString(frame.FrameBytes);

        Assert.True(frame.Built);
        Assert.True(frame.ReadOnlyOnly);
        Assert.True(frame.ApprovedInstrumentScopeExact);
        Assert.True(frame.UsdJpyCaveatPreserved);
        Assert.False(frame.OrderFramesSupported);
        Assert.False(frame.NewOrderSingleSupported);
        Assert.False(frame.CancelReplaceSupported);
        Assert.False(frame.ExecutionReportFillOrderLifecycleParsingSupported);
        Assert.False(frame.RawFixSerialized);
        Assert.False(frame.RawCredentialsSerialized);
        Assert.False(frame.RawSessionIdentifiersSerialized);
        Assert.False(frame.CredentialValuesReturned);
        Assert.Equal("FIX.4.4", fields["8"]);
        Assert.Equal("V", fields["35"]);
        Assert.Equal("0", fields["263"]);
        Assert.Equal("1", fields["264"]);
        Assert.Equal("4", fields["146"]);
        Assert.Equal(4, CountTag(text, "48"));
        Assert.Equal(4, CountTag(text, "22"));
        Assert.Equal(4, CountTag(text, "55"));
        Assert.Contains("4004", text, StringComparison.Ordinal);
        Assert.DoesNotContain("35=D", text, StringComparison.Ordinal);
        Assert.DoesNotContain("35=F", text, StringComparison.Ordinal);
        Assert.DoesNotContain("35=8", text, StringComparison.Ordinal);
        Assert.DoesNotContain("35=AE", text, StringComparison.Ordinal);
    }

    [Fact]
    public void R139_repaired_marketdata_request_profile_uses_snapshot_plus_updates_securityid_only_nonbatched_shape()
    {
        var builder = new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader);

        var frame = builder.BuildRequestFrame(RepairedMarketDataOptions(), ApprovedRetryScope("LMAX-R137"));
        var text = FixEncoding.GetString(frame.FrameBytes);

        Assert.True(frame.Built);
        Assert.True(frame.ReadOnlyOnly);
        Assert.True(frame.ApprovedInstrumentScopeExact);
        Assert.True(frame.UsdJpyCaveatPreserved);
        Assert.False(frame.RawFixSerialized);
        Assert.False(frame.RawCredentialsSerialized);
        Assert.False(frame.RawSessionIdentifiersSerialized);
        Assert.False(frame.CredentialValuesReturned);
        Assert.False(frame.OrderFramesSupported);
        Assert.False(frame.NewOrderSingleSupported);
        Assert.False(frame.CancelReplaceSupported);
        Assert.False(frame.ExecutionReportFillOrderLifecycleParsingSupported);
        Assert.Equal(4, CountTag(text, "35"));
        Assert.Equal(4, CountTag(text, "146"));
        Assert.Equal(4, CountTag(text, "263"));
        Assert.Equal(4, CountTagValue(text, "263", "1"));
        Assert.Equal(4, CountTag(text, "265"));
        Assert.Equal(4, CountTagValue(text, "265", "0"));
        Assert.Equal(4, CountTag(text, "48"));
        Assert.Equal(4, CountTag(text, "22"));
        Assert.Equal(0, CountTag(text, "55"));
    }

    [Fact]
    public void R139_shape_validation_represents_repaired_profile_and_legacy_rejected_profile_without_external_boundary()
    {
        var repaired = LmaxReadOnlyActivationManualMarketDataRequestOperation.ValidateBinding(
            RepairedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R137"),
            fixSessionAcknowledged: true,
            SyntheticCredentialReader);
        var legacy = LmaxReadOnlyActivationManualMarketDataRequestOperation.ValidateBinding(
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R137"),
            fixSessionAcknowledged: true,
            SyntheticCredentialReader);
        var repairedJson = JsonSerializer.Serialize(repaired);

        Assert.True(repaired.RepairedProfileSelected);
        Assert.False(repaired.LegacyRejectedProfileRepresented);
        Assert.Equal(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched, repaired.ShapeProfileName);
        Assert.True(repaired.SnapshotPlusUpdatesSubscriptionTypePresent);
        Assert.True(repaired.SecurityIdOnlyShape);
        Assert.False(repaired.SymbolTextPresent);
        Assert.True(repaired.NonBatchedSingleInstrumentRequests);
        Assert.True(repaired.AllApprovedInstrumentsRepresentedAcrossRequests);
        Assert.True(repaired.MdUpdateTypePresent);
        Assert.False(repaired.RawFixSerialized);
        Assert.False(repaired.CredentialValuesReturned);
        Assert.False(repaired.ExternalBoundaryAttemptedDuringValidation);
        Assert.False(repaired.OrderFramesSupported);
        Assert.False(repaired.ApiWorkerReachable);
        Assert.True(legacy.LegacyRejectedProfileRepresented);
        Assert.False(legacy.RepairedProfileSelected);
        Assert.Equal(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.LegacySnapshotOnlySymbolAndSecurityBatch, legacy.ShapeProfileName);
        Assert.True(legacy.SnapshotSubscriptionTypePresent);
        Assert.True(legacy.SymbolTextPresent);
        Assert.False(legacy.NonBatchedSingleInstrumentRequests);
        Assert.DoesNotContain("r121-synthetic-sender", repairedJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("r121-synthetic-target", repairedJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R144_ultraminimal_gbpusd_profile_builds_single_securityid_only_snapshot_plus_updates_request()
    {
        var builder = new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader);

        var frame = builder.BuildRequestFrame(UltraMinimalGbpusdMarketDataOptions(), ApprovedRetryScope("LMAX-R141"));
        var text = FixEncoding.GetString(frame.FrameBytes);

        Assert.True(frame.Built);
        Assert.True(frame.ReadOnlyOnly);
        Assert.True(frame.ApprovedInstrumentScopeExact);
        Assert.True(frame.UsdJpyCaveatPreserved);
        Assert.False(frame.RawFixSerialized);
        Assert.False(frame.RawCredentialsSerialized);
        Assert.False(frame.RawSessionIdentifiersSerialized);
        Assert.False(frame.CredentialValuesReturned);
        Assert.False(frame.OrderFramesSupported);
        Assert.False(frame.NewOrderSingleSupported);
        Assert.False(frame.CancelReplaceSupported);
        Assert.False(frame.ExecutionReportFillOrderLifecycleParsingSupported);
        Assert.Equal(1, CountTag(text, "35"));
        Assert.Equal(1, CountTag(text, "146"));
        Assert.Equal(1, CountTagValue(text, "146", "1"));
        Assert.Equal(1, CountTagValue(text, "263", "1"));
        Assert.Equal(1, CountTagValue(text, "264", "1"));
        Assert.Equal(0, CountTag(text, "265"));
        Assert.Equal(2, CountTag(text, "269"));
        Assert.Equal(1, CountTagValue(text, "269", "0"));
        Assert.Equal(1, CountTagValue(text, "269", "1"));
        Assert.Equal(1, CountTagValue(text, "48", "4002"));
        Assert.Equal(1, CountTagValue(text, "22", "8"));
        Assert.Equal(0, CountTag(text, "55"));
        Assert.DoesNotContain("GBPUSD", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EURGBP", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AUDUSD", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USDJPY", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4003", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4004", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4007", text, StringComparison.Ordinal);
    }

    [Fact]
    public void R144_ultraminimal_profile_is_explicit_and_prior_profiles_remain_represented()
    {
        var ultraminimal = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.FromOptions(UltraMinimalGbpusdMarketDataOptions());
        var repaired = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.Repaired();
        var legacy = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.Legacy();

        Assert.Equal(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument, ultraminimal.Name);
        Assert.Equal("1", ultraminimal.SubscriptionRequestType);
        Assert.False(ultraminimal.IncludeMdUpdateType);
        Assert.True(ultraminimal.MdUpdateTypeProfileControlled);
        Assert.False(ultraminimal.IncludeSymbolText);
        Assert.True(ultraminimal.NonBatchedSingleInstrumentRequests);
        Assert.True(ultraminimal.GbpusdOnlyDiagnosticProfile);
        Assert.False(ultraminimal.LegacyRejectedProfile);
        Assert.False(ultraminimal.RepairedProfile);
        Assert.Equal(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched, repaired.Name);
        Assert.Equal(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.LegacySnapshotOnlySymbolAndSecurityBatch, legacy.Name);
    }

    [Fact]
    public void R149_identifier_combination_profiles_are_gbpusd_only_and_preserve_ultraminimal_contract()
    {
        var builder = new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader);

        var symbolOnly = builder.BuildRequestFrame(SymbolOnlyGbpusdMarketDataOptions(), ApprovedRetryScope("LMAX-R147"));
        var symbolAndSecurity = builder.BuildRequestFrame(SymbolAndSecurityIdGbpusdMarketDataOptions(), ApprovedRetryScope("LMAX-R147"));
        var symbolOnlyText = FixEncoding.GetString(symbolOnly.FrameBytes);
        var symbolAndSecurityText = FixEncoding.GetString(symbolAndSecurity.FrameBytes);

        Assert.True(symbolOnly.Built);
        Assert.True(symbolAndSecurity.Built);
        Assert.True(symbolOnly.ApprovedInstrumentScopeExact);
        Assert.True(symbolAndSecurity.ApprovedInstrumentScopeExact);
        Assert.True(symbolOnly.UsdJpyCaveatPreserved);
        Assert.True(symbolAndSecurity.UsdJpyCaveatPreserved);
        Assert.False(symbolOnly.RawFixSerialized);
        Assert.False(symbolAndSecurity.RawFixSerialized);
        Assert.False(symbolOnly.CredentialValuesReturned);
        Assert.False(symbolAndSecurity.CredentialValuesReturned);
        Assert.Equal(1, CountTag(symbolOnlyText, "35"));
        Assert.Equal(1, CountTag(symbolAndSecurityText, "35"));
        Assert.Equal(1, CountTagValue(symbolOnlyText, "263", "1"));
        Assert.Equal(1, CountTagValue(symbolAndSecurityText, "263", "1"));
        Assert.Equal(0, CountTag(symbolOnlyText, "265"));
        Assert.Equal(0, CountTag(symbolAndSecurityText, "265"));
        Assert.Equal(1, CountTagValue(symbolOnlyText, "264", "1"));
        Assert.Equal(1, CountTagValue(symbolAndSecurityText, "264", "1"));
        Assert.Equal(1, CountTagValue(symbolOnlyText, "146", "1"));
        Assert.Equal(1, CountTagValue(symbolAndSecurityText, "146", "1"));
        Assert.Equal(2, CountTag(symbolOnlyText, "269"));
        Assert.Equal(2, CountTag(symbolAndSecurityText, "269"));
        Assert.Equal(1, CountTag(symbolOnlyText, "55"));
        Assert.Equal(1, CountTag(symbolAndSecurityText, "55"));
        Assert.Equal(0, CountTag(symbolOnlyText, "48"));
        Assert.Equal(0, CountTag(symbolOnlyText, "22"));
        Assert.Equal(1, CountTagValue(symbolAndSecurityText, "48", "4002"));
        Assert.Equal(1, CountTagValue(symbolAndSecurityText, "22", "8"));
        Assert.DoesNotContain("EURGBP", symbolOnlyText + symbolAndSecurityText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AUDUSD", symbolOnlyText + symbolAndSecurityText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USDJPY", symbolOnlyText + symbolAndSecurityText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4003", symbolOnlyText + symbolAndSecurityText, StringComparison.Ordinal);
        Assert.DoesNotContain("4004", symbolOnlyText + symbolAndSecurityText, StringComparison.Ordinal);
        Assert.DoesNotContain("4007", symbolOnlyText + symbolAndSecurityText, StringComparison.Ordinal);
        Assert.DoesNotContain("35=D", symbolOnlyText + symbolAndSecurityText, StringComparison.Ordinal);
        Assert.DoesNotContain("35=8", symbolOnlyText + symbolAndSecurityText, StringComparison.Ordinal);
    }

    [Fact]
    public void R149_next_diagnostic_profile_selects_symbol_and_securityid_without_api_worker_regression()
    {
        var selected = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.FromOptions(SymbolAndSecurityIdGbpusdMarketDataOptions());
        var symbolOnly = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.FromOptions(SymbolOnlyGbpusdMarketDataOptions());
        var priorSecurityIdOnly = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.FromOptions(UltraMinimalGbpusdMarketDataOptions());
        var root = FindRepoRoot();
        var factory = File.ReadAllText(Path.Combine(root, "tools", "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation", "LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));

        Assert.Equal(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument, selected.Name);
        Assert.True(selected.IncludeSecurityIdFields);
        Assert.True(selected.IncludeSymbolText);
        Assert.True(selected.GbpusdOnlyDiagnosticProfile);
        Assert.False(selected.IncludeMdUpdateType);
        Assert.True(selected.MdUpdateTypeProfileControlled);
        Assert.Equal(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesSymbolOnlyGbpusdSingleInstrument, symbolOnly.Name);
        Assert.False(symbolOnly.IncludeSecurityIdFields);
        Assert.True(symbolOnly.IncludeSymbolText);
        Assert.Equal(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument, priorSecurityIdOnly.Name);
        Assert.True(factory.Contains(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument, StringComparison.Ordinal));
        Assert.DoesNotContain("LmaxReadOnlyActivationManualMarketDataRequestOperation", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxReadOnlyActivationManualMarketDataRequestOperation", workerProgram, StringComparison.Ordinal);
        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void R153_subscription_lifecycle_profile_preserves_symbol_securityid_gbpusd_contract_no_externally()
    {
        var builder = new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader);

        var frame = builder.BuildRequestFrame(FreshLifecycleSymbolAndSecurityIdGbpusdMarketDataOptions(), ApprovedRetryScope("LMAX-R151"));
        var text = FixEncoding.GetString(frame.FrameBytes);
        var profile = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.FromOptions(FreshLifecycleSymbolAndSecurityIdGbpusdMarketDataOptions());
        var validation = LmaxReadOnlyActivationManualMarketDataRequestOperation.ValidateBinding(
            FreshLifecycleSymbolAndSecurityIdGbpusdMarketDataOptions(),
            ApprovedRetryScope("LMAX-R151"),
            fixSessionAcknowledged: true,
            SyntheticCredentialReader);
        var json = JsonSerializer.Serialize(validation);

        Assert.True(frame.Built);
        Assert.True(frame.ReadOnlyOnly);
        Assert.True(frame.ApprovedInstrumentScopeExact);
        Assert.True(frame.UsdJpyCaveatPreserved);
        Assert.False(frame.RawFixSerialized);
        Assert.False(frame.RawCredentialsSerialized);
        Assert.False(frame.RawSessionIdentifiersSerialized);
        Assert.False(frame.CredentialValuesReturned);
        Assert.Equal(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesFreshLifecycleSymbolAndSecurityIdGbpusdSingleInstrument, profile.Name);
        Assert.Equal("1", profile.SubscriptionRequestType);
        Assert.True(profile.IncludeSymbolText);
        Assert.True(profile.IncludeSecurityIdFields);
        Assert.True(profile.GbpusdOnlyDiagnosticProfile);
        Assert.True(profile.NonBatchedSingleInstrumentRequests);
        Assert.False(profile.IncludeMdUpdateType);
        Assert.True(profile.MdUpdateTypeProfileControlled);
        Assert.Equal(1, CountTag(text, "35"));
        Assert.Equal(1, CountTagValue(text, "263", "1"));
        Assert.Equal(0, CountTagValue(text, "263", "0"));
        Assert.Contains("LMAX_READONLY_R153_", text, StringComparison.Ordinal);
        Assert.Equal(0, CountTag(text, "265"));
        Assert.Equal(1, CountTagValue(text, "264", "1"));
        Assert.Equal(2, CountTag(text, "269"));
        Assert.Equal(1, CountTagValue(text, "269", "0"));
        Assert.Equal(1, CountTagValue(text, "269", "1"));
        Assert.Equal(1, CountTagValue(text, "146", "1"));
        Assert.Equal(1, CountTagValue(text, "48", "4002"));
        Assert.Equal(1, CountTagValue(text, "22", "8"));
        Assert.Equal(1, CountTag(text, "55"));
        Assert.DoesNotContain("EURGBP", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AUDUSD", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USDJPY", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4003", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4004", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4007", text, StringComparison.Ordinal);
        Assert.False(validation.ExternalBoundaryAttemptedDuringValidation);
        Assert.False(validation.RawFixSerialized);
        Assert.False(validation.CredentialValuesReturned);
        Assert.DoesNotContain("r121-synthetic-sender", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("r121-synthetic-target", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R158_mdupdatetype_required_profile_uses_docs_backed_securityidonly_gbpusd_contract_no_externally()
    {
        var builder = new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader);

        var frame = builder.BuildRequestFrame(MdUpdateTypeRequiredSecurityIdOnlyGbpusdMarketDataOptions(), ApprovedRetryScope("LMAX-R155"));
        var text = FixEncoding.GetString(frame.FrameBytes);
        var fields = ParseFields(frame.FrameBytes);
        var profile = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.FromOptions(MdUpdateTypeRequiredSecurityIdOnlyGbpusdMarketDataOptions());
        var validation = LmaxReadOnlyActivationManualMarketDataRequestOperation.ValidateBinding(
            MdUpdateTypeRequiredSecurityIdOnlyGbpusdMarketDataOptions(),
            ApprovedRetryScope("LMAX-R155"),
            fixSessionAcknowledged: true,
            SyntheticCredentialReader);
        var root = FindRepoRoot();
        var factory = File.ReadAllText(Path.Combine(root, "tools", "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation", "LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));
        var json = JsonSerializer.Serialize(validation);

        Assert.True(frame.Built);
        Assert.True(frame.ReadOnlyOnly);
        Assert.True(frame.ApprovedInstrumentScopeExact);
        Assert.True(frame.UsdJpyCaveatPreserved);
        Assert.False(frame.RawFixSerialized);
        Assert.False(frame.RawCredentialsSerialized);
        Assert.False(frame.RawSessionIdentifiersSerialized);
        Assert.False(frame.CredentialValuesReturned);
        Assert.Equal(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument, profile.Name);
        Assert.Equal("1", profile.SubscriptionRequestType);
        Assert.True(profile.IncludeSecurityIdFields);
        Assert.False(profile.IncludeSymbolText);
        Assert.True(profile.IncludeMdUpdateType);
        Assert.False(profile.MdUpdateTypeProfileControlled);
        Assert.True(profile.GbpusdOnlyDiagnosticProfile);
        Assert.True(profile.NonBatchedSingleInstrumentRequests);
        Assert.Equal(1, CountTag(text, "35"));
        Assert.Equal(1, CountTagValue(text, "263", "1"));
        Assert.Equal(0, CountTagValue(text, "263", "0"));
        Assert.True(fields.TryGetValue("262", out var mdReqId));
        AssertRepairedMdReqIdShape(mdReqId);
        Assert.DoesNotContain(mdReqId, JsonSerializer.Serialize(frame), StringComparison.Ordinal);
        Assert.Equal(1, CountTagValue(text, "265", "0"));
        Assert.Equal(1, CountTagValue(text, "264", "1"));
        Assert.Equal(1, CountTagValue(text, "267", "2"));
        Assert.Equal(2, CountTag(text, "269"));
        Assert.Equal(1, CountTagValue(text, "269", "0"));
        Assert.Equal(1, CountTagValue(text, "269", "1"));
        Assert.Equal(1, CountTagValue(text, "146", "1"));
        Assert.Equal(1, CountTagValue(text, "48", "4002"));
        Assert.Equal(1, CountTagValue(text, "22", "8"));
        Assert.Equal(0, CountTag(text, "55"));
        Assert.DoesNotContain("GBPUSD", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EURGBP", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AUDUSD", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USDJPY", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4003", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4004", text, StringComparison.Ordinal);
        Assert.DoesNotContain("4007", text, StringComparison.Ordinal);
        Assert.True(validation.MdUpdateTypePresent);
        Assert.True(validation.SnapshotPlusUpdatesSubscriptionTypePresent);
        Assert.True(validation.SecurityIdOnlyShape);
        Assert.False(validation.SymbolTextPresent);
        Assert.False(validation.ExternalBoundaryAttemptedDuringValidation);
        Assert.False(validation.RawFixSerialized);
        Assert.False(validation.CredentialValuesReturned);
        Assert.Contains(LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument, factory, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxReadOnlyActivationManualMarketDataRequestOperation", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxReadOnlyActivationManualMarketDataRequestOperation", workerProgram, StringComparison.Ordinal);
        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("r121-synthetic-sender", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("r121-synthetic-target", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R197_mdreqid_value_repair_preserves_short_alphanumeric_unique_shape_no_externally()
    {
        var builder = new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader);
        var generated = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < 25; index++)
        {
            var frame = builder.BuildRequestFrame(MdUpdateTypeRequiredSecurityIdOnlyGbpusdMarketDataOptions(), ApprovedRetryScope("LMAX-R155"));
            var fields = ParseFields(frame.FrameBytes);
            Assert.True(fields.TryGetValue("262", out var mdReqId));

            AssertRepairedMdReqIdShape(mdReqId);
            Assert.True(generated.Add(mdReqId), "Repaired MDReqID values must remain unique across generated requests.");
            Assert.DoesNotContain(mdReqId, JsonSerializer.Serialize(frame), StringComparison.Ordinal);
        }

        Assert.Equal(25, generated.Count);
    }

    [Fact]
    public void Marketdata_operation_remains_blocked_without_fix_session_success()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(MarketDataSnapshotFrame(entryCount: 2));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: false,
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            CancellationToken.None);

        Assert.Equal("FixSessionAcknowledgementRequired", result.SanitizedErrorCategory);
        Assert.Null(result.MarketDataEntriesObserved);
        Assert.Null(result.MarketDataSanitizedEntryCount);
        Assert.Equal("EntriesEvidenceInconclusiveSafe", result.MarketDataEntriesEvidenceCategory);
        Assert.Equal("MarketDataRequestNotCompleted", result.MarketDataEntriesReportingSource);
        Assert.Equal("FixSessionAcknowledgementRequired", result.MarketDataEntriesNotAvailableReason);
        Assert.Equal(0, stream.Length);
        Assert.All(result.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, x.MarketDataBoundary));
    }

    [Fact]
    public void Marketdata_operation_writes_to_in_memory_sink_only_after_fix_success()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(MarketDataSnapshotFrame(entryCount: 2));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            CancellationToken.None);
        var text = FixEncoding.GetString(stream.WrittenBytes);

        Assert.Equal("ManualMarketDataSnapshotObservedSanitized", result.SanitizedStatus);
        Assert.Null(result.SanitizedErrorCategory);
        Assert.True(stream.WrittenBytes.Length > 0);
        Assert.Contains("35=V", text, StringComparison.Ordinal);
        Assert.All(result.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.Succeeded, x.MarketDataBoundary));
        Assert.All(result.InstrumentStatuses, x => Assert.Equal(2, x.MarketDataSnapshotCount));
        Assert.True(result.MarketDataEntriesObserved);
        Assert.Equal(2, result.MarketDataSanitizedEntryCount);
        Assert.Equal("EntriesObservedWithSanitizedCount", result.MarketDataEntriesEvidenceCategory);
        Assert.Equal("MarketDataResponseParserClassifierEntryCount", result.MarketDataEntriesReportingSource);
        Assert.Null(result.MarketDataEntriesNotAvailableReason);
        Assert.Contains(result.InstrumentStatuses, x =>
            x.Symbol == "USDJPY" &&
            x.SecurityId == "4004" &&
            x.SecurityIdSource == "8" &&
            x.Caveat == LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat);
    }

    [Fact]
    public void R207_remaining_approved_instruments_are_requested_sequentially_with_sanitized_entry_counts()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new ChunkedDuplexMarketDataStream(
            MarketDataSnapshotFrame(entryCount: 2),
            MarketDataSnapshotFrame(entryCount: 2),
            MarketDataSnapshotFrame(entryCount: 2));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            MdUpdateTypeRequiredSecurityIdOnlyGbpusdMarketDataOptions(),
            ApprovedRetryScope("LMAX-R207"),
            CancellationToken.None);
        var text = FixEncoding.GetString(stream.WrittenBytes);

        Assert.Null(result.SanitizedErrorCategory);
        Assert.Equal("SequentialRemainingApprovedInstrumentMarketDataSucceededSanitized", result.SanitizedStatus);
        Assert.True(result.MarketDataRequestWriteAttempted);
        Assert.True(result.MarketDataRequestWriteSucceeded);
        Assert.True(result.MarketDataRequestResponseReadAttempted);
        Assert.True(result.MarketDataRequestReachedBoundedResponseClassification);
        Assert.True(result.MarketDataEntriesObserved);
        Assert.Equal(6, result.MarketDataSanitizedEntryCount);
        Assert.Equal("EntriesObservedWithSanitizedCount", result.MarketDataEntriesEvidenceCategory);
        Assert.Equal("SequentialRemainingApprovedInstrumentMarketDataResponseParserClassifierEntryCount", result.MarketDataEntriesReportingSource);
        Assert.Equal("NoRejectObserved", result.RejectReasonExtractionSource);

        Assert.Equal(3, result.InstrumentStatuses.Count);
        Assert.Contains(result.InstrumentStatuses, x => x.Symbol == "EURGBP" && x.SecurityId == "4003" && x.SecurityIdSource == "8" && x.MarketDataSnapshotCount == 2);
        Assert.Contains(result.InstrumentStatuses, x => x.Symbol == "AUDUSD" && x.SecurityId == "4007" && x.SecurityIdSource == "8" && x.MarketDataSnapshotCount == 2);
        Assert.Contains(result.InstrumentStatuses, x => x.Symbol == "USDJPY" && x.SecurityId == "4004" && x.SecurityIdSource == "8" && x.Caveat == LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.UsdJpyCaveat && x.MarketDataSnapshotCount == 2);
        Assert.DoesNotContain(result.InstrumentStatuses, x => x.Symbol == "GBPUSD");

        Assert.Equal(3, CountTagValue(text, "35", "V"));
        Assert.Equal(3, CountTagValue(text, "146", "1"));
        Assert.Equal(3, CountTagValue(text, "263", "1"));
        Assert.Equal(3, CountTagValue(text, "265", "0"));
        Assert.Contains("48=4003", text, StringComparison.Ordinal);
        Assert.Contains("48=4007", text, StringComparison.Ordinal);
        Assert.Contains("48=4004", text, StringComparison.Ordinal);
        Assert.DoesNotContain("48=4002", text, StringComparison.Ordinal);
        Assert.DoesNotContain("55=", text, StringComparison.Ordinal);
        Assert.Equal(3, CountTag(text, "262"));
    }

    [Theory]
    [InlineData("LMAX-R211")]
    [InlineData("LMAX-R215")]
    [InlineData("LMAX-R221")]
    public void Audusd_only_retry_phases_use_repaired_single_instrument_contract(string phase)
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(MarketDataSnapshotFrame(entryCount: 2));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            MdUpdateTypeRequiredSecurityIdOnlyGbpusdMarketDataOptions(),
            ApprovedRetryScope(phase),
            CancellationToken.None);
        var text = FixEncoding.GetString(stream.WrittenBytes);

        Assert.Null(result.SanitizedErrorCategory);
        Assert.Equal("AudUsdOnlyInstrumentMarketDataSucceededSanitized", result.SanitizedStatus);
        Assert.True(result.MarketDataRequestWriteAttempted);
        Assert.True(result.MarketDataRequestWriteSucceeded);
        Assert.True(result.MarketDataRequestResponseReadAttempted);
        Assert.True(result.MarketDataRequestReachedBoundedResponseClassification);
        Assert.True(result.MarketDataEntriesObserved);
        Assert.Equal(2, result.MarketDataSanitizedEntryCount);
        Assert.Equal("EntriesObservedWithSanitizedCount", result.MarketDataEntriesEvidenceCategory);
        Assert.Equal("NoRejectObserved", result.RejectReasonExtractionSource);

        Assert.Single(result.InstrumentStatuses);
        Assert.Contains(result.InstrumentStatuses, x => x.Symbol == "AUDUSD" && x.SecurityId == "4007" && x.SecurityIdSource == "8" && x.MarketDataSnapshotCount == 2);
        Assert.DoesNotContain(result.InstrumentStatuses, x => x.Symbol == "GBPUSD" || x.Symbol == "EURGBP" || x.Symbol == "USDJPY");

        Assert.Equal(1, CountTagValue(text, "35", "V"));
        Assert.Equal(1, CountTagValue(text, "146", "1"));
        Assert.Equal(1, CountTagValue(text, "263", "1"));
        Assert.Equal(1, CountTagValue(text, "265", "0"));
        Assert.Equal(1, CountTagValue(text, "264", "1"));
        Assert.Equal(1, CountTagValue(text, "267", "2"));
        Assert.Equal(1, CountTagValue(text, "48", "4007"));
        Assert.Equal(1, CountTagValue(text, "22", "8"));
        Assert.Equal(0, CountTag(text, "55"));
        Assert.DoesNotContain("48=4002", text, StringComparison.Ordinal);
        Assert.DoesNotContain("48=4003", text, StringComparison.Ordinal);
        Assert.DoesNotContain("48=4004", text, StringComparison.Ordinal);
        Assert.Equal(1, CountTag(text, "262"));
    }

    [Fact]
    public void Marketdata_operation_reports_zero_entries_as_safe_no_entries_evidence()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(MarketDataSnapshotFrame(entryCount: 0));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            CancellationToken.None);

        Assert.Equal("ManualMarketDataSnapshotNoEntriesObservedSanitized", result.SanitizedStatus);
        Assert.False(result.MarketDataEntriesObserved);
        Assert.Equal(0, result.MarketDataSanitizedEntryCount);
        Assert.Equal("NoEntriesObserved", result.MarketDataEntriesEvidenceCategory);
        Assert.Equal("MarketDataResponseParserClassifierEntryCount", result.MarketDataEntriesReportingSource);
    }

    [Fact]
    public void R163_successful_write_with_reject_response_reports_distinct_write_read_and_classification_flags()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(FixEncoding.GetBytes("35=3\u000158=missing required tag\u0001"));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            MdUpdateTypeRequiredSecurityIdOnlyGbpusdMarketDataOptions(),
            ApprovedRetryScope("LMAX-R161"),
            CancellationToken.None);
        var json = JsonSerializer.Serialize(result);

        Assert.True(result.MarketDataRequestWriteAttempted);
        Assert.True(result.MarketDataRequestWriteSucceeded);
        Assert.True(result.MarketDataRequestResponseReadAttempted);
        Assert.True(result.MarketDataRequestReachedBoundedResponseClassification);
        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.SanitizedErrorCategory);
        Assert.Equal("MalformedOrUnsupportedMarketDataRequestPlausible", result.SanitizedErrorMessage);
        Assert.False(result.MarketDataEntriesObserved);
        Assert.Equal(0, result.MarketDataSanitizedEntryCount);
        Assert.Equal("NoEntriesObserved", result.MarketDataEntriesEvidenceCategory);
        Assert.True(stream.WrittenBytes.Length > 0);
        Assert.DoesNotContain("missing required tag", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("35=3", json, StringComparison.Ordinal);
    }

    [Fact]
    public void R163_no_write_attempted_produces_no_response_read_or_bounded_classification()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(MarketDataSnapshotFrame(entryCount: 2));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: false,
            MdUpdateTypeRequiredSecurityIdOnlyGbpusdMarketDataOptions(),
            ApprovedRetryScope("LMAX-R161"),
            CancellationToken.None);

        Assert.False(result.MarketDataRequestWriteAttempted);
        Assert.False(result.MarketDataRequestWriteSucceeded);
        Assert.False(result.MarketDataRequestResponseReadAttempted);
        Assert.False(result.MarketDataRequestReachedBoundedResponseClassification);
        Assert.Equal("FixSessionAcknowledgementRequired", result.SanitizedErrorCategory);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void R163_write_failure_does_not_collapse_into_response_classification()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());

        var result = operation.RequestReadOnlyMarketData(
            stream: null,
            fixSessionAcknowledged: true,
            MdUpdateTypeRequiredSecurityIdOnlyGbpusdMarketDataOptions(),
            ApprovedRetryScope("LMAX-R161"),
            CancellationToken.None);

        Assert.False(result.MarketDataRequestWriteAttempted);
        Assert.False(result.MarketDataRequestWriteSucceeded);
        Assert.False(result.MarketDataRequestResponseReadAttempted);
        Assert.False(result.MarketDataRequestReachedBoundedResponseClassification);
        Assert.Equal("WritableFixSessionStreamUnavailable", result.SanitizedErrorCategory);
    }

    [Fact]
    public void R163_safety_snapshot_preserves_legacy_flag_but_exposes_authoritative_request_state_fields()
    {
        var snapshot = LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot.DryRunNoNetwork with
        {
            MarketDataRequestSent = false,
            MarketDataRequestSentLegacyFlag = false,
            MarketDataRequestWriteAttempted = true,
            MarketDataRequestWriteSucceeded = true,
            MarketDataRequestResponseReadAttempted = true,
            MarketDataRequestReachedBoundedResponseClassification = true
        };
        var json = JsonSerializer.Serialize(snapshot);

        Assert.False(snapshot.MarketDataRequestSent);
        Assert.False(snapshot.MarketDataRequestSentLegacyFlag);
        Assert.True(snapshot.MarketDataRequestWriteAttempted);
        Assert.True(snapshot.MarketDataRequestWriteSucceeded);
        Assert.True(snapshot.MarketDataRequestResponseReadAttempted);
        Assert.True(snapshot.MarketDataRequestReachedBoundedResponseClassification);
        Assert.Contains(nameof(LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot.MarketDataRequestSentLegacyFlag), json, StringComparison.Ordinal);
        Assert.Contains(nameof(LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot.MarketDataRequestWriteAttempted), json, StringComparison.Ordinal);
        Assert.DoesNotContain("35=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("58=", json, StringComparison.Ordinal);
    }

    [Fact]
    public void R163_pre_external_blocked_snapshot_leaves_external_write_read_flags_false()
    {
        var snapshot = LmaxTemporaryReadOnlyRuntimeAdapterSafetySnapshot.DryRunNoNetwork;

        Assert.False(snapshot.ExternalRunExecuted);
        Assert.False(snapshot.TcpConnectionAttempted);
        Assert.False(snapshot.TlsHandshakeAttempted);
        Assert.False(snapshot.FixLogonAttempted);
        Assert.False(snapshot.MarketDataRequestSent);
        Assert.False(snapshot.MarketDataRequestSentLegacyFlag);
        Assert.False(snapshot.MarketDataRequestWriteAttempted);
        Assert.False(snapshot.MarketDataRequestWriteSucceeded);
        Assert.False(snapshot.MarketDataRequestResponseReadAttempted);
        Assert.False(snapshot.MarketDataRequestReachedBoundedResponseClassification);
    }

    [Fact]
    public void Marketdata_response_reader_and_parser_are_ready_with_finite_bounded_wait()
    {
        var validation = LmaxReadOnlyActivationManualMarketDataResponseReader.ValidateBinding();
        var json = JsonSerializer.Serialize(validation);

        Assert.True(validation.ReaderReady);
        Assert.True(validation.ParserClassifierReady);
        Assert.True(validation.BoundedReadWaitReady);
        Assert.True(validation.ResponseReadBlockedUntilRequestSuccess);
        Assert.True(validation.ReadOnlyOnly);
        Assert.False(validation.OrderFramesSupported);
        Assert.False(validation.NewOrderSingleSupported);
        Assert.False(validation.CancelReplaceSupported);
        Assert.False(validation.ExecutionReportFillOrderLifecycleParsingSupported);
        Assert.False(validation.RawFixSerialized);
        Assert.False(validation.CredentialValuesReturned);
        Assert.False(validation.ExternalBoundaryAttemptedDuringValidation);
        Assert.Contains("MarketDataSnapshotObserved", validation.SupportedCategories);
        Assert.Contains("MarketDataResponseNotAttempted", validation.SupportedCategories);
        Assert.Contains("SessionRejectObservedWithoutReason", validation.SupportedCategories);
        Assert.Contains("SessionRejectObservedWithSanitizedReason", validation.SupportedCategories);
        Assert.Contains("MarketClosedOrSessionUnavailablePlausible", validation.SupportedCategories);
        Assert.Contains("NoEntriesOutOfHoursPlausible", validation.SupportedCategories);
        Assert.Contains("ParserClassifierFalsePositiveNotExcluded", validation.SupportedCategories);
        Assert.Contains("InconclusiveSafe", validation.SupportedCategories);
        Assert.DoesNotContain("r121-synthetic-sender", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("r121-synthetic-target", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("W", "MarketDataSnapshotObserved", true, 2)]
    [InlineData("X", "MarketDataIncrementalObserved", true, 2)]
    [InlineData("Y", "MarketDataRejectObserved", false, 0)]
    public void Marketdata_response_classifier_supports_sanitized_marketdata_categories(
        string messageType,
        string expectedCategory,
        bool expectedSuccess,
        int expectedEntryCount)
    {
        var frame = messageType is "W" or "X"
            ? MarketDataResponseFrame(messageType, entryCount: 2)
            : MarketDataResponseFrame(messageType, entryCount: 0);

        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(frame);
        var json = JsonSerializer.Serialize(result);

        Assert.Equal(expectedSuccess, result.Success);
        Assert.Equal(expectedCategory, result.Category);
        Assert.Equal(expectedEntryCount, result.EntryCount);
        Assert.False(result.RawFixSerialized);
        Assert.False(result.CredentialValuesReturned);
        Assert.DoesNotContain("r121-synthetic-sender", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("r121-synthetic-target", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0", "MarketDataRequestRejectUnknownSymbol", "InstrumentSecurityMappingRejectPlausible")]
    [InlineData("1", "MarketDataRequestRejectDuplicateMDReqID", "MalformedOrUnsupportedMarketDataRequestPlausible")]
    [InlineData("4", "MarketDataRequestRejectUnsupportedSubscriptionRequestType", "MalformedOrUnsupportedMarketDataRequestPlausible")]
    [InlineData("5", "MarketDataRequestRejectUnsupportedMarketDepth", "MalformedOrUnsupportedMarketDataRequestPlausible")]
    [InlineData("6", "MarketDataRequestRejectUnsupportedMDUpdateType", "MalformedOrUnsupportedMarketDataRequestPlausible")]
    [InlineData("8", "MarketDataRequestRejectUnsupportedMDEntryType", "MalformedOrUnsupportedMarketDataRequestPlausible")]
    [InlineData("9", "MarketDataRequestRejectReasonOtherSanitized", "MalformedOrUnsupportedMarketDataRequestPlausible")]
    public void Marketdata_request_reject_281_is_sanitized_to_doc_backed_subcategories_without_raw_fix(
        string mdReqRejectReason,
        string expectedSubcategory,
        string expectedReasonCategory)
    {
        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(
            FixEncoding.GetBytes($"35=Y\u0001262=r184-fixture\u0001281={mdReqRejectReason}\u000158=raw lmax reject detail\u0001"));
        var json = JsonSerializer.Serialize(result);

        Assert.False(result.Success);
        Assert.Equal("MarketDataRejectObserved", result.Category);
        Assert.Equal(expectedReasonCategory, result.SanitizedReasonCategory);
        Assert.Equal(expectedSubcategory, result.SanitizedRejectSubcategory);
        Assert.Contains(expectedSubcategory, LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.AllowedSanitizedRejectSubcategories);
        Assert.False(result.RawFixSerialized);
        Assert.False(result.CredentialValuesReturned);
        Assert.DoesNotContain("35=Y", json, StringComparison.Ordinal);
        Assert.DoesNotContain("281=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("raw lmax reject detail", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Marketdata_request_reject_without_281_uses_not_available_subcategory_without_raw_fix()
    {
        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(
            FixEncoding.GetBytes("35=Y\u0001262=r184-fixture\u000158=raw lmax reject detail\u0001"));
        var json = JsonSerializer.Serialize(result);

        Assert.False(result.Success);
        Assert.Equal("MarketDataRejectObserved", result.Category);
        Assert.Equal("MalformedOrUnsupportedMarketDataRequestPlausible", result.SanitizedReasonCategory);
        Assert.Equal("RejectReasonNotAvailable", result.SanitizedRejectSubcategory);
        Assert.False(result.RawFixSerialized);
        Assert.False(result.CredentialValuesReturned);
        Assert.DoesNotContain("35=Y", json, StringComparison.Ordinal);
        Assert.DoesNotContain("58=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("raw lmax reject detail", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Logout_msgtype5_with_text_reports_sanitized_text_presence_without_raw_logout_text()
    {
        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(
            FixEncoding.GetBytes("35=5\u000158=raw logout reason should not leak\u0001"));
        var json = JsonSerializer.Serialize(result);

        Assert.False(result.Success);
        Assert.Equal("LogoutObserved", result.Category);
        Assert.True(result.LogoutObserved);
        Assert.Equal("FixLogoutMsgType5", result.LogoutSourceCategory);
        Assert.Equal("LogoutTextPresentSanitized", result.LogoutReasonSanitizedCategory);
        Assert.True(result.LogoutTextPresentSanitized);
        Assert.Equal("LogoutAfterMarketDataRequest", result.LogoutTimingCategory);
        Assert.Equal("MsgType5LogoutTextPresentSanitized", result.LogoutReasonExtractionSource);
        Assert.False(result.RawFixSerialized);
        Assert.DoesNotContain("raw logout reason", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("58=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("35=5", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Logout_msgtype5_without_text_reports_not_available_distinct_from_failure()
    {
        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(
            FixEncoding.GetBytes("35=5\u0001"));

        Assert.Equal("LogoutObserved", result.Category);
        Assert.True(result.LogoutObserved);
        Assert.Equal("FixLogoutMsgType5", result.LogoutSourceCategory);
        Assert.Equal("LogoutReasonNotAvailable", result.LogoutReasonSanitizedCategory);
        Assert.False(result.LogoutTextPresentSanitized);
        Assert.Equal("MsgType5LogoutNoText", result.LogoutReasonExtractionSource);
    }

    [Fact]
    public void R207_logout_after_audusd_request_reports_sanitized_logout_position()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new ChunkedDuplexMarketDataStream(
            MarketDataSnapshotFrame(entryCount: 2),
            FixEncoding.GetBytes("35=5\u000158=raw audusd logout detail\u0001"));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            MdUpdateTypeRequiredSecurityIdOnlyGbpusdMarketDataOptions(),
            ApprovedRetryScope("LMAX-R207"),
            CancellationToken.None);
        var json = JsonSerializer.Serialize(result);

        Assert.Equal("LogoutObserved", result.SanitizedErrorCategory);
        Assert.True(result.LogoutObserved);
        Assert.Equal("FixLogoutMsgType5", result.LogoutSourceCategory);
        Assert.Equal("LogoutTextPresentSanitized", result.LogoutReasonSanitizedCategory);
        Assert.True(result.LogoutTextPresentSanitized);
        Assert.Equal("AUDUSD", result.LogoutAfterInstrument);
        Assert.Equal("4007", result.LogoutAfterSecurityIdSanitized);
        Assert.Equal("LogoutAfterMarketDataRequest", result.LogoutTimingCategory);
        Assert.Equal("MsgType5LogoutTextPresentSanitized", result.LogoutReasonExtractionSource);
        Assert.DoesNotContain("raw audusd logout detail", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Successful_marketdata_response_reports_no_logout()
    {
        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(
            MarketDataSnapshotFrame(entryCount: 2));

        Assert.True(result.Success);
        Assert.False(result.LogoutObserved);
        Assert.Equal("LogoutEvidenceInconclusiveSafe", result.LogoutSourceCategory);
        Assert.Equal("LogoutReasonNotAvailable", result.LogoutReasonSanitizedCategory);
        Assert.Equal("LogoutReasonNotAvailable", result.LogoutReasonExtractionSource);
    }

    [Fact]
    public void Marketdata_response_reader_classifies_no_entries_and_not_attempted_without_raw_fix_serialization()
    {
        var reader = new LmaxReadOnlyActivationManualMarketDataResponseReader();
        using var emptyStream = new MemoryStream();

        var notAttempted = reader.ReadResponse(
            emptyStream,
            requestSucceeded: false,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        var noEntries = reader.ReadResponse(
            emptyStream,
            requestSucceeded: true,
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.False(notAttempted.Success);
        Assert.Equal("MarketDataResponseNotAttempted", notAttempted.Category);
        Assert.False(notAttempted.RawFixSerialized);
        Assert.False(notAttempted.CredentialValuesReturned);
        Assert.False(noEntries.Success);
        Assert.Equal("MarketDataNoEntriesObserved", noEntries.Category);
        Assert.False(noEntries.RawFixSerialized);
        Assert.False(noEntries.CredentialValuesReturned);
    }

    [Fact]
    public void Marketdata_response_parser_rejects_malformed_and_order_lifecycle_messages_as_sanitized_failures()
    {
        var malformed = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(
            FixEncoding.GetBytes("268=1\u0001269=0\u0001"));
        var orderLifecycle = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(
            FixEncoding.GetBytes("35=D\u0001268=1\u0001269=0\u0001"));

        Assert.Equal("MarketDataMalformedFrame", malformed.Category);
        Assert.Equal("MarketDataMalformedFrame", orderLifecycle.Category);
        Assert.False(malformed.Success);
        Assert.False(orderLifecycle.Success);
        Assert.False(malformed.RawFixSerialized);
        Assert.False(orderLifecycle.RawFixSerialized);
    }

    [Fact]
    public void Sessionreject_without_text_is_enriched_as_without_reason_and_inconclusive_safe()
    {
        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(
            FixEncoding.GetBytes("35=3\u0001"));
        var evidence = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.ClassifySessionRejectEvidence(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["35"] = "3"
            });
        var json = JsonSerializer.Serialize(result);

        Assert.False(result.Success);
        Assert.Equal("SessionRejectObservedWithoutReason", result.Category);
        Assert.Equal("SessionRejectReasonNotAvailable", result.SanitizedReasonCategory);
        Assert.False(result.RawFixSerialized);
        Assert.False(result.CredentialValuesReturned);
        Assert.Equal("SessionRejectObservedWithoutReason", evidence.Category);
        Assert.True(evidence.ParserClassifierFalsePositiveNotExcluded);
        Assert.True(evidence.InconclusiveSafe);
        Assert.False(evidence.RawFixSerialized);
        Assert.DoesNotContain("35=3", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("35=3\u0001372=V\u0001373=5\u000158=raw lmax reject detail\u0001", "SessionRejectRefMsgTypeMarketDataRequest")]
    [InlineData("35=3\u0001371=265\u0001373=5\u000158=raw lmax reject detail\u0001", "SessionRejectRefTagIdPresentSanitized")]
    [InlineData("35=3\u0001373=5\u000158=raw lmax reject detail\u0001", "SessionRejectReasonPresentSanitized")]
    public void Sessionreject_371_372_373_tags_are_sanitized_to_subcategories_without_raw_values(
        string frame,
        string expectedSubcategory)
    {
        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(
            FixEncoding.GetBytes(frame));
        var json = JsonSerializer.Serialize(result);

        Assert.False(result.Success);
        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.Category);
        Assert.Equal("MalformedOrUnsupportedMarketDataRequestPlausible", result.SanitizedReasonCategory);
        Assert.Equal(expectedSubcategory, result.SanitizedRejectSubcategory);
        Assert.Contains(expectedSubcategory, LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.AllowedSanitizedRejectSubcategories);
        Assert.False(result.RawFixSerialized);
        Assert.False(result.CredentialValuesReturned);
        Assert.DoesNotContain("371=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("372=V", json, StringComparison.Ordinal);
        Assert.DoesNotContain("373=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("raw lmax reject detail", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sessionreject_text_is_mapped_to_sanitized_reason_without_serializing_raw_text()
    {
        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(
            FixEncoding.GetBytes("35=3\u000158=market closed for session\u0001"));
        var json = JsonSerializer.Serialize(result);

        Assert.False(result.Success);
        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.Category);
        Assert.Equal("MarketClosedOrSessionUnavailablePlausible", result.SanitizedReasonCategory);
        Assert.DoesNotContain("market closed for session", json, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.RawFixSerialized);
        Assert.False(result.CredentialValuesReturned);
    }

    [Fact]
    public void Marketdata_operation_surfaces_sanitized_sessionreject_reason_without_raw_reject_text()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(FixEncoding.GetBytes("35=3\u000158=permission denied\u0001"));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            CancellationToken.None);
        var json = JsonSerializer.Serialize(result);

        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.SanitizedErrorCategory);
        Assert.Equal("PermissionSessionAccountRejectPlausible", result.SanitizedErrorMessage);
        Assert.All(result.InstrumentStatuses, x =>
        {
            Assert.Equal("SessionRejectObservedWithSanitizedReason", x.SanitizedErrorCategory);
            Assert.Equal("PermissionSessionAccountRejectPlausible", x.SanitizedErrorMessage);
        });
        Assert.DoesNotContain("permission denied", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("35=3", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Marketdata_operation_propagates_msgtype_y_281_subcategory_to_runtime_contract_without_raw_fix()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(FixEncoding.GetBytes("35=Y\u0001262=r188-fixture\u0001281=6\u000158=raw lmax reject detail\u0001"));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            CancellationToken.None);
        var json = JsonSerializer.Serialize(result);

        Assert.Equal("MarketDataRejectObserved", result.SanitizedErrorCategory);
        Assert.Equal("MalformedOrUnsupportedMarketDataRequestPlausible", result.SanitizedErrorMessage);
        Assert.Equal("MarketDataRequestRejectUnsupportedMDUpdateType", result.MarketDataRejectSanitizedSubcategory);
        Assert.Equal("RejectReasonNotAvailable", result.SessionRejectSanitizedSubcategory);
        Assert.Equal("MsgTypeYTag281", result.RejectReasonExtractionSource);
        Assert.DoesNotContain("35=Y", json, StringComparison.Ordinal);
        Assert.DoesNotContain("281=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("raw lmax reject detail", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Marketdata_operation_propagates_msgtype_3_subcategory_to_runtime_contract_without_raw_fix()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(FixEncoding.GetBytes("35=3\u0001372=V\u0001373=5\u000158=raw lmax reject detail\u0001"));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            CancellationToken.None);
        var json = JsonSerializer.Serialize(result);

        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.SanitizedErrorCategory);
        Assert.Equal("MalformedOrUnsupportedMarketDataRequestPlausible", result.SanitizedErrorMessage);
        Assert.Equal("RejectReasonNotAvailable", result.MarketDataRejectSanitizedSubcategory);
        Assert.Equal("SessionRejectRefMsgTypeMarketDataRequest", result.SessionRejectSanitizedSubcategory);
        Assert.Equal("MsgType3Tags371372373", result.RejectReasonExtractionSource);
        Assert.Equal("RefTagID_NotAvailable", result.SessionRejectRefTagIdSanitizedCategory);
        Assert.Equal("SessionRejectReason_ValueIncorrect", result.SessionRejectReasonSanitizedCategory);
        Assert.Equal("RefMsgType_MarketDataRequest", result.SessionRejectRefMsgTypeSanitizedCategory);
        Assert.DoesNotContain("35=3", json, StringComparison.Ordinal);
        Assert.DoesNotContain("372=V", json, StringComparison.Ordinal);
        Assert.DoesNotContain("373=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("raw lmax reject detail", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("263", "RefTagID_SubscriptionRequestType_263")]
    [InlineData("265", "RefTagID_MDUpdateType_265")]
    [InlineData("267", "RefTagID_NoMDEntryTypes_267")]
    [InlineData("269", "RefTagID_MDEntryType_269")]
    [InlineData("48", "RefTagID_SecurityID_48")]
    public void Marketdata_operation_maps_msgtype_3_reftagid_to_sanitized_marketdatarequest_categories(
        string refTagId,
        string expectedCategory)
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(FixEncoding.GetBytes($"35=3\u0001371={refTagId}\u0001372=V\u0001373=1\u000158=raw lmax reject detail\u0001"));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            CancellationToken.None);
        var json = JsonSerializer.Serialize(result);

        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.SanitizedErrorCategory);
        Assert.Equal("MalformedOrUnsupportedMarketDataRequestPlausible", result.SanitizedErrorMessage);
        Assert.Equal(expectedCategory, result.SessionRejectRefTagIdSanitizedCategory);
        Assert.Equal("SessionRejectReason_RequiredTagMissing", result.SessionRejectReasonSanitizedCategory);
        Assert.Equal("RefMsgType_MarketDataRequest", result.SessionRejectRefMsgTypeSanitizedCategory);
        Assert.DoesNotContain("371=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("372=V", json, StringComparison.Ordinal);
        Assert.DoesNotContain("373=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("raw lmax reject detail", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("1", "SessionRejectReason_RequiredTagMissing")]
    [InlineData("5", "SessionRejectReason_ValueIncorrect")]
    public void Marketdata_operation_maps_msgtype_3_sessionrejectreason_to_sanitized_categories(
        string sessionRejectReason,
        string expectedCategory)
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(FixEncoding.GetBytes($"35=3\u0001372=V\u0001373={sessionRejectReason}\u000158=raw lmax reject detail\u0001"));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            CancellationToken.None);

        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.SanitizedErrorCategory);
        Assert.Equal(expectedCategory, result.SessionRejectReasonSanitizedCategory);
        Assert.Equal("RefTagID_NotAvailable", result.SessionRejectRefTagIdSanitizedCategory);
        Assert.Equal("RefMsgType_MarketDataRequest", result.SessionRejectRefMsgTypeSanitizedCategory);
    }

    [Fact]
    public void Marketdata_operation_represents_absent_detailed_reject_tags_as_not_available()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(FixEncoding.GetBytes("35=3\u000158=missing required tag\u0001"));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            CancellationToken.None);

        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.SanitizedErrorCategory);
        Assert.Equal("RejectReasonNotAvailable", result.MarketDataRejectSanitizedSubcategory);
        Assert.Equal("RejectReasonNotAvailable", result.SessionRejectSanitizedSubcategory);
        Assert.Equal("MsgType3Tags371372373Absent", result.RejectReasonExtractionSource);
        Assert.Equal("RefTagID_NotAvailable", result.SessionRejectRefTagIdSanitizedCategory);
        Assert.Equal("SessionRejectReason_NotAvailable", result.SessionRejectReasonSanitizedCategory);
        Assert.Equal("RefMsgType_NotAvailable", result.SessionRejectRefMsgTypeSanitizedCategory);
    }

    [Fact]
    public void Marketdata_operation_represents_missing_371_and_373_as_not_available_without_propagation_failure()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new DuplexMarketDataStream(FixEncoding.GetBytes("35=3\u0001372=V\u000158=raw lmax reject detail\u0001"));

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            CancellationToken.None);

        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.SanitizedErrorCategory);
        Assert.Equal("SessionRejectRefMsgTypeMarketDataRequest", result.SessionRejectSanitizedSubcategory);
        Assert.Equal("MsgType3Tags371372373", result.RejectReasonExtractionSource);
        Assert.Equal("RefMsgType_MarketDataRequest", result.SessionRejectRefMsgTypeSanitizedCategory);
        Assert.Equal("RefTagID_NotAvailable", result.SessionRejectRefTagIdSanitizedCategory);
        Assert.Equal("SessionRejectReason_NotAvailable", result.SessionRejectReasonSanitizedCategory);
    }

    [Fact]
    public void Cli_summary_contract_emits_enhanced_reject_subcategory_fields()
    {
        var root = FindRepoRoot();
        var program = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation",
            "Program.cs"));

        Assert.Contains("marketDataRejectSanitizedSubcategory=", program, StringComparison.Ordinal);
        Assert.Contains("sessionRejectSanitizedSubcategory=", program, StringComparison.Ordinal);
        Assert.Contains("rejectReasonExtractionSource=", program, StringComparison.Ordinal);
        Assert.Contains("sessionRejectRefTagIdSanitizedCategory=", program, StringComparison.Ordinal);
        Assert.Contains("sessionRejectReasonSanitizedCategory=", program, StringComparison.Ordinal);
        Assert.Contains("sessionRejectRefMsgTypeSanitizedCategory=", program, StringComparison.Ordinal);
        Assert.Contains("marketDataEntriesObserved=", program, StringComparison.Ordinal);
        Assert.Contains("marketDataSanitizedEntryCount=", program, StringComparison.Ordinal);
        Assert.Contains("marketDataEntriesEvidenceCategory=", program, StringComparison.Ordinal);
        Assert.Contains("marketDataEntriesReportingSource=", program, StringComparison.Ordinal);
        Assert.Contains("marketDataEntriesNotAvailableReason=", program, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeCliSummaryDidNotEmitR184Subcategory", program, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SessionRejectReasonNotAvailable", "SessionRejectObservedWithoutReason")]
    [InlineData("MarketClosedOrSessionUnavailablePlausible", "SessionRejectObservedWithSanitizedReason")]
    [InlineData("PermissionSessionAccountRejectPlausible", "SessionRejectObservedWithSanitizedReason")]
    [InlineData("InstrumentSecurityMappingRejectPlausible", "SessionRejectObservedWithSanitizedReason")]
    [InlineData("MalformedOrUnsupportedMarketDataRequestPlausible", "SessionRejectObservedWithSanitizedReason")]
    [InlineData("SessionRejectReasonOtherSanitized", "SessionRejectObservedWithSanitizedReason")]
    public void Sanitized_fixture_reclassification_covers_allowed_categories_without_raw_fix_or_reject_text(
        string sanitizedReasonCategory,
        string expectedRejectCategory)
    {
        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier
            .ReclassifySanitizedSessionRejectReasonCategory(sanitizedReasonCategory);
        var json = JsonSerializer.Serialize(result);

        Assert.Equal(expectedRejectCategory, result.Category);
        Assert.Equal(sanitizedReasonCategory, result.SanitizedReasonCategory);
        Assert.False(result.RawFixSerialized);
        Assert.False(result.CredentialValuesReturned);
        Assert.DoesNotContain("35=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("58=", json, StringComparison.Ordinal);
        Assert.DoesNotContain("permission denied", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R131_equivalent_sanitized_fixture_preserves_not_rehydratable_distinction_without_inferring_root_cause()
    {
        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier
            .ReclassifySanitizedSessionRejectReasonCategory(null);
        var json = JsonSerializer.Serialize(result);

        Assert.Equal("SessionRejectObservedWithoutReason", result.Category);
        Assert.Equal("SessionRejectReasonNotAvailable", result.SanitizedReasonCategory);
        Assert.True(result.InconclusiveSafe);
        Assert.False(result.RawFixSerialized);
        Assert.False(result.CredentialValuesReturned);
        Assert.DoesNotContain("MarketDataRequestShape", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PermissionSessionAccountRejectPlausible", json, StringComparison.Ordinal);
        Assert.DoesNotContain("InstrumentSecurityMappingRejectPlausible", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("permission denied", "PermissionSessionAccountRejectPlausible")]
    [InlineData("unknown security", "InstrumentSecurityMappingRejectPlausible")]
    [InlineData("missing required tag", "MalformedOrUnsupportedMarketDataRequestPlausible")]
    public void Sessionreject_reason_mapping_distinguishes_sanitized_cause_classes(
        string rawText,
        string expectedReasonCategory)
    {
        var result = LmaxReadOnlyActivationManualMarketDataResponseParserClassifier.Classify(
            FixEncoding.GetBytes($"35=3\u000158={rawText}\u0001"));
        var json = JsonSerializer.Serialize(result);

        Assert.Equal("SessionRejectObservedWithSanitizedReason", result.Category);
        Assert.Equal(expectedReasonCategory, result.SanitizedReasonCategory);
        Assert.DoesNotContain(rawText, json, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.RawFixSerialized);
    }

    [Fact]
    public void Non_approved_instruments_are_rejected_before_marketdata_write()
    {
        var operation = new LmaxReadOnlyActivationManualMarketDataRequestOperation(
            new LmaxReadOnlyActivationManualMarketDataRequestBuilder(SyntheticCredentialReader),
            new LmaxReadOnlyActivationManualMarketDataRequestWriter());
        using var stream = new MemoryStream();
        var scope = ApprovedRetryScope("LMAX-R119") with
        {
            Instruments =
            [
                new LmaxReadOnlyRuntimeApprovedInstrument(
                    "XAUUSD",
                    "9999",
                    "8",
                    "not_approved",
                    false,
                    null)
            ]
        };

        var result = operation.RequestReadOnlyMarketData(
            stream,
            fixSessionAcknowledged: true,
            ApprovedMarketDataOptions(),
            scope,
            CancellationToken.None);

        Assert.Equal("ApprovedInstrumentScopeMismatch", result.SanitizedErrorCategory);
        Assert.Equal(0, stream.Length);
        Assert.All(result.InstrumentStatuses, x => Assert.Equal(LmaxTemporaryReadOnlySessionBoundaryStatus.NotAttempted, x.MarketDataBoundary));
    }

    [Fact]
    public void Manual_real_bounded_factory_binds_marketdata_operation_without_making_it_default_global()
    {
        var root = FindRepoRoot();
        var factory = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "QQ.Production.Intraday.Tools.LmaxReadOnlyActivation",
            "LmaxReadOnlyActivationManualExecutionSurfaceFactory.cs"));
        var apiProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Api", "Program.cs"));
        var workerProgram = File.ReadAllText(Path.Combine(root, "src", "QQ.Production.Intraday.Worker", "Program.cs"));

        Assert.Contains("socketConnector.RequestMarketData", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("new LmaxRealReadOnlyMarketDataFrameClient();", factory, StringComparison.Ordinal);
        Assert.Contains("ExternalMarketDataRequestExecutionApproved: true", factory, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxReadOnlyActivationManualMarketDataRequestOperation", apiProgram, StringComparison.Ordinal);
        Assert.DoesNotContain("LmaxReadOnlyActivationManualMarketDataRequestOperation", workerProgram, StringComparison.Ordinal);
        Assert.Contains("FakeLmaxGateway", apiProgram, StringComparison.Ordinal);
    }

    [Fact]
    public void No_socket_tls_fix_or_marketdata_boundary_is_attempted_during_r121_validation()
    {
        var validation = LmaxReadOnlyActivationManualMarketDataRequestOperation.ValidateBinding(
            ApprovedMarketDataOptions(),
            ApprovedRetryScope("LMAX-R119"),
            fixSessionAcknowledged: true,
            SyntheticCredentialReader);

        Assert.False(validation.ExternalBoundaryAttemptedDuringValidation);
        Assert.False(validation.ApiWorkerReachable);
        Assert.True(validation.NoExternalDefaultPreserved);
        Assert.False(validation.OrderFramesSupported);
    }

    private static LmaxReadOnlyMarketDataRequestOptions ApprovedMarketDataOptions()
        => new(
            "Demo/read-only",
            DemoReadOnly: true,
            "ReadOnlyMarketDataRequest",
            "SnapshotOrStatus",
            TimeSpan.FromSeconds(15),
            LmaxReadOnlyMarketDataRequestOptions.DefaultAllowedReadOnlyMessageTypes,
            ExternalMarketDataRequestExecutionApproved: true);

    private static LmaxReadOnlyMarketDataRequestOptions RepairedMarketDataOptions()
        => ApprovedMarketDataOptions() with
        {
            SnapshotModeLabel = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.RepairedSnapshotPlusUpdatesSecurityIdOnlyNonBatched
        };

    private static LmaxReadOnlyMarketDataRequestOptions UltraMinimalGbpusdMarketDataOptions()
        => ApprovedMarketDataOptions() with
        {
            SnapshotModeLabel = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesSecurityIdOnlyGbpusdSingleInstrument
        };

    private static LmaxReadOnlyMarketDataRequestOptions SymbolOnlyGbpusdMarketDataOptions()
        => ApprovedMarketDataOptions() with
        {
            SnapshotModeLabel = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesSymbolOnlyGbpusdSingleInstrument
        };

    private static LmaxReadOnlyMarketDataRequestOptions SymbolAndSecurityIdGbpusdMarketDataOptions()
        => ApprovedMarketDataOptions() with
        {
            SnapshotModeLabel = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesSymbolAndSecurityIdGbpusdSingleInstrument
        };

    private static LmaxReadOnlyMarketDataRequestOptions FreshLifecycleSymbolAndSecurityIdGbpusdMarketDataOptions()
        => ApprovedMarketDataOptions() with
        {
            SnapshotModeLabel = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesFreshLifecycleSymbolAndSecurityIdGbpusdSingleInstrument
        };

    private static LmaxReadOnlyMarketDataRequestOptions MdUpdateTypeRequiredSecurityIdOnlyGbpusdMarketDataOptions()
        => ApprovedMarketDataOptions() with
        {
            SnapshotModeLabel = LmaxReadOnlyActivationManualMarketDataRequestShapeProfile.UltraMinimalSnapshotPlusUpdatesWithMDUpdateTypeSecurityIdOnlyGbpusdSingleInstrument
        };

    private static LmaxTemporaryReadOnlyRuntimeActivationScope ApprovedRetryScope(string phase)
        => new(
            phase,
            "Demo",
            DemoReadOnly: true,
            Temporary: true,
            InertValidatorOnly: true,
            LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments,
            new LmaxReadOnlyRuntimeSafetyFlags(),
            new LmaxReadOnlyRuntimeOperatorApproval(
                "Philippe",
                new DateTimeOffset(2026, 05, 13, 13, 40, 00, TimeSpan.Zero),
                ApprovalForPhase(phase),
                phase,
                "Demo/read-only",
                LmaxReadOnlyRuntimeApprovedInstrumentAllowlist.Instruments.Select(x => x.Symbol).ToList()),
            new LmaxReadOnlyRuntimeShutdownRevertRecord(
                PlanPresent: true,
                ShutdownRequiredAfterAttempt: true,
                RevertRequiredAfterAttempt: true,
                "artifacts/readiness/lmax-runtime-enablement/r121-test-shutdown-revert.json"),
            MaxRuntimeSeconds: 30,
            "artifacts/readiness/lmax-runtime-enablement");

    private static string ApprovalForPhase(string phase)
        => $"I, Philippe, explicitly approve Phase {phase} for one temporary Demo read-only runtime market-data activation retry with no orders, no trading enablement, no scheduler, no polling, no replay, no shadow replay submit, no trading-state mutation, no production account, sanitized output only, and immediate abort authority.";

    private static string? SyntheticCredentialReader(string key)
        => key switch
        {
            "LMAX_DEMO_SENDER_COMP_ID" => "r121-synthetic-sender",
            "LMAX_DEMO_TARGET_COMP_ID" => "r121-synthetic-target",
            _ => null
        };

    private static byte[] MarketDataSnapshotFrame(int entryCount)
        => MarketDataResponseFrame("W", entryCount);

    private static byte[] MarketDataResponseFrame(string messageType, int entryCount)
    {
        var body = $"35={messageType}\u0001268={entryCount}\u0001";
        if (entryCount > 0)
        {
            body += "269=0\u0001269=1\u0001";
        }

        return FixEncoding.GetBytes(body);
    }

    private static IReadOnlyDictionary<string, string> ParseFields(byte[] frameBytes)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in FixEncoding.GetString(frameBytes).Split(['\u0001', '|'], StringSplitOptions.RemoveEmptyEntries))
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

    private static int CountTag(string message, string tag)
        => message.Split('\u0001', StringSplitOptions.RemoveEmptyEntries)
            .Count(x => x.StartsWith(tag + "=", StringComparison.Ordinal));

    private static int CountTagValue(string message, string tag, string value)
        => message.Split('\u0001', StringSplitOptions.RemoveEmptyEntries)
            .Count(x => string.Equals(x, tag + "=" + value, StringComparison.Ordinal));

    private static void AssertRepairedMdReqIdShape(string mdReqId)
    {
        Assert.True(mdReqId.Length <= 16, "Repaired MDReqID must be 16 characters or shorter.");
        Assert.All(mdReqId, c => Assert.True(IsAsciiAlphaNumeric(c), "Repaired MDReqID must be ASCII alphanumeric only."));
        Assert.DoesNotContain("_", mdReqId, StringComparison.Ordinal);
        Assert.DoesNotContain("-", mdReqId, StringComparison.Ordinal);
        Assert.DoesNotContain(".", mdReqId, StringComparison.Ordinal);
        Assert.DoesNotContain("LMAX", mdReqId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("READONLY", mdReqId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("R158", mdReqId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("R195", mdReqId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("R197", mdReqId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAsciiAlphaNumeric(char value)
        => value is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "QQ.Production.Intraday.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class DuplexMarketDataStream : Stream
    {
        private readonly MemoryStream readStream;
        private readonly MemoryStream writeStream = new();

        public DuplexMarketDataStream(byte[] responseBytes)
        {
            readStream = new MemoryStream(responseBytes);
        }

        public byte[] WrittenBytes => writeStream.ToArray();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => writeStream.Length;

        public override long Position
        {
            get => writeStream.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            writeStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => readStream.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => readStream.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            writeStream.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => writeStream.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                readStream.Dispose();
                writeStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class ChunkedDuplexMarketDataStream : Stream
    {
        private readonly Queue<byte[]> chunks;
        private readonly MemoryStream writeStream = new();

        public ChunkedDuplexMarketDataStream(params byte[][] responseChunks)
        {
            chunks = new Queue<byte[]>(responseChunks);
        }

        public byte[] WrittenBytes => writeStream.ToArray();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => writeStream.Length;

        public override long Position
        {
            get => writeStream.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            writeStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (chunks.Count == 0)
            {
                return 0;
            }

            var chunk = chunks.Dequeue();
            var length = Math.Min(count, chunk.Length);
            Array.Copy(chunk, 0, buffer, offset, length);
            return length;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (chunks.Count == 0)
            {
                return ValueTask.FromResult(0);
            }

            var chunk = chunks.Dequeue();
            var length = Math.Min(buffer.Length, chunk.Length);
            chunk.AsSpan(0, length).CopyTo(buffer.Span);
            return ValueTask.FromResult(length);
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            writeStream.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => writeStream.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                writeStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
