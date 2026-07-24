using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bKnownOrderQualificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 1, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Exact_demo_preflight_is_allowed_and_identifiers_are_deterministic()
    {
        var first = Arch7bKnownOrderQualification.EvaluatePreflight(ValidPreflight());
        var second = Arch7bKnownOrderQualification.EvaluatePreflight(ValidPreflight());

        Assert.True(first.Allowed, string.Join(";", first.Blockers));
        Assert.Empty(first.Blockers);
        Assert.Equal(first.OpeningClientOrderId, second.OpeningClientOrderId);
        Assert.Equal(first.FlattenClientOrderId, second.FlattenClientOrderId);
        Assert.Equal(first.CancelClientOrderId, second.CancelClientOrderId);
        Assert.Equal(first.PolicySha256, second.PolicySha256);
        Assert.Equal(20, first.OpeningClientOrderId.Length);
        Assert.StartsWith("A7BO", first.OpeningClientOrderId, StringComparison.Ordinal);
        Assert.StartsWith("A7BF", first.FlattenClientOrderId, StringComparison.Ordinal);
        Assert.StartsWith("A7BC", first.CancelClientOrderId, StringComparison.Ordinal);
        Assert.Equal(1.33703m, first.OpeningLimitPrice);
        Assert.Equal(1.33723m, first.MaximumOpeningPrice);
        Assert.Equal(1.33681m, first.MinimumOpeningPrice);
        Assert.Equal(64, first.PolicySha256.Length);
    }

    [Theory]
    [InlineData("TEST_ONLY")]
    [InlineData("")]
    [InlineData("1754288006")]
    public void Noncanonical_child_account_scope_is_fail_closed(string accountScope)
    {
        var input = ValidPreflight() with
        {
            ChildOrder = ValidChild() with { AccountScope = accountScope }
        };

        var decision = Arch7bKnownOrderQualification.EvaluatePreflight(input);

        Assert.False(decision.Allowed);
        Assert.Contains("ARCH7B_CHILD_ACCOUNT_SCOPE_MISMATCH", decision.Blockers);
        Assert.Contains("ARCH7B_CHILD_CONFIGURED_ACCOUNT_MISMATCH", decision.Blockers);
    }

    [Fact]
    public void Real_child_account_scope_is_explicitly_forbidden()
    {
        var input = ValidPreflight() with
        {
            ChildOrder = ValidChild() with
            {
                AccountScope = Arch7bKnownOrderQualificationPolicy.ForbiddenRealAccountId
            }
        };

        var decision = Arch7bKnownOrderQualification.EvaluatePreflight(input);

        Assert.False(decision.Allowed);
        Assert.Contains("ARCH7B_CHILD_ACCOUNT_SCOPE_MISMATCH", decision.Blockers);
        Assert.Contains("ARCH7B_CHILD_CONFIGURED_ACCOUNT_MISMATCH", decision.Blockers);
        Assert.Contains("ARCH7B_REAL_ACCOUNT_FORBIDDEN", decision.Blockers);
    }

    [Theory]
    [InlineData("TEST_PMS_SHADOW_ONLY")]
    [InlineData("")]
    [InlineData("UNKNOWN")]
    public void Noncanonical_trade_intent_classification_is_fail_closed(string classification)
    {
        var input = ValidPreflight() with
        {
            ChildOrder = ValidChild() with { TradeIntentClassification = classification }
        };

        var decision = Arch7bKnownOrderQualification.EvaluatePreflight(input);

        Assert.False(decision.Allowed);
        Assert.Contains(
            "ARCH7B_TRADE_INTENT_CLASSIFICATION_MISMATCH",
            decision.Blockers);
    }

    [Fact]
    public void Historical_1030z_contract_passes_scope_but_stays_blocked_when_stale()
    {
        var input = ValidPreflight() with
        {
            ChildOrder = ValidChild() with
            {
                TradeIntentId = Guid.Parse("b17c07ba-5cdb-56f3-bef8-93cf7c113ac0"),
                ParentOrderId = Guid.Parse("f1c90e85-407f-5564-adea-61626f324454"),
                ChildOrderId = Guid.Parse("516f9016-404d-5afc-8be0-6ad6f2e1f320"),
                SlotId = "pms-shadow-15m-20260724T1030Z",
                EconomicRevisionId = Guid.Parse("91eae733-cd6d-b886-b8f7-d9f3b020f1c4"),
                MarketDataSnapshotSha256 =
                    "65769fb842e3dc22bbd26f00adb49b5c1b47a2777c59430b4258bb273184597b",
                SourceFresh = false
            }
        };

        var decision = Arch7bKnownOrderQualification.EvaluatePreflight(input);

        Assert.False(decision.Allowed);
        Assert.Equal(["ARCH7B_SOURCE_NOT_FRESH"], decision.Blockers);
    }

    [Fact]
    public void Economic_and_account_safety_guardrails_remain_fail_closed()
    {
        var input = ValidPreflight() with
        {
            ConfiguredEnvironment = "PRODUCTION",
            ChildOrder = ValidChild() with
            {
                Environment = "PRODUCTION",
                Symbol = "EURUSD",
                SecurityId = "1001",
                EconomicRevisionNumber = 1,
                LatestQualifyingRevision = false,
                SourceCompleted = false,
                SourceFresh = false,
                SourceSuperseded = true,
                LmaxMarketData = false,
                PolygonOrderPrice = true,
                LineageComplete = false
            },
            Bbo = ValidBbo() with { PolygonUsed = true },
            CurrentKnownPosition = 0.1m,
            PlatformKnownWorkingOrderCount = 1
        };

        var decision = Arch7bKnownOrderQualification.EvaluatePreflight(input);

        Assert.False(decision.Allowed);
        var expected = new[]
        {
            "ARCH7B_ENVIRONMENT_NOT_TEST",
            "ARCH7B_CHILD_ENVIRONMENT_MISMATCH",
            "ARCH7B_SELECTED_SYMBOL_MISMATCH",
            "ARCH7B_SECURITY_ID_MISMATCH",
            "ARCH7B_SOURCE_NOT_LATEST_QUALIFYING_REVISION",
            "ARCH7B_ECONOMIC_REVISION_TWO_REQUIRED",
            "ARCH7B_SOURCE_NOT_COMPLETED",
            "ARCH7B_SOURCE_NOT_FRESH",
            "ARCH7B_SOURCE_SUPERSEDED",
            "ARCH7B_SOURCE_NOT_LMAX_MARKET_DATA",
            "ARCH7B_POLYGON_ORDER_PRICE_FORBIDDEN",
            "ARCH7B_SOURCE_LINEAGE_INCOMPLETE",
            "ARCH7B_INITIAL_POSITION_NOT_FLAT",
            "ARCH7B_PLATFORM_KNOWN_WORKING_ORDER_PRESENT"
        };
        Assert.All(expected, blocker => Assert.Contains(blocker, decision.Blockers));
    }

    [Fact]
    public void Real_account_and_stale_non_lmax_bbo_are_fail_closed()
    {
        var input = ValidPreflight() with
        {
            ConfiguredAccountId = Arch7bKnownOrderQualificationPolicy.ForbiddenRealAccountId,
            Bbo = ValidBbo() with { Source = "POLYGON", ObservedAtUtc = Now.AddSeconds(-6) }
        };

        var decision = Arch7bKnownOrderQualification.EvaluatePreflight(input);

        Assert.False(decision.Allowed);
        Assert.Contains("ARCH7B_DEMO_ACCOUNT_IDENTITY_MISMATCH", decision.Blockers);
        Assert.Contains("ARCH7B_REAL_ACCOUNT_FORBIDDEN", decision.Blockers);
        Assert.Contains("ARCH7B_BBO_SOURCE_NOT_LMAX", decision.Blockers);
        Assert.Contains("ARCH7B_BBO_STALE", decision.Blockers);
    }

    [Fact]
    public void Missing_exclusivity_or_operator_authorization_is_fail_closed()
    {
        var input = ValidPreflight() with
        {
            ExactOperatorAuthorizationPresent = false,
            Exclusivity = ValidExclusivity() with
            {
                AdvisoryLeaseHeld = false,
                NoManualOrdersDeclared = false
            }
        };

        var decision = Arch7bKnownOrderQualification.EvaluatePreflight(input);

        Assert.False(decision.Allowed);
        Assert.Contains("ARCH7B_EXACT_OPERATOR_AUTHORIZATION_MISSING", decision.Blockers);
        Assert.Contains("ARCH7B_EXCLUSIVITY_LEASE_NOT_HELD", decision.Blockers);
        Assert.Contains("ARCH7B_EXCLUSIVITY_DECLARATION_INCOMPLETE", decision.Blockers);
    }

    [Fact]
    public void Application_message_budget_is_explicit_and_bounded()
    {
        Arch7bKnownOrderQualification.ValidateBudget(new(2, 1, 0, 4));

        var error = Assert.Throws<InvalidOperationException>(
            () => Arch7bKnownOrderQualification.ValidateBudget(new(3, 1, 0, 4)));

        Assert.Equal("ARCH7B_APPLICATION_MESSAGE_BUDGET_EXCEEDED", error.Message);
    }

    [Fact]
    public void Complete_open_and_flatten_fill_reconciles_flat()
    {
        var lifecycle = Arch7bKnownOrderQualification.EvaluateLifecycle(
            CompleteLifecycle(),
            "A7BO0000000000000001",
            "A7BF0000000000000001");

        Assert.True(lifecycle.Qualified, string.Join(";", lifecycle.Issues));
        Assert.True(lifecycle.Flat);
        Assert.Equal(0.1m, lifecycle.OpeningFilledQuantity);
        Assert.Equal(0.1m, lifecycle.FlattenFilledQuantity);
        Assert.Equal(0m, lifecycle.InternalPosition);
        Assert.Equal(0m, lifecycle.ResidualQuantity);
        Assert.Equal(0.000001m, lifecycle.RealizedPnlBeforeFees);
        Assert.Equal("BROKER_FEES_UNAVAILABLE_NOT_ASSUMED_ZERO", lifecycle.FeeStatus);
        Assert.Equal(2, lifecycle.Fills.Count);
        Assert.Equal(0, lifecycle.CriticalBreakCount);
    }

    [Fact]
    public void Partial_opening_fills_require_flatten_of_exact_executed_quantity()
    {
        var reports = new[]
        {
            Report(1, "OPEN-1", "A7BO0000000000000001", "E1", "F", "1", "BUY", 0.1m, 0.04m, 0.06m, 0.04m, 1.33703m),
            Report(2, "OPEN-1", "A7BO0000000000000001", "E2", "F", "2", "BUY", 0.1m, 0.1m, 0m, 0.06m, 1.33704m),
            Report(3, "FLAT-1", "A7BF0000000000000001", "E3", "F", "2", "SELL", 0.1m, 0.1m, 0m, 0.1m, 1.33702m)
        };

        var lifecycle = Arch7bKnownOrderQualification.EvaluateLifecycle(
            reports,
            "A7BO0000000000000001",
            "A7BF0000000000000001");

        Assert.True(lifecycle.Qualified, string.Join(";", lifecycle.Issues));
        Assert.Equal(3, lifecycle.Fills.Count);
        Assert.Equal(0.1m, lifecycle.OpeningFilledQuantity);
        Assert.Equal(0.1m, lifecycle.FlattenFilledQuantity);
        Assert.Equal(0m, lifecycle.InternalPosition);
    }

    [Fact]
    public void Partial_fill_then_late_fill_before_cancelled_uses_terminal_cumqty()
    {
        const string openingId = "A7BO0000000000000001";
        const string cancelId = "A7BC0000000000000001";
        const string flattenId = "A7BF0000000000000001";
        var cancel = Report(
            3, "OPEN-1", cancelId, "CANCEL-1", "4", "4", "BUY",
            0.1m, 0.07m, 0m, 0m, 0m) with
        {
            OrigClOrdId = openingId
        };
        var reports = new[]
        {
            Report(1, "OPEN-1", openingId, "E1", "F", "1", "BUY",
                0.1m, 0.04m, 0.06m, 0.04m, 1.33703m),
            Report(2, "OPEN-1", openingId, "E2", "F", "1", "BUY",
                0.1m, 0.07m, 0.03m, 0.03m, 1.33704m),
            cancel,
            Report(4, "FLAT-1", flattenId, "E3", "F", "2", "SELL",
                0.07m, 0.07m, 0m, 0.07m, 1.33702m)
        };

        var lifecycle = Arch7bKnownOrderQualification.EvaluateLifecycle(
            reports,
            openingId,
            flattenId,
            cancelId);

        Assert.True(lifecycle.Qualified, string.Join(";", lifecycle.Issues));
        Assert.Equal(0.07m, lifecycle.OpeningFilledQuantity);
        Assert.Equal(0.07m, lifecycle.FlattenFilledQuantity);
    }

    [Fact]
    public void Opening_fill_sum_divergence_from_terminal_cumqty_is_emergency_stop()
    {
        var reports = CompleteLifecycle().ToArray();
        reports[0] = reports[0] with
        {
            CumQty = 0.1m,
            LastQty = 0.04m,
            RawMessageSha256 = Sha("opening-divergence")
        };
        reports[1] = reports[1] with
        {
            OrderQty = 0.04m,
            CumQty = 0.04m,
            LastQty = 0.04m,
            RawMessageSha256 = Sha("flatten-004")
        };

        var lifecycle = Arch7bKnownOrderQualification.EvaluateLifecycle(
            reports,
            "A7BO0000000000000001",
            "A7BF0000000000000001");

        Assert.False(lifecycle.Qualified);
        Assert.Contains(
            "ARCH7B_OPENING_FILL_CUMQTY_DIVERGENCE_EMERGENCY_STOP",
            lifecycle.Issues);
    }

    [Fact]
    public void Flatten_fill_sum_divergence_or_overfill_is_emergency_stop()
    {
        var reports = CompleteLifecycle().ToArray();
        reports[1] = reports[1] with
        {
            OrderQty = 0.1m,
            CumQty = 0.1m,
            LastQty = 0.11m,
            RawMessageSha256 = Sha("flatten-overfill")
        };

        var lifecycle = Arch7bKnownOrderQualification.EvaluateLifecycle(
            reports,
            "A7BO0000000000000001",
            "A7BF0000000000000001");

        Assert.False(lifecycle.Qualified);
        Assert.Contains(
            "ARCH7B_FLATTEN_FILL_CUMQTY_DIVERGENCE_EMERGENCY_STOP",
            lifecycle.Issues);
        Assert.Contains("ARCH7B_INTERNAL_POSITION_NOT_FLAT", lifecycle.Issues);
    }
    [Fact]
    public void Byte_identical_execution_report_replay_is_idempotent()
    {
        var reports = CompleteLifecycle().ToList();
        reports.Add(reports[0]);

        var lifecycle = Arch7bKnownOrderQualification.EvaluateLifecycle(
            reports,
            "A7BO0000000000000001",
            "A7BF0000000000000001");

        Assert.True(lifecycle.Qualified, string.Join(";", lifecycle.Issues));
        Assert.Equal(2, lifecycle.AcceptedExecutionReports.Count);
        Assert.Equal(2, lifecycle.Fills.Count);
    }

    [Fact]
    public void Unknown_order_conflicting_exec_and_sequence_gap_are_critical_breaks()
    {
        var reports = new[]
        {
            Report(1, "OPEN-1", "UNKNOWN", "E1", "F", "2", "BUY", 0.1m, 0.1m, 0m, 0.1m, 1.33703m),
            Report(3, "FLAT-1", "A7BF0000000000000001", "E1", "F", "2", "SELL", 0.1m, 0.1m, 0m, 0.1m, 1.33704m)
        };

        var lifecycle = Arch7bKnownOrderQualification.EvaluateLifecycle(
            reports,
            "A7BO0000000000000001",
            "A7BF0000000000000001");

        Assert.False(lifecycle.Qualified);
        Assert.Contains("ARCH7B_UNKNOWN_CLORDID:UNKNOWN", lifecycle.Issues);
        Assert.Contains("ARCH7B_DUPLICATE_EXEC_ID_CONFLICT:E1", lifecycle.Issues);
        Assert.Contains("ARCH7B_FIX_SEQUENCE_GAP:FIX-SESSION-1:2-2", lifecycle.Issues);
    }

    [Fact]
    public void Full_fix_session_continuity_proof_avoids_false_gap_from_admin_messages()
    {
        var reports = CompleteLifecycle().ToArray();
        reports[1] = reports[1] with { SequenceNumber = 3 };

        var lifecycle = Arch7bKnownOrderQualification.EvaluateLifecycle(
            reports,
            "A7BO0000000000000001",
            "A7BF0000000000000001",
            fullFixSessionSequenceValidated: true);

        Assert.True(lifecycle.Qualified, string.Join(";", lifecycle.Issues));
        Assert.DoesNotContain(
            lifecycle.Issues,
            value => value.StartsWith("ARCH7B_FIX_SEQUENCE_GAP", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_flatten_never_qualifies()
    {
        var lifecycle = Arch7bKnownOrderQualification.EvaluateLifecycle(
            CompleteLifecycle().Take(1),
            "A7BO0000000000000001",
            "A7BF0000000000000001");

        Assert.False(lifecycle.Qualified);
        Assert.False(lifecycle.Flat);
        Assert.Equal(0.1m, lifecycle.ResidualQuantity);
        Assert.Contains("ARCH7B_FLATTEN_NOT_CONFIRMED", lifecycle.Issues);
        Assert.Contains("ARCH7B_INTERNAL_POSITION_NOT_FLAT", lifecycle.Issues);
    }

    [Fact]
    public void PossDup_replay_with_identical_business_fields_is_idempotent()
    {
        var reports = CompleteLifecycle().ToList();
        reports.Add(reports[0] with
        {
            PossDup = true,
            RawMessageSha256 = Sha("possdup-replay")
        });

        var lifecycle = Arch7bKnownOrderQualification.EvaluateLifecycle(
            reports,
            "A7BO0000000000000001",
            "A7BF0000000000000001");

        Assert.True(lifecycle.Qualified, string.Join(";", lifecycle.Issues));
        Assert.Equal(2, lifecycle.AcceptedExecutionReports.Count);
        Assert.Equal(2, lifecycle.Fills.Count);
        Assert.DoesNotContain(
            lifecycle.Issues,
            issue => issue.StartsWith("ARCH7B_CONFLICTING_FIX_SEQUENCE", StringComparison.Ordinal));
        Assert.DoesNotContain(
            lifecycle.Issues,
            issue => issue.StartsWith("ARCH7B_DUPLICATE_EXEC_ID_CONFLICT", StringComparison.Ordinal));
    }

    [Fact]
    public void Partial_fill_cancel_report_terminalizes_opening_through_orig_clordid()
    {
        const string openingId = "A7BO0000000000000001";
        const string cancelId = "A7BC0000000000000001";
        const string flattenId = "A7BF0000000000000001";
        var cancel = Report(
            2, "OPEN-1", cancelId, "E2", "4", "4", "BUY",
            0.1m, 0.04m, 0m, 0m, 0m) with
        {
            OrigClOrdId = openingId
        };
        var reports = new[]
        {
            Report(1, "OPEN-1", openingId, "E1", "F", "1", "BUY",
                0.1m, 0.04m, 0.06m, 0.04m, 1.33703m),
            cancel,
            Report(3, "FLAT-1", flattenId, "E3", "F", "2", "SELL",
                0.04m, 0.04m, 0m, 0.04m, 1.33702m)
        };

        var lifecycle = Arch7bKnownOrderQualification.EvaluateLifecycle(
            reports, openingId, flattenId, cancelId);

        Assert.True(lifecycle.Qualified, string.Join(";", lifecycle.Issues));
        Assert.Equal(0.04m, lifecycle.OpeningFilledQuantity);
        Assert.Equal(0.04m, lifecycle.FlattenFilledQuantity);
        Assert.True(lifecycle.Orders.Single(order => order.ClOrdId == openingId).Terminal);
    }

    private static Arch7bPreflightInput ValidPreflight()
        => new(
            ValidChild(),
            ValidBbo(),
            ValidExclusivity(),
            Arch7bKnownOrderQualificationPolicy.Environment,
            Arch7bKnownOrderQualificationPolicy.DemoAccountId,
            0m,
            0,
            true,
            true,
            Now);

    private static Arch7bSelectedChildOrder ValidChild()
        => new(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            "A7CA4AF665F369BB31F",
            "arch6b-daily-tier1-20260721T130346Z-422530a8",
            "arch6f-20260722T230000Z",
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            2,
            Sha("market"),
            Sha("lineage"),
            Sha("plan"),
            "TEST",
            Arch7bKnownOrderQualificationPolicy.DemoAccountId,
            "GBPUSD",
            "4002",
            "8",
            "BUY",
            703497m,
            Now.AddMinutes(15),
            Now.AddMinutes(-15),
            Now.AddMinutes(30),
            Arch7aPmsShadowExecutionContract.ShadowTradeIntentClassification,
            "SHADOW_PLANNED",
            "SHADOW_ONLY",
            true,
            true,
            true,
            false,
            true,
            false,
            true);

    private static Arch7bLmaxBbo ValidBbo()
        => new("GBPUSD", "4002", 1.33701m, 1.33703m, Now.AddSeconds(-1), "LMAX", Sha("bbo"), Now.AddSeconds(-2), SequenceIntegrityProven: true, PolygonUsed: false);

    private static Arch7bExclusivityDeclaration ValidExclusivity()
        => new(
            "arch7b-test-owner",
            Now.AddMinutes(-1),
            Now.AddMinutes(10),
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true);

    private static IReadOnlyList<Arch7bExecutionReportEvent> CompleteLifecycle()
        =>
        [
            Report(1, "OPEN-1", "A7BO0000000000000001", "E1", "F", "2", "BUY", 0.1m, 0.1m, 0m, 0.1m, 1.33703m),
            Report(2, "FLAT-1", "A7BF0000000000000001", "E2", "F", "2", "SELL", 0.1m, 0.1m, 0m, 0.1m, 1.33704m)
        ];

    private static Arch7bExecutionReportEvent Report(
        long sequence,
        string orderId,
        string clOrdId,
        string execId,
        string execType,
        string ordStatus,
        string side,
        decimal orderQuantity,
        decimal cumulativeQuantity,
        decimal leavesQuantity,
        decimal lastQuantity,
        decimal lastPrice)
        => new(
            "FIX-SESSION-1",
            sequence,
            Arch7bKnownOrderQualificationPolicy.DemoAccountId,
            orderId,
            clOrdId,
            null,
            execId,
            execType,
            ordStatus,
            "GBPUSD",
            "4002",
            side,
            orderQuantity,
            cumulativeQuantity,
            leavesQuantity,
            lastQuantity,
            lastPrice,
            cumulativeQuantity == 0m ? 0m : lastPrice,
            1.33703m,
            Now.AddSeconds(sequence),
            false,
            Sha($"{sequence}|{orderId}|{clOrdId}|{execId}|{lastQuantity}|{lastPrice}"));

    private static string Sha(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
