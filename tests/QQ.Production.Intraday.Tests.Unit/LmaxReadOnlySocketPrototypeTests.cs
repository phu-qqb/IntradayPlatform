using System.Reflection;
using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxReadOnlySocketPrototypeTests
{
    [Fact]
    public async Task Default_options_block_before_any_external_attempt()
    {
        var transport = new LmaxReadOnlySocketPrototypeTransport();

        var result = await transport.RunDemoSnapshotAsync(new LmaxReadOnlySocketPrototypeOptions());

        Assert.Equal(LmaxReadOnlySocketPrototypeStatus.BlockedSafetyGate, result.Status);
        Assert.False(result.ExternalConnectionAttempted);
        Assert.False(result.CredentialReadAttempted);
        Assert.False(result.CredentialValuesReturned);
        Assert.False(result.LogonAttempted);
        Assert.False(result.LogonSucceeded);
        Assert.False(result.SnapshotRequestAttempted);
        Assert.False(result.SnapshotReceived);
        Assert.False(result.LogoutAttempted);
        Assert.False(result.LogoutSucceeded);
        Assert.False(result.OrderSubmissionAttempted);
        Assert.False(result.ShadowReplaySubmitAttempted);
        Assert.False(result.TradingMutationAttempted);
        Assert.False(result.SchedulerStarted);
        Assert.False(result.MarketDataSnapshotReceived);
        Assert.Equal(0, result.EventCount);
        Assert.True(result.NoSensitiveContent);
        Assert.Contains("Enabled", result.Safety.FailedGateNames);
        Assert.Contains("ResolveCredentialAvailabilityOnly", result.Safety.FailedGateNames);
        Assert.Contains("CredentialAvailabilityConfigured", result.Safety.FailedGateNames);
    }

    [Theory]
    [InlineData("MissingReason", "ReasonRequired")]
    [InlineData("MissingAllowExternalConnections", "AllowExternalConnections")]
    [InlineData("MissingConfirmDemoReadOnly", "ConfirmDemoReadOnly")]
    [InlineData("NonDemoEnvironment", "EnvironmentName")]
    [InlineData("ProductionVenue", "VenueProfileName")]
    [InlineData("AllowOrderSubmission", "OrderSubmissionForbidden")]
    [InlineData("PersistToTradingTables", "PersistToTradingTables")]
    [InlineData("SchedulerEnabled", "SchedulerEnabled")]
    [InlineData("SubmitToShadowReplay", "SubmitToShadowReplay")]
    [InlineData("TooLargeRuntime", "MaxRuntimeSeconds")]
    [InlineData("TooLargeEvents", "MaxEventsPerRun")]
    public void Prototype_safety_gate_blocks_unsafe_options(string condition, string expectedGate)
    {
        var options = ValidLookingOptions() with
        {
            Reason = condition == "MissingReason" ? "" : "manual demo snapshot",
            AllowExternalConnections = condition != "MissingAllowExternalConnections",
            ConfirmDemoReadOnly = condition != "MissingConfirmDemoReadOnly",
            EnvironmentName = condition == "NonDemoEnvironment" ? "UAT" : "Demo",
            VenueProfileName = condition == "ProductionVenue" ? "Production" : LmaxReadOnlyVenueProfileName.DemoLondon.Value,
            AllowOrderSubmission = condition == "AllowOrderSubmission",
            PersistToTradingTables = condition == "PersistToTradingTables",
            SchedulerEnabled = condition == "SchedulerEnabled",
            SubmitToShadowReplay = condition == "SubmitToShadowReplay",
            MaxRuntimeSeconds = condition == "TooLargeRuntime" ? LmaxReadOnlySocketPrototypeOptions.SafeMaxRuntimeSeconds + 1 : 15,
            MaxEventsPerRun = condition == "TooLargeEvents" ? LmaxReadOnlySocketPrototypeOptions.SafeMaxEventsPerRun + 1 : 5
        };

        var safety = LmaxReadOnlySocketPrototypeTransport.EvaluateSafety(options);

        Assert.False(safety.Passed);
        Assert.Contains(expectedGate, safety.FailedGateNames);
    }

    [Fact]
    public async Task Missing_credentials_block_with_retry_guidance_before_connection()
    {
        var transport = new LmaxReadOnlySocketPrototypeTransport(
            new LmaxReadOnlyCredentialProfileResolverEnvironment(_ => null),
            _ => null,
            new FakeDemoMarketDataSocketClient());

        var result = await transport.RunDemoSnapshotAsync(ValidLookingOptions() with
        {
            ResolveCredentialAvailabilityOnly = true
        });

        Assert.Equal(LmaxReadOnlySocketPrototypeStatus.BlockedMissingCredentials, result.Status);
        Assert.False(result.ExternalConnectionAttempted);
        Assert.False(result.LogonAttempted);
        Assert.False(result.SnapshotRequestAttempted);
        Assert.Equal(LmaxReadOnlySocketPrototypeRetryRecommendation.FixCredentialsThenRetry, result.RetryPolicy.Recommendation);
        Assert.False(result.RetryPolicy.RetryEnabled);
        Assert.False(result.RetryPolicy.RetryAllowed);
        Assert.Equal(1, result.RetryPolicy.MaxAttempts);
    }

    [Theory]
    [InlineData("UAT", "DemoLondon", LmaxReadOnlySocketPrototypeStatus.BlockedInvalidEnvironment)]
    [InlineData("Demo", "Production", LmaxReadOnlySocketPrototypeStatus.BlockedUnsafeVenue)]
    public async Task Environment_and_venue_blocks_classify_before_connection(string environmentName, string venueProfileName, LmaxReadOnlySocketPrototypeStatus expected)
    {
        var values = PresentCredentialValues();
        var transport = TransportWithValues(values, new FakeDemoMarketDataSocketClient());

        var result = await transport.RunDemoSnapshotAsync(ValidLookingOptions() with
        {
            ResolveCredentialAvailabilityOnly = true,
            EnvironmentName = environmentName,
            VenueProfileName = venueProfileName
        });

        Assert.Equal(expected, result.Status);
        Assert.False(result.ExternalConnectionAttempted);
    }

    [Fact]
    public async Task Order_submission_flag_classifies_before_connection()
    {
        var values = PresentCredentialValues();
        var transport = TransportWithValues(values, new FakeDemoMarketDataSocketClient());

        var result = await transport.RunDemoSnapshotAsync(ValidLookingOptions() with
        {
            ResolveCredentialAvailabilityOnly = true,
            AllowOrderSubmission = true
        });

        Assert.Equal(LmaxReadOnlySocketPrototypeStatus.BlockedOrderSubmissionFlag, result.Status);
        Assert.False(result.ExternalConnectionAttempted);
        Assert.False(result.OrderSubmissionAttempted);
    }

    [Fact]
    public async Task Known_rejected_snapshot_only_profile_blocks_locally_before_connection()
    {
        var values = PresentCredentialValues();
        var transport = TransportWithValues(values, new FakeDemoMarketDataSocketClient());

        var result = await transport.RunDemoSnapshotAsync(ValidLookingOptions() with
        {
            ResolveCredentialAvailabilityOnly = true,
            RequestMode = LmaxReadOnlyMarketDataRequestMode.SnapshotOnly,
            SymbolEncodingMode = LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdOnly,
            SkipKnownRejectedProfiles = true,
            AllowKnownRejectedDiagnostics = false
        });

        Assert.Equal(LmaxReadOnlySocketPrototypeStatus.FailedSafeKnownRejectedRequestProfile, result.Status);
        Assert.False(result.ExternalConnectionAttempted);
        Assert.True(result.Diagnostics.Request.KnownRejectedByLmaxDemo);
        Assert.Contains("263=0", result.Diagnostics.Request.SanitizedFieldSummary);
    }

    [Fact]
    public void Compatibility_default_uses_snapshot_plus_updates_security_id_only()
    {
        var profile = LmaxReadOnlyMarketDataRequestCompatibility.CreateProfile(ValidLookingOptions());

        Assert.Equal(LmaxReadOnlyMarketDataRequestMode.SnapshotPlusUpdates, profile.RequestMode);
        Assert.Equal(LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdOnly, profile.SymbolEncodingMode);
        Assert.False(profile.KnownRejectedByLmaxDemo);
        Assert.True(profile.SafeToAttempt);
        Assert.True(profile.RequiresUnsubscribeAfterSnapshot);
        Assert.Equal("1", profile.ExpectedSubscriptionRequestType);
        Assert.Contains("48 present", profile.SanitizedFieldSummary);
        Assert.Contains("22=8", profile.SanitizedFieldSummary);
        Assert.Contains("55 omitted", profile.SanitizedFieldSummary);
    }

    [Fact]
    public void Valid_looking_options_still_block_because_credential_availability_not_checked()
    {
        var safety = LmaxReadOnlySocketPrototypeTransport.EvaluateSafety(ValidLookingOptions());

        Assert.False(safety.Passed);
        Assert.Contains("ResolveCredentialAvailabilityOnly", safety.FailedGateNames);
        Assert.Contains("CredentialAvailabilityConfigured", safety.FailedGateNames);
    }

    [Fact]
    public async Task Prototype_can_complete_through_injected_fake_socket_when_credential_gate_passes()
    {
        var values = PresentCredentialValues();
        var transport = TransportWithValues(values, new FakeDemoMarketDataSocketClient());

        var result = await transport.RunDemoSnapshotAsync(ValidLookingOptions() with
        {
            ResolveCredentialAvailabilityOnly = true
        });
        var json = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.Equal(LmaxReadOnlySocketPrototypeStatus.Completed, result.Status);
        Assert.True(result.CredentialReadAttempted);
        Assert.True(result.ExternalConnectionAttempted);
        Assert.True(result.LogonAttempted);
        Assert.True(result.LogonSucceeded);
        Assert.True(result.SnapshotRequestAttempted);
        Assert.True(result.SnapshotReceived);
        Assert.True(result.LogoutAttempted);
        Assert.True(result.LogoutSucceeded);
        Assert.False(result.OrderSubmissionAttempted);
        Assert.False(result.ShadowReplaySubmitAttempted);
        Assert.False(result.TradingMutationAttempted);
        Assert.NotNull(result.CredentialAvailability);
        Assert.True(result.CredentialAvailability.IsConfigured);
        Assert.False(result.CredentialAvailability.CredentialValuesReturned);
        Assert.False(result.CredentialAvailability.SensitiveMaterialReturned);
        Assert.True(result.Safety.Passed);
        Assert.Equal(LmaxReadOnlySocketPrototypeRetryRecommendation.NoRetry, result.RetryPolicy.Recommendation);
        Assert.Equal(1, result.MessageCount);
        Assert.Equal(2, result.EntryCount);
        Assert.True(result.MarketDataSnapshotReceived);
        Assert.Equal(1.1m, result.BestBid);
        Assert.Equal(1.2m, result.BestAsk);
        Assert.Equal(1.15m, result.Mid);
        Assert.NotNull(result.SnapshotReceivedAtUtc);
        Assert.Equal("Redacted", result.RedactionStatus);
        Assert.Equal("phase5g-snapshot-diagnostics-v1", result.Diagnostics.DiagnosticVersion);
        Assert.Equal("phase5j-logon-diagnostics-v1", result.LogonDiagnostics.DiagnosticVersion);
        Assert.True(result.LogonDiagnostics.TcpConnected);
        Assert.True(result.LogonDiagnostics.TlsConnected);
        Assert.True(result.LogonDiagnostics.UsernamePresent);
        Assert.True(result.LogonDiagnostics.PasswordPresent);
        Assert.True(result.LogonDiagnostics.SenderCompIdPresent);
        Assert.True(result.LogonDiagnostics.TargetCompIdPresent);
        Assert.True(result.LogonDiagnostics.UsernameLength > 0);
        Assert.True(result.LogonDiagnostics.PasswordLength > 0);
        Assert.Equal("FIX.4.4", result.LogonDiagnostics.BeginString);
        Assert.Equal(0, result.LogonDiagnostics.EncryptMethod);
        Assert.Equal(30, result.LogonDiagnostics.HeartbeatInterval);
        Assert.True(result.LogonDiagnostics.ProfileComparison.SameBeginString);
        Assert.False(result.LogonDiagnostics.ProfileComparison.SenderCompIdMismatchSuspected);
        Assert.False(result.LogonDiagnostics.ProfileComparison.TargetCompIdMismatchSuspected);
        Assert.Equal(LmaxReadOnlyMarketDataRequestMode.SnapshotPlusUpdates, result.Diagnostics.Request.RequestMode);
        Assert.Equal(LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdOnly, result.Diagnostics.Request.SymbolEncodingMode);
        Assert.Equal("4001", result.Diagnostics.Request.SecurityId);
        Assert.Equal(1, result.Diagnostics.Request.MarketDepth);
        Assert.Equal("SnapshotPlusUpdates", result.Diagnostics.Request.SubscriptionRequestType);
        Assert.True(result.Diagnostics.Request.RequiresUnsubscribeAfterSnapshot);
        Assert.False(result.Diagnostics.Request.KnownRejectedByLmaxDemo);
        Assert.Contains("263=1", result.Diagnostics.Request.SanitizedFieldSummary);
        Assert.Contains("55 omitted", result.Diagnostics.Request.SanitizedFieldSummary);
        Assert.Equal(1, result.Diagnostics.MessageCounters.Logon);
        Assert.Equal(1, result.Diagnostics.MessageCounters.MarketDataRequest);
        Assert.Equal(1, result.Diagnostics.MessageCounters.MarketDataSnapshot);
        foreach (var sentinel in values.Values)
        {
            Assert.DoesNotContain(sentinel, json, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(FakeSocketScenario.ConnectionFailure, LmaxReadOnlySocketPrototypeStatus.FailedSafeConnectionError)]
    [InlineData(FakeSocketScenario.LogonTimeout, LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonTimeout)]
    [InlineData(FakeSocketScenario.LogonRejected, LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonRejected)]
    [InlineData(FakeSocketScenario.LogonLogoutReceived, LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonLogoutReceived)]
    [InlineData(FakeSocketScenario.LogonSessionRejectReceived, LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonRejectReceived)]
    [InlineData(FakeSocketScenario.SnapshotTimeout, LmaxReadOnlySocketPrototypeStatus.FailedSafeSnapshotTimeout)]
    [InlineData(FakeSocketScenario.MarketDataReject, LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejected)]
    [InlineData(FakeSocketScenario.ValueOutOfRange263, LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedValueOutOfRange263)]
    [InlineData(FakeSocketScenario.UnknownTag55, LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedUnknownTag55)]
    [InlineData(FakeSocketScenario.GroupMismatch146, LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedGroupMismatch146)]
    [InlineData(FakeSocketScenario.SymbolEncodingRejected, LmaxReadOnlySocketPrototypeStatus.FailedSafeSymbolEncodingRejected)]
    [InlineData(FakeSocketScenario.BusinessReject, LmaxReadOnlySocketPrototypeStatus.FailedSafeBusinessReject)]
    [InlineData(FakeSocketScenario.SessionReject, LmaxReadOnlySocketPrototypeStatus.FailedSafeSessionReject)]
    [InlineData(FakeSocketScenario.UnexpectedLogout, LmaxReadOnlySocketPrototypeStatus.FailedSafeUnexpectedLogout)]
    [InlineData(FakeSocketScenario.EmptyBook, LmaxReadOnlySocketPrototypeStatus.CompletedWithEmptyBook)]
    [InlineData(FakeSocketScenario.LogoutFailure, LmaxReadOnlySocketPrototypeStatus.CompletedWithWarnings)]
    [InlineData(FakeSocketScenario.MaxEventsExceeded, LmaxReadOnlySocketPrototypeStatus.FailedSafeMaxEventsExceeded)]
    public async Task Failure_scenarios_are_classified_without_secret_leak(FakeSocketScenario scenario, LmaxReadOnlySocketPrototypeStatus expectedStatus)
    {
        var values = PresentCredentialValues();
        var transport = TransportWithValues(values, new FakeDemoMarketDataSocketClient(scenario));

        var result = await transport.RunDemoSnapshotAsync(ValidLookingOptions() with
        {
            ResolveCredentialAvailabilityOnly = true
        });
        var json = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.Equal(expectedStatus, result.Status);
        Assert.True(result.CredentialReadAttempted);
        Assert.False(result.OrderSubmissionAttempted);
        Assert.False(result.ShadowReplaySubmitAttempted);
        Assert.False(result.TradingMutationAttempted);
        Assert.False(result.RetryPolicy.RetryEnabled);
        Assert.False(result.RetryPolicy.RetryAllowed);
        Assert.Equal(expectedStatus, result.Diagnostics.ResponseClassification);
        Assert.Equal("phase5g-snapshot-diagnostics-v1", result.Diagnostics.DiagnosticVersion);
        Assert.Equal("phase5j-logon-diagnostics-v1", result.LogonDiagnostics.DiagnosticVersion);
        Assert.Equal(LmaxReadOnlyMarketDataRequestMode.SnapshotPlusUpdates, result.Diagnostics.Request.RequestMode);
        Assert.Equal(LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdOnly, result.Diagnostics.Request.SymbolEncodingMode);
        foreach (var sentinel in values.Values)
        {
            Assert.DoesNotContain(sentinel, json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Logon_logout_before_confirmation_has_sanitized_diagnostics()
    {
        var values = PresentCredentialValues();
        var transport = TransportWithValues(values, new FakeDemoMarketDataSocketClient(FakeSocketScenario.LogonLogoutReceived));

        var result = await transport.RunDemoSnapshotAsync(ValidLookingOptions() with
        {
            ResolveCredentialAvailabilityOnly = true
        });
        var json = System.Text.Json.JsonSerializer.Serialize(result);

        Assert.Equal(LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonLogoutReceived, result.Status);
        Assert.Equal("5", result.LogonDiagnostics.FirstInboundMsgType);
        Assert.Contains("Logout", result.LogonDiagnostics.FirstInboundLogoutText ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.LogonDiagnostics.FirstInboundRejectText);
        Assert.True(result.LogonDiagnostics.TcpConnected);
        Assert.True(result.LogonDiagnostics.TlsConnected);
        Assert.False(result.SnapshotRequestAttempted);
        foreach (var sentinel in values.Values)
        {
            Assert.DoesNotContain(sentinel, json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Session_profile_comparison_uses_only_safe_labels_and_flags_missing_comp_ids()
    {
        var comparison = LmaxReadOnlySocketPrototypeTransport.CreateSessionProfileComparison(ValidLookingOptions(), senderCompId: "", targetCompId: null);
        var json = System.Text.Json.JsonSerializer.Serialize(comparison);

        Assert.True(comparison.SenderCompIdMismatchSuspected);
        Assert.True(comparison.TargetCompIdMismatchSuspected);
        Assert.False(comparison.SameSenderCompIdSourceLabel);
        Assert.False(comparison.SameTargetCompIdSourceLabel);
        Assert.DoesNotContain("phase5c-prototype-sentinel", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fix_redactor_removes_sensitive_fix_tags_and_sentinel_values()
    {
        var raw = "8=FIX.4.4\u00019=100\u000135=A\u000149=SENDER-SENTINEL\u000156=TARGET-SENTINEL\u0001553=USER-SENTINEL\u0001554=PASSWORD-SENTINEL\u000110=000\u0001 password=plain secret=value token=abc apiKey=def privateKey=ghi bearer bearer-value authorization: auth-value";

        var redacted = LmaxReadOnlyFixMessageRedactor.Redact(raw, ["SENDER-SENTINEL", "TARGET-SENTINEL", "USER-SENTINEL", "PASSWORD-SENTINEL"]);

        Assert.DoesNotContain("SENDER-SENTINEL", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("TARGET-SENTINEL", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("USER-SENTINEL", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("PASSWORD-SENTINEL", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("plain", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("value", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("def", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("ghi", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("bearer-value", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-value", redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sanitized_artifact_writer_never_writes_sentinel_credentials()
    {
        var values = PresentCredentialValues();
        var transport = TransportWithValues(values, new FakeDemoMarketDataSocketClient());
        var result = await transport.RunDemoSnapshotAsync(ValidLookingOptions() with
        {
            ResolveCredentialAvailabilityOnly = true
        });
        var artifactDirectory = Path.Combine(Path.GetTempPath(), "qq-phase5f-artifact-test-" + Guid.NewGuid().ToString("N"));

        try
        {
            var path = LmaxReadOnlySocketPrototypeSanitizedArtifactWriter.Write(result, artifactDirectory, values.Values);
            var json = File.ReadAllText(path);

            Assert.Contains("\"NoSensitiveContent\": true", json, StringComparison.Ordinal);
            Assert.Contains("\"RedactionStatus\": \"Redacted\"", json, StringComparison.Ordinal);
            Assert.Contains("phase5g-snapshot-diagnostics-v1", json, StringComparison.Ordinal);
            Assert.DoesNotContain("rawFix", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("554=", json, StringComparison.Ordinal);
            Assert.Contains("LMAX_DEMO_FIX_PASSWORD", json, StringComparison.Ordinal);
            foreach (var sentinel in values.Values)
            {
                Assert.DoesNotContain(sentinel, json, StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Prototype_result_never_contains_sensitive_or_order_attempt_flags()
    {
        var transport = new LmaxReadOnlySocketPrototypeTransport();

        var result = await transport.RunDemoSnapshotAsync(ValidLookingOptions());

        Assert.Equal(LmaxReadOnlySocketPrototypeStatus.BlockedSafetyGate, result.Status);
        Assert.False(result.ExternalConnectionAttempted);
        Assert.False(result.CredentialReadAttempted);
        Assert.False(result.OrderSubmissionAttempted);
        Assert.False(result.ShadowReplaySubmitAttempted);
        Assert.False(result.TradingMutationAttempted);
        Assert.True(result.NoSensitiveContent);
        Assert.DoesNotContain(result.Errors, x => x.Contains("NewOrderSingle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Prototype_public_contract_exposes_no_forbidden_field_names()
    {
        var types = new[]
        {
            typeof(LmaxReadOnlySocketPrototypeOptions),
            typeof(LmaxReadOnlySocketPrototypeSafetyReport),
            typeof(LmaxReadOnlySocketPrototypeResult)
        };
        var forbidden = new[]
        {
            "host",
            "port",
            "username",
            "password",
            "secret",
            "token",
            "apiKey",
            "privateKey",
            "accountId",
            "senderCompId",
            "targetCompId",
            "endpointUrl",
            "rawFix"
        };

        foreach (var property in types.SelectMany(x => x.GetProperties(BindingFlags.Public | BindingFlags.Instance)))
        {
            foreach (var word in forbidden)
            {
                Assert.DoesNotContain(word, property.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Prototype_contract_has_no_order_submission_method_or_type_surface()
    {
        var types = typeof(LmaxReadOnlySocketPrototypeTransport).Assembly
            .GetTypes()
            .Where(x => x.Namespace == "QQ.Production.Intraday.Infrastructure.Lmax" && x.Name.Contains("SocketPrototype", StringComparison.Ordinal))
            .ToList();

        Assert.DoesNotContain(types.Select(x => x.Name), x => x.Contains("NewOrder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(types.Select(x => x.Name), x => x.Contains("Cancel", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(types.Select(x => x.Name), x => x.Contains("Replace", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(types.SelectMany(x => x.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)).Select(x => x.Name), x => x.Contains("SubmitOrder", StringComparison.OrdinalIgnoreCase));
    }

    private static LmaxReadOnlySocketPrototypeOptions ValidLookingOptions()
        => new()
        {
            Enabled = true,
            ImplementationMode = LmaxReadOnlyRuntimeImplementationMode.FutureReadOnly,
            ActivationLevel = LmaxReadOnlyRuntimeActivationLevel.Level4RuntimeManualReadOnlyConnectionNoReplaySubmit,
            EnvironmentName = "Demo",
            VenueProfileName = LmaxReadOnlyVenueProfileName.DemoLondon.Value,
            CredentialProfileName = "LmaxDemoReadOnlyProfile",
            Reason = "manual demo snapshot",
            OperatorId = "local-operator",
            ConfirmDemoReadOnly = true,
            AllowExternalConnections = true,
            AllowCredentialUse = true,
            AllowOrderSubmission = false,
            PersistToTradingTables = false,
            SchedulerEnabled = false,
            SubmitToShadowReplay = false,
            DryRun = true,
            MaxRuntimeSeconds = 15,
            MaxEventsPerRun = 5,
            MaxWaitSeconds = 15,
            RequestMode = LmaxReadOnlyMarketDataRequestMode.SnapshotPlusUpdates,
            SymbolEncodingMode = LmaxReadOnlyMarketDataSymbolEncodingMode.SecurityIdOnly,
            SkipKnownRejectedProfiles = true,
            AllowKnownRejectedDiagnostics = false,
            MarketDepth = 1
        };

    private static Dictionary<string, string> PresentCredentialValues()
        => LmaxReadOnlyCredentialRequiredKeyLabels.DemoReadOnlyEnvironmentLabels
            .ToDictionary(x => x, x => "phase5c-prototype-sentinel-" + x);

    private static LmaxReadOnlySocketPrototypeTransport TransportWithValues(
        IReadOnlyDictionary<string, string> values,
        ILmaxReadOnlyDemoMarketDataSocketClient client)
        => new(
            new LmaxReadOnlyCredentialProfileResolverEnvironment(label => values.TryGetValue(label, out var value) ? value : null),
            label => values.TryGetValue(label, out var value) ? value : null,
            client);

    public enum FakeSocketScenario
    {
        Success,
        ConnectionFailure,
        LogonTimeout,
        LogonRejected,
        LogonLogoutReceived,
        LogonSessionRejectReceived,
        SnapshotTimeout,
        MarketDataReject,
        ValueOutOfRange263,
        UnknownTag55,
        GroupMismatch146,
        SymbolEncodingRejected,
        BusinessReject,
        SessionReject,
        UnexpectedLogout,
        EmptyBook,
        LogoutFailure,
        MaxEventsExceeded
    }

    private sealed class FakeDemoMarketDataSocketClient : ILmaxReadOnlyDemoMarketDataSocketClient
    {
        private readonly FakeSocketScenario _scenario;

        public FakeDemoMarketDataSocketClient(FakeSocketScenario scenario = FakeSocketScenario.Success)
        {
            _scenario = scenario;
        }

        public Task<LmaxReadOnlyDemoMarketDataSocketAttemptResult> RunSnapshotAsync(
            LmaxReadOnlySocketPrototypeOptions options,
            IReadOnlyDictionary<string, string> internalCredentialValues,
            CancellationToken cancellationToken = default)
        {
            Assert.All(internalCredentialValues.Values, value => Assert.StartsWith("phase5c-prototype-sentinel-", value, StringComparison.Ordinal));
            if (_scenario == FakeSocketScenario.ConnectionFailure)
            {
                throw new IOException("connection failed with phase5c-prototype-sentinel-hidden");
            }

            if (_scenario == FakeSocketScenario.LogonTimeout)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, false, false, false, false, false, 0, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonTimeout),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonTimeout, [], ["logon timeout phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.LogonRejected)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, false, false, false, false, false, 1, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonRejected),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: "5", firstInboundText: "logon rejected phase5c-prototype-sentinel-hidden"),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonRejected, [], ["logon rejected phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.LogonLogoutReceived)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, false, false, false, false, false, 1, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonLogoutReceived),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: "5", firstInboundText: "Logout before logon confirmation phase5c-prototype-sentinel-hidden"),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonLogoutReceived, [], ["logout before logon confirmation phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.LogonSessionRejectReceived)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, false, false, false, false, false, 1, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonRejectReceived),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: "3", firstInboundText: "Reject before logon confirmation phase5c-prototype-sentinel-hidden"),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeLogonRejectReceived, [], ["session reject before logon confirmation phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.SnapshotTimeout)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, false, true, true, 1, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeSnapshotTimeout),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeSnapshotTimeout, [], ["snapshot timeout phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.MarketDataReject)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, false, true, true, 2, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejected),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejected, [], ["market data reject phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.ValueOutOfRange263)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, false, true, true, 2, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedValueOutOfRange263),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedValueOutOfRange263, [], ["ValueOutOfRange tag 263 phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.UnknownTag55)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, false, true, true, 2, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedUnknownTag55),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedUnknownTag55, [], ["UnknownTag tag 55 phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.GroupMismatch146)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, false, true, true, 2, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedGroupMismatch146),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejectedGroupMismatch146, [], ["RepeatingGroupNumInGroupMismatch tag 146 phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.SymbolEncodingRejected)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, false, true, true, 2, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeSymbolEncodingRejected),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeSymbolEncodingRejected, [], ["symbol encoding reject phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.BusinessReject)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, false, true, true, 2, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeBusinessReject),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeBusinessReject, [], ["business reject phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.SessionReject)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, false, true, true, 2, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeSessionReject),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeSessionReject, [], ["session reject phase5c-prototype-sentinel-hidden"]));
            }

            if (_scenario == FakeSocketScenario.UnexpectedLogout)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, false, true, true, 2, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeUnexpectedLogout),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.FailedSafeUnexpectedLogout, ["unexpected logout phase5c-prototype-sentinel-hidden"], []));
            }

            if (_scenario == FakeSocketScenario.EmptyBook)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, true, true, true, 2, 0, null, null, null, DateTimeOffset.UtcNow, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.CompletedWithEmptyBook),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.CompletedWithEmptyBook, ["empty book phase5c-prototype-sentinel-hidden"], []));
            }

            if (_scenario == FakeSocketScenario.LogoutFailure)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, true, true, false, 1, 2, 1.1m, 1.2m, 1.15m, DateTimeOffset.UtcNow, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.CompletedWithWarnings),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    LmaxReadOnlySocketPrototypeStatus.CompletedWithWarnings, ["logout failed phase5c-prototype-sentinel-hidden"], []));
            }

            if (_scenario == FakeSocketScenario.MaxEventsExceeded)
            {
                return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                    true, true, true, true, false, false, false, options.MaxEventsPerRun, 0, null, null, null, null, Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.FailedSafeMaxEventsExceeded),
                    LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: null, firstInboundText: null),
                    null, [], []));
            }

            return Task.FromResult(new LmaxReadOnlyDemoMarketDataSocketAttemptResult(
                ExternalConnectionAttempted: true,
                LogonAttempted: true,
                LogonSucceeded: true,
                SnapshotRequestAttempted: true,
                SnapshotReceived: true,
                LogoutAttempted: true,
                LogoutSucceeded: true,
                MessageCount: 1,
                EntryCount: 2,
                BestBid: 1.1m,
                BestAsk: 1.2m,
                Mid: 1.15m,
                SnapshotReceivedAtUtc: DateTimeOffset.UtcNow,
                Diagnostics: Diagnostics(options, LmaxReadOnlySocketPrototypeStatus.Completed),
                LogonDiagnostics: LogonDiagnostics(options, internalCredentialValues, firstInboundMsgType: "A", firstInboundText: null),
                FailureStatus: null,
                Warnings: [],
                Errors: []));
        }

        private static LmaxReadOnlyMarketDataSnapshotDiagnostics Diagnostics(
            LmaxReadOnlySocketPrototypeOptions options,
            LmaxReadOnlySocketPrototypeStatus status)
            => LmaxReadOnlySocketPrototypeTransport.CreateDiagnostics(
                options,
                status,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                status == LmaxReadOnlySocketPrototypeStatus.Completed ? null : DateTimeOffset.UtcNow,
                25,
                new(1, 1, status is LmaxReadOnlySocketPrototypeStatus.Completed or LmaxReadOnlySocketPrototypeStatus.CompletedWithEmptyBook ? 1 : 0, status == LmaxReadOnlySocketPrototypeStatus.FailedSafeMarketDataRequestRejected ? 1 : 0, status == LmaxReadOnlySocketPrototypeStatus.FailedSafeBusinessReject ? 1 : 0, status == LmaxReadOnlySocketPrototypeStatus.FailedSafeSessionReject ? 1 : 0, status == LmaxReadOnlySocketPrototypeStatus.FailedSafeUnexpectedLogout ? 1 : 0, 0, 0, 0),
                [],
                []);

        private static LmaxReadOnlyFixLogonDiagnostics LogonDiagnostics(
            LmaxReadOnlySocketPrototypeOptions options,
            IReadOnlyDictionary<string, string> internalCredentialValues,
            string? firstInboundMsgType,
            string? firstInboundText)
            => LmaxReadOnlySocketPrototypeTransport.CreateLogonDiagnostics(
                options,
                internalCredentialValues,
                tcpConnected: true,
                tlsConnected: true,
                msgSeqNumSentForLogon: 1,
                firstInboundMsgType,
                firstInboundText,
                logonWaitDurationMs: 25);
    }
}


