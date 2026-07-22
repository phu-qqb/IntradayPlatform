using QQ.Production.Intraday.Infrastructure.Lmax;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7aLmaxFixOpenOrderCapabilityTests
{
    private const string Account = "ARCH7A_DEMO_ACCOUNT";
    private const string Session = "ARCH7A_DEMO_FIX_SESSION";

    [Fact]
    public void Supplied_dictionary_is_fix_44_broker_trading()
    {
        var profile = Profile();
        Assert.Equal("4.4", profile.FixVersion);
        Assert.Equal("FIX.4.4", profile.BeginString);
        Assert.Equal("Broker FIX Trading", profile.Service);
        Assert.Equal("LMXBD", profile.TargetCompId);
    }

    [Fact]
    public void Exact_capability_is_known_order_status_only()
    {
        var decision = Decision();
        Assert.Equal(LmaxFixOpenOrderCapabilityCategory.LMAX_FIX_KNOWN_ORDER_STATUS_ONLY, decision.Category);
        Assert.False(decision.BrokerAuthority);
        Assert.False(decision.SnapshotComplete);
        Assert.False(decision.EmptyStateMayBeAuthoritative);
    }

    [Fact]
    public void Order_status_request_35h_is_documented()
    {
        var profile = Profile();
        Assert.True(profile.OrderStatusRequestSupported);
        Assert.Contains("OrderStatusRequest(35=H)", profile.ClientToLmaxApplicationMessages);
    }

    [Fact]
    public void Order_mass_status_request_35af_is_absent()
    {
        var profile = Profile();
        Assert.False(profile.OrderMassStatusRequestSupported);
        Assert.DoesNotContain(profile.ClientToLmaxApplicationMessages, value => value.Contains("35=AF"));
    }

    [Fact]
    public void Mass_status_req_type_585_has_no_supported_values()
        => Assert.Empty(Profile().SupportedMassStatusReqTypes);

    [Fact]
    public void Replay_is_bounded_to_512_and_not_durable_across_reset_or_failure()
    {
        var profile = Profile();
        Assert.True(profile.ReplayAtLogonAvailable);
        Assert.Equal(512, profile.ReplayQueueLimit);
        Assert.False(profile.ReplaySurvivesGatewayFailure);
        Assert.False(profile.ReplaySurvivesSequenceReset);
    }

    [Fact]
    public void Drop_copy_and_initial_snapshot_completion_are_not_documented()
    {
        var profile = Profile();
        Assert.False(profile.DropCopyDocumented);
        Assert.False(profile.InitialSnapshotCompletionDocumented);
    }

    [Fact]
    public void Manual_and_external_order_coverage_is_unproven()
        => Assert.Equal("UNPROVEN", Decision().ExternalOrManualOrderCoverage);

    [Fact]
    public void Empty_known_order_reconstruction_is_never_authoritative()
    {
        var result = Reconstruct([]);
        Assert.Empty(result.Orders);
        Assert.False(result.SnapshotComplete);
        Assert.False(result.EmptyStateWasExplicitlyObserved);
        Assert.False(result.EmptyStateWasInferred);
        Assert.False(result.BrokerAuthority);
    }

    [Fact]
    public void Fake_completion_flag_cannot_upgrade_known_order_only_capability()
    {
        var result = Reconstruct([], explicitCompletion: true);
        Assert.False(result.SnapshotComplete);
        Assert.False(result.BrokerAuthority);
    }

    [Fact]
    public void Documented_mass_snapshot_with_completion_can_authoritatively_observe_empty()
    {
        var profile = Profile() with
        {
            OrderMassStatusRequestSupported = true,
            SupportedMassStatusReqTypes = [7],
            InitialSnapshotCompletionDocumented = true
        };
        var decision = LmaxFixOpenOrderCapabilityClassifier.Classify(profile);
        var result = new LmaxFixExecutionReportOrderStateMachine().Reconstruct(
            [], decision, Account, Session, explicitSnapshotCompletionObserved: true);
        Assert.True(result.SnapshotComplete);
        Assert.True(result.EmptyStateWasExplicitlyObserved);
        Assert.True(result.BrokerAuthority);
    }

    [Theory]
    [InlineData("0", 100, true)]
    [InlineData("1", 60, true)]
    [InlineData("6", 100, true)]
    [InlineData("A", 100, true)]
    [InlineData("E", 100, true)]
    [InlineData("2", 0, false)]
    [InlineData("4", 0, false)]
    [InlineData("8", 0, false)]
    [InlineData("C", 0, false)]
    public void Ord_status_and_leaves_determine_working_state(string status, int leaves, bool expectedWorking)
    {
        var cum = 100 - leaves;
        var state = Assert.Single(Reconstruct([Observation(1, status: status, cum: cum, leaves: leaves)]).Orders);
        Assert.Equal(expectedWorking, state.Working);
    }

    [Fact]
    public void Partial_fill_preserves_remaining_working_leaves()
    {
        var result = Reconstruct([Observation(1, status: "1", cum: 40m, leaves: 60m)]);
        Assert.Equal(60m, result.SignedReservedWorkingLeavesBySecurityId["4001"]);
    }

    [Fact]
    public void Sell_working_leaves_are_signed_negative()
    {
        var result = Reconstruct([Observation(1, side: "2", qty: 80m, leaves: 80m)]);
        Assert.Equal(-80m, result.SignedReservedWorkingLeavesBySecurityId["4001"]);
    }

    [Fact]
    public void Duplicate_source_hash_is_idempotently_ignored()
    {
        var observation = Observation(1);
        var result = Reconstruct([observation, observation with { PossDupFlag = true }]);
        Assert.Single(result.Orders);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Possdup_with_same_business_state_and_hash_is_idempotent()
    {
        var first = Observation(1);
        var duplicate = first with { PossDupFlag = true };
        Assert.Equal(Reconstruct([first]).ReconstructionSha256, Reconstruct([first, duplicate]).ReconstructionSha256);
    }

    [Fact]
    public void Conflicting_same_sequence_is_fail_closed()
    {
        var result = Reconstruct([
            Observation(1),
            Observation(1) with { SourceMessageSha256 = new string('f', 64), ClOrdId = "CL-CONFLICT" }
        ]);
        Assert.Contains("CONFLICTING_FIX_SEQUENCE:1", result.Issues);
        Assert.True(result.SequenceGap);
        Assert.False(result.BrokerAuthority);
    }

    [Fact]
    public void Sequence_gap_is_detected()
    {
        var result = Reconstruct([Observation(1), Observation(3, clOrdId: "CL-2", orderId: "ORDER-2")]);
        Assert.Contains("FIX_SEQUENCE_GAP:2-2", result.Issues);
        Assert.True(result.SequenceGap);
    }

    [Fact]
    public void Inconsistent_leaves_formula_is_detected()
    {
        var result = Reconstruct([Observation(1, qty: 100m, cum: 25m, leaves: 60m)]);
        Assert.Contains("LEAVES_QTY_INCONSISTENT:CL-1", result.Issues);
        Assert.False(result.BrokerAuthority);
    }

    [Fact]
    public void Account_scope_mismatch_is_detected()
    {
        var result = Reconstruct([Observation(1) with { AccountId = "OTHER" }]);
        Assert.Contains("ACCOUNT_SCOPE_MISMATCH:OTHER", result.Issues);
    }

    [Fact]
    public void Session_scope_mismatch_is_detected()
    {
        var result = Reconstruct([Observation(1) with { SourceSession = "OTHER" }]);
        Assert.Contains("SOURCE_SESSION_MISMATCH:OTHER", result.Issues);
    }

    [Fact]
    public void Invalid_source_message_sha_is_detected()
    {
        var result = Reconstruct([Observation(1) with { SourceMessageSha256 = "abc" }]);
        Assert.Contains("SOURCE_MESSAGE_SHA256_INVALID", result.Issues);
    }

    [Fact]
    public void Invalid_sequence_number_is_detected()
    {
        var result = Reconstruct([Observation(0)]);
        Assert.Contains("FIX_SEQUENCE_INVALID:0", result.Issues);
    }

    [Fact]
    public void Missing_order_identity_is_detected()
    {
        var result = Reconstruct([Observation(1) with { OrderId = "", ClOrdId = "" }]);
        Assert.Contains("ORDER_IDENTITY_INCOMPLETE", result.Issues);
    }

    [Fact]
    public void Cancel_replace_chain_collapses_to_latest_order_state()
    {
        var result = Reconstruct([
            Observation(1, clOrdId: "CL-OLD", orderId: "ORDER-1"),
            Observation(2, status: "E", clOrdId: "CL-NEW", orderId: "ORDER-1") with
            {
                OrigClOrdId = "CL-OLD",
                ExecType = "5"
            }
        ]);
        var state = Assert.Single(result.Orders);
        Assert.Equal("CL-NEW", state.ClOrdId);
        Assert.Equal("CL-OLD", state.OrigClOrdId);
        Assert.True(state.Working);
    }

    [Fact]
    public void Two_known_orders_are_reconstructed_without_claiming_global_completeness()
    {
        var result = Reconstruct([
            Observation(1, qty: 100m, leaves: 100m),
            Observation(2, side: "2", qty: 40m, leaves: 40m, clOrdId: "CL-2", orderId: "ORDER-2")
        ]);
        Assert.Equal(2, result.Orders.Count);
        Assert.Equal(60m, result.SignedReservedWorkingLeavesBySecurityId["4001"]);
        Assert.False(result.SnapshotComplete);
        Assert.False(result.BrokerAuthority);
    }

    [Fact]
    public void Terminal_update_removes_order_from_reserved_leaves()
    {
        var result = Reconstruct([
            Observation(1),
            Observation(2, status: "2", cum: 100m, leaves: 0m)
        ]);
        Assert.Single(result.Orders);
        Assert.Empty(result.SignedReservedWorkingLeavesBySecurityId);
    }

    [Fact]
    public void Reconstruction_hash_is_deterministic_across_input_order()
    {
        var first = Observation(1);
        var second = Observation(2, clOrdId: "CL-2", orderId: "ORDER-2");
        Assert.Equal(
            Reconstruct([first, second]).ReconstructionSha256,
            Reconstruct([second, first]).ReconstructionSha256);
    }

    [Fact]
    public void Reconstruction_hash_is_full_sha256()
    {
        var hash = Reconstruct([Observation(1)]).ReconstructionSha256;
        Assert.Equal(64, hash.Length);
        Assert.All(hash, value => Assert.True(Uri.IsHexDigit(value)));
    }

    [Fact]
    public void Non_fix44_profile_is_inconclusive()
    {
        var decision = LmaxFixOpenOrderCapabilityClassifier.Classify(Profile() with
        {
            FixVersion = "4.2",
            BeginString = "FIX.4.2"
        });
        Assert.Equal(LmaxFixOpenOrderCapabilityCategory.INCONCLUSIVE, decision.Category);
        Assert.False(decision.BrokerAuthority);
    }

    [Fact]
    public void Profile_without_h_or_mass_is_open_order_discovery_unsupported()
    {
        var decision = LmaxFixOpenOrderCapabilityClassifier.Classify(Profile() with
        {
            OrderStatusRequestSupported = false
        });
        Assert.Equal(LmaxFixOpenOrderCapabilityCategory.LMAX_FIX_OPEN_ORDER_DISCOVERY_UNSUPPORTED, decision.Category);
    }

    private static LmaxFixProfileInventory Profile()
        => LmaxFixOpenOrderCapabilityClassifier.AuthoritativeBrokerFix44Profile();

    private static LmaxFixOpenOrderCapabilityDecision Decision()
        => LmaxFixOpenOrderCapabilityClassifier.Classify(Profile());

    private static LmaxFixOrderStateReconstruction Reconstruct(
        IReadOnlyList<LmaxFixExecutionReportOrderObservation> observations,
        bool explicitCompletion = false)
        => new LmaxFixExecutionReportOrderStateMachine().Reconstruct(
            observations, Decision(), Account, Session, explicitCompletion);

    private static LmaxFixExecutionReportOrderObservation Observation(
        long sequence,
        string status = "0",
        string side = "1",
        decimal qty = 100m,
        decimal cum = 0m,
        decimal leaves = 100m,
        string clOrdId = "CL-1",
        string orderId = "ORDER-1")
    {
        var hex = "abcdef"[(int)(Math.Abs(sequence) % 6)];
        return new(
            Account,
            orderId,
            clOrdId,
            null,
            "4001",
            side,
            qty,
            cum,
            leaves,
            status,
            status,
            1.10m,
            "0",
            new DateTimeOffset(2026, 7, 21, 13, 0, 0, TimeSpan.Zero).AddSeconds(sequence),
            sequence,
            Session,
            new string(hex, 64),
            PossDupFlag: false);
    }
}