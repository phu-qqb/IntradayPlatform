using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7aPmsShadowExecutionPipelineTests
{
    private const string Session = "arch6b-daily-tier1-20260721T130346Z-422530a8";
    private static readonly DateTimeOffset TargetClose = new(2026, 7, 21, 13, 15, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("EURUSD", "EURUSD", false, "4001")]
    [InlineData("GBPUSD", "GBPUSD", false, "4002")]
    [InlineData("AUDUSD", "AUDUSD", false, "4007")]
    [InlineData("USDJPY", "USDJPY", true, "4004")]
    [InlineData("NZDUSD", "NZDUSD", false, "100613")]
    [InlineData("USDCAD", "USDCAD", true, "4013")]
    [InlineData("USDCHF", "USDCHF", true, "4010")]
    public void Seven_historically_proven_symbols_map_to_expected_lmax_identity(
        string sourceSymbol, string executionSymbol, bool inverted, string securityId)
    {
        var plan = Build(Source([Contribution(sourceSymbol, 0.10m)]));

        var line = Assert.Single(plan.Netting.ExecutionLines);
        Assert.Equal(executionSymbol, line.ExecutionTradableSymbol);
        Assert.Equal(inverted, line.RequiresInversion);
        Assert.Equal(securityId, line.SecurityId);
        Assert.Equal("8", line.SecurityIdSource);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    public void Canonical_quarter_hour_closes_are_accepted(int minute)
    {
        var close = new DateTimeOffset(2026, 7, 21, 13, minute, 0, TimeSpan.Zero);
        var source = Source([Contribution("EURUSD", 0.10m)]) with { Slot = Slot(close), CompletedAtUtc = close.AddMinutes(-20) };
        Assert.NotEmpty(Build(source).Units);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(21)]
    [InlineData(36)]
    [InlineData(51)]
    public void Historical_noncanonical_closes_are_rejected(int minute)
    {
        var close = new DateTimeOffset(2026, 7, 21, 13, minute, 0, TimeSpan.Zero);
        var source = Source([Contribution("EURUSD", 0.10m)]) with { Slot = Slot(close), CompletedAtUtc = close.AddMinutes(-20) };
        Assert.Throws<InvalidOperationException>(() => Build(source));
    }

    [Fact]
    public void Plan_is_strictly_no_network_no_order_no_fill()
    {
        var plan = Build(Source([Contribution("EURUSD", 0.10m)]));
        Assert.True(plan.NoFixLogon);
        Assert.True(plan.NoBrokerSend);
        Assert.True(plan.NoAccountApi);
        Assert.True(plan.NoDatabento);
        Assert.True(plan.NoRealAccount);
        Assert.True(plan.NoFill);
        Assert.True(plan.NoPositionLedgerEvent);
        Assert.Empty(plan.NetworkLedger);
        Assert.All(plan.Units, unit =>
        {
            Assert.False(unit.TradeIntent.ExecutionAllowed);
            Assert.False(unit.TradeIntent.BrokerRouteAllowed);
            Assert.False(unit.RiskDecision.BrokerSendAllowed);
            Assert.False(unit.ParentOrder.RouteAllowed);
            Assert.False(unit.ChildOrder.BrokerSendAllowed);
        });
    }

    [Fact]
    public void Unknown_working_leaves_still_allow_non_actionable_shadow_simulation()
    {
        var plan = Build(Source([Contribution("EURUSD", 0.10m)]) with
        {
            WorkingOrderAuthority = Arch7aWorkingOrderAuthority.UnavailableWithCurrentLmaxInterfaces
        });
        var unit = Assert.Single(plan.Units);
        Assert.False(unit.TradeIntent.Actionable);
        Assert.Equal(Arch7aShadowRiskOutcome.BLOCK_NEW_ORDERS, unit.RiskDecision.Outcome);
        Assert.Equal("BROKER_WORKING_LEAVES_UNOBSERVABLE", unit.TradeIntent.BlockingReason);
        Assert.Contains("BROKER_WORKING_LEAVES_UNOBSERVABLE", plan.Blockers);
    }

    [Fact]
    public void Complete_authorities_produce_approved_shadow_but_never_route()
    {
        var unit = Assert.Single(Build(Source([Contribution("EURUSD", 0.10m)])).Units);
        Assert.True(unit.TradeIntent.Actionable);
        Assert.Equal(Arch7aShadowRiskOutcome.APPROVED_SHADOW, unit.RiskDecision.Outcome);
        Assert.False(unit.TradeIntent.ExecutionAllowed);
        Assert.False(unit.ParentOrder.RouteAllowed);
    }

    [Fact]
    public void Missing_position_authority_blocks_new_orders()
    {
        var unit = Assert.Single(Build(Source([Contribution("EURUSD", 0.10m)]) with
        {
            PositionAuthority = false
        }).Units);
        Assert.Equal(Arch7aShadowRiskOutcome.BLOCK_NEW_ORDERS, unit.RiskDecision.Outcome);
        Assert.Equal("BROKER_POSITION_AUTHORITY_UNAVAILABLE", unit.TradeIntent.BlockingReason);
    }

    [Fact]
    public void Critical_reconciliation_conflict_emergency_stops_shadow_unit()
    {
        var unit = Assert.Single(Build(Source([Contribution("EURUSD", 0.10m)]) with
        {
            HasCriticalConflict = true
        }).Units);
        Assert.Equal(Arch7aShadowRiskOutcome.EMERGENCY_STOP, unit.RiskDecision.Outcome);
        Assert.Equal("CRITICAL_RECONCILIATION_CONFLICT", unit.TradeIntent.BlockingReason);
    }

    [Theory]
    [InlineData(Arch7aSourceStatus.Incomplete, Arch7aSourceFreshness.Fresh, true, "TEST", "ARCH7A_TEST")]
    [InlineData(Arch7aSourceStatus.Completed, Arch7aSourceFreshness.Stale, true, "TEST", "ARCH7A_TEST")]
    [InlineData(Arch7aSourceStatus.Completed, Arch7aSourceFreshness.Incomplete, true, "TEST", "ARCH7A_TEST")]
    [InlineData(Arch7aSourceStatus.Completed, Arch7aSourceFreshness.Fresh, false, "TEST", "ARCH7A_TEST")]
    [InlineData(Arch7aSourceStatus.Completed, Arch7aSourceFreshness.Fresh, true, "PRODUCTION", "ARCH7A_TEST")]
    [InlineData(Arch7aSourceStatus.Completed, Arch7aSourceFreshness.Fresh, true, "TEST", "REAL_ACCOUNT")]
    public void Ineligible_source_shapes_construct_no_shadow_units(
        Arch7aSourceStatus status, Arch7aSourceFreshness freshness, bool lineage,
        string environment, string account)
    {
        var source = Source([Contribution("EURUSD", 0.10m)]) with
        {
            Status = status,
            Freshness = freshness,
            LineageComplete = lineage,
            Environment = environment,
            AccountScope = account
        };
        Assert.Empty(Build(source).Units);
    }

    [Fact]
    public void Working_leaves_policy_can_forbid_even_shadow_construction()
    {
        var source = Source([Contribution("EURUSD", 0.10m)]) with
        {
            WorkingOrderAuthority = Arch7aWorkingOrderAuthority.UnavailableWithCurrentLmaxInterfaces,
            AllowShadowSimulationWhenWorkingLeavesUnknown = false
        };
        var plan = Build(source);
        Assert.Empty(plan.Units);
        Assert.Contains("WORKING_LEAVES_POLICY_FORBIDS_CONSTRUCTION", plan.Blockers);
    }

    [Fact]
    public void Direct_cross_is_netted_through_usd_pairs_and_never_executed_directly()
    {
        var plan = Build(Source([Contribution("EURGBP", 0.10m)]));
        Assert.Contains("EURGBP", plan.Netting.DirectCrossesExcluded);
        Assert.True(plan.Netting.DirectCrossExecutionDisabled);
        Assert.DoesNotContain(plan.Netting.ExecutionLines, line => line.ExecutionTradableSymbol == "EURGBP");
        Assert.Equal(["EURUSD", "GBPUSD"], plan.Netting.ExecutionLines.Select(x => x.ExecutionTradableSymbol).ToArray());
    }

    [Fact]
    public void Unsupported_currency_blocks_the_whole_plan_instead_of_trading_one_leg()
    {
        var plan = Build(Source([Contribution("AUDCNH", 0.10m)]));
        Assert.Contains("CNH", plan.Netting.UnsupportedCurrencies);
        Assert.Empty(plan.Units);
        Assert.Contains("UNSUPPORTED_EXECUTION_CURRENCY:CNH", plan.Blockers);
    }

    [Fact]
    public void Unsupported_cross_is_excluded_atomically_without_blocking_independent_supported_symbols()
    {
        var plan = Build(Source([
            Contribution("EURUSD", 0.10m),
            Contribution("AUDCNH", 0.10m)
        ]));

        Assert.Contains("CNH", plan.Netting.UnsupportedCurrencies);
        Assert.Contains(plan.Units, value => value.TradeIntent.ExecutionTradableSymbol == "EURUSD");
        Assert.DoesNotContain(plan.Units, value => value.TradeIntent.ExecutionTradableSymbol == "AUDUSD");
        Assert.DoesNotContain(plan.Units, value => value.TradeIntent.ExecutionTradableSymbol == "AUDCNH");
    }
    [Fact]
    public void Netted_symbol_produces_at_most_one_parent_and_one_child()
    {
        var plan = Build(Source([
            Contribution("EURUSD", 0.10m, "strategy-a"),
            Contribution("EURUSD", 0.05m, "strategy-b")
        ]));
        Assert.Single(plan.Units);
        Assert.Single(plan.Units.Select(value => value.ParentOrder.Canonical.Id).Distinct());
        Assert.Single(plan.Units.Select(value => value.ChildOrder.Canonical.Id).Distinct());
    }
    [Fact]
    public void Opposite_strategy_weights_net_to_zero_before_intent_creation()
    {
        var plan = Build(Source([
            Contribution("EURUSD", 0.10m, "strategy-a"),
            Contribution("EURUSD", -0.10m, "strategy-b")
        ]));
        Assert.Empty(plan.Units);
    }

    [Fact]
    public void Multiple_strategies_preserve_lineage_on_one_netted_intent()
    {
        var plan = Build(Source([
            Contribution("EURUSD", 0.10m, "strategy-a"),
            Contribution("EURUSD", 0.05m, "strategy-b")
        ]));
        var unit = Assert.Single(plan.Units);
        Assert.Equal(2, unit.TradeIntent.ModelRunIds.Count);
        Assert.Equal(2, unit.TradeIntent.TargetPositionIds.Count);
        Assert.Equal(2, unit.TradeIntent.DriftIds.Count);
    }

    [Fact]
    public void Usdjpy_uses_jpyusd_normalization_and_inversion()
    {
        var line = Assert.Single(Build(Source([Contribution("USDJPY", 0.10m)])).Netting.ExecutionLines);
        Assert.Equal("JPYUSD", line.NormalizedPortfolioSymbol);
        Assert.Equal("USDJPY", line.ExecutionTradableSymbol);
        Assert.True(line.RequiresInversion);
        Assert.True(line.TargetExecutionQuantity > 0m);
    }

    [Fact]
    public void Replay_is_byte_semantically_deterministic()
    {
        var source = Source([Contribution("EURUSD", 0.10m)]);
        var first = Build(source);
        var second = Build(source);
        Assert.Equal(first.Netting.NettingSha256, second.Netting.NettingSha256);
        Assert.Equal(first.PlanSha256, second.PlanSha256);
        Assert.Equal(first.Units[0].TradeIntent.Canonical.Id, second.Units[0].TradeIntent.Canonical.Id);
        Assert.Equal(first.Units[0].ParentOrder.Canonical.Id, second.Units[0].ParentOrder.Canonical.Id);
        Assert.Equal(first.Units[0].ChildOrder.Canonical.Id, second.Units[0].ChildOrder.Canonical.Id);
    }

    [Fact]
    public void Persisted_identities_and_hashes_are_full_sha256()
    {
        var plan = Build(Source([Contribution("EURUSD", 0.10m)]));
        var unit = Assert.Single(plan.Units);
        AssertHash(plan.PlanSha256);
        AssertHash(plan.Netting.NettingSha256);
        AssertHash(unit.TradeIntent.IdempotencyKey);
        AssertHash(unit.TradeIntent.LineageSha256);
        AssertHash(unit.ParentOrder.DeterministicIdentity);
        AssertHash(unit.ChildOrder.DeterministicIdentity);
    }

    [Fact]
    public void Close_seeking_foundation_is_reused_with_three_phases()
    {
        var plan = Build(Source([Contribution("EURUSD", 0.10m)]));
        Assert.Equal(3, plan.CloseSeekingPhases.Count);
        Assert.Equal(CloseSeekingPhaseName.PassiveOpportunistic, plan.CloseSeekingPhases[0].PhaseName);
        Assert.Equal(CloseSeekingPhaseName.AdaptiveUrgency, plan.CloseSeekingPhases[1].PhaseName);
        Assert.Equal(CloseSeekingPhaseName.ControlledResidualCompletion, plan.CloseSeekingPhases[2].PhaseName);
    }

    [Fact]
    public void Child_preview_is_deadline_bounded_and_has_no_overnight_carry()
    {
        var child = Assert.Single(Build(Source([Contribution("EURUSD", 0.10m)])).Units).ChildOrder;
        Assert.True(child.EffectiveTimeUtc < child.DeadlineUtc);
        Assert.Equal(TargetClose, child.DeadlineUtc);
        Assert.Equal(TargetClose.Date, child.EffectiveTimeUtc.Date);
        Assert.Equal("WHOLE_SHADOW_PREVIEW", child.Tranche);
    }

    [Fact]
    public async Task Coordinator_connects_pms_reader_pipeline_and_idempotent_shadow_store()
    {
        var source = Source([Contribution("EURUSD", 0.10m)]);
        var coordinator = new Arch7aShadowExecutionCoordinator(
            new FixedSourceReader(source), new Arch7aPmsShadowExecutionPipeline(),
            new InMemoryArch7aShadowExecutionStore());

        var first = await coordinator.RunAsync(Session, source.Slot, TargetClose);
        var replay = await coordinator.RunAsync(Session, source.Slot, TargetClose);

        Assert.True(first.Persisted);
        Assert.Equal(Arch7aShadowStoreResult.Persisted, first.StoreResult);
        Assert.Equal(Arch7aShadowStoreResult.AlreadyPersistedIdentical, replay.StoreResult);
        Assert.Equal(first.Plan.PlanSha256, replay.Plan.PlanSha256);
    }

    [Fact]
    public async Task Coordinator_does_not_persist_when_source_is_blocked_before_intent_creation()
    {
        var source = Source([Contribution("EURUSD", 0.10m)]) with
        {
            Freshness = Arch7aSourceFreshness.Stale
        };
        var store = new CountingStore();
        var result = await new Arch7aShadowExecutionCoordinator(
            new FixedSourceReader(source), new Arch7aPmsShadowExecutionPipeline(), store)
            .RunAsync(Session, source.Slot, TargetClose);

        Assert.False(result.Persisted);
        Assert.Null(result.StoreResult);
        Assert.Empty(result.Plan.Units);
        Assert.Equal(0, store.CallCount);
    }
    [Fact]
    public async Task In_memory_store_is_idempotent_for_identical_replay()
    {
        var store = new InMemoryArch7aShadowExecutionStore();
        var plan = Build(Source([Contribution("EURUSD", 0.10m)]));
        Assert.Equal(Arch7aShadowStoreResult.Persisted, await store.PersistAsync(plan));
        Assert.Equal(Arch7aShadowStoreResult.AlreadyPersistedIdentical, await store.PersistAsync(plan));
    }

    [Fact]
    public async Task In_memory_store_rejects_same_slot_with_different_plan()
    {
        var store = new InMemoryArch7aShadowExecutionStore();
        var plan = Build(Source([Contribution("EURUSD", 0.10m)]));
        await store.PersistAsync(plan);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.PersistAsync(plan with { PlanSha256 = new string('f', 64) }));
    }

    [Fact]
    public async Task In_memory_store_rejects_every_persisted_object_conflict_for_the_same_revision()
    {
        var store = new InMemoryArch7aShadowExecutionStore();
        var original = Build(Source([Contribution("EURUSD", 0.10m)]));
        await store.PersistAsync(original);
        var unit = Assert.Single(original.Units);

        Arch7aShadowExecutionPlan Rehash(Arch7aShadowExecutionPlan candidate) =>
            candidate with
            {
                PlanSha256 = Arch7aPmsShadowExecutionPipeline.ComputePlanSha256(
                    candidate.Netting, candidate.Units, candidate.Blockers)
            };
        async Task Reject(Arch7aShadowExecutionPlan candidate)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.PersistAsync(Rehash(candidate)));
            Assert.Equal("ARCH7A_IDEMPOTENCY_CONFLICT", error.Message);
        }

        await Reject(original with
        {
            Units =
            [
                unit with
                {
                    TradeIntent = unit.TradeIntent with
                    {
                        SignedDesiredDelta = unit.TradeIntent.SignedDesiredDelta + 1m
                    }
                }
            ]
        });
        await Reject(original with
        {
            Units =
            [
                unit with
                {
                    RiskDecision = unit.RiskDecision with { ReasonCodes = ["OTHER_RISK_REASON"] }
                }
            ]
        });
        await Reject(original with
        {
            Units =
            [
                unit with { ParentOrder = unit.ParentOrder with { Symbol = "GBPUSD" } }
            ]
        });
        await Reject(original with
        {
            Units =
            [
                unit with
                {
                    ChildOrder = unit.ChildOrder with
                    {
                        Canonical = unit.ChildOrder.Canonical with
                        {
                            ParentOrderId = new ParentOrderId(
                                Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"))
                        }
                    }
                }
            ]
        });

        var otherLineage = new string('e', 64);
        await Reject(original with
        {
            Netting = original.Netting with { SourceLineageSha256 = otherLineage },
            Units =
            [
                unit with
                {
                    TradeIntent = unit.TradeIntent with { SourceLineageSha256 = otherLineage }
                }
            ]
        });
    }
    [Fact]
    public void Artifact_sha_must_be_64_hex_characters()
    {
        var bad = Contribution("EURUSD", 0.10m) with { InputSha256 = "abc" };
        Assert.Throws<InvalidOperationException>(() => Build(Source([bad])));
    }

    [Fact]
    public void Git_commit_id_must_not_be_abbreviated()
    {
        var bad = Contribution("EURUSD", 0.10m) with { CoreCommitId = "a96ca2e" };
        Assert.Throws<InvalidOperationException>(() => Build(Source([bad])));
    }

    private static Arch7aShadowExecutionPlan Build(Arch7aPmsExecutionSource source)
        => new Arch7aPmsShadowExecutionPipeline().Build(source);

    private static Arch7aPmsExecutionSource Source(IReadOnlyList<Arch7aPmsTargetContribution> contributions)
        => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Session,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            2,
            new string('c', 64),
            new string('d', 64),
            TargetClose.AddMinutes(1),
            TargetClose.AddHours(-24),
            Slot(TargetClose),
            TargetClose.AddMinutes(-20),
            "TEST",
            "ARCH7A_TEST",
            100_000m,
            Arch7aSourceStatus.Completed,
            Arch7aSourceFreshness.Fresh,
            LineageComplete: true,
            PositionAuthority: true,
            Arch7aWorkingOrderAuthority.AuthoritativeComplete,
            AllowShadowSimulationWhenWorkingLeavesUnknown: true,
            HasCriticalConflict: false,
            contributions,
            new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["EURUSD"] = 1.10m, ["GBPUSD"] = 1.27m, ["AUDUSD"] = 0.66m,
                ["USDJPY"] = 157m, ["NZDUSD"] = 0.61m, ["USDCAD"] = 1.37m,
                ["USDCHF"] = 0.89m
            },
            new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                ["EURUSD"] = 0.01m, ["GBPUSD"] = 0.01m, ["AUDUSD"] = 0.01m,
                ["USDJPY"] = 0.01m, ["NZDUSD"] = 0.01m, ["USDCAD"] = 0.01m,
                ["USDCHF"] = 0.01m
            });

    private static Arch7aPmsTargetContribution Contribution(
        string symbol, decimal weight, string strategy = "strategy-a")
    {
        var suffix = $"{strategy}|{symbol}|{weight}";
        return new(
            Arch7aPmsShadowExecutionPipeline.DeterministicGuid($"model|{suffix}"),
            strategy,
            Arch7aPmsShadowExecutionPipeline.DeterministicGuid($"target|{suffix}"),
            Arch7aPmsShadowExecutionPipeline.DeterministicGuid($"drift|{suffix}"),
            symbol,
            symbol,
            weight,
            0m,
            0m,
            0m,
            new string('a', 64),
            new string('b', 64),
            "a96ca2eb725dcba3bc66579b8782fdd14ecfe97a");
    }

    private static Arch7aExecutionSlot Slot(DateTimeOffset close)
        => new($"ARCH7A-{close:yyyyMMddTHHmmssZ}", DateOnly.FromDateTime(close.UtcDateTime),
            close, close.AddMinutes(-15), close);

    private static void AssertHash(string value)
    {
        Assert.Equal(64, value.Length);
        Assert.All(value, character => Assert.True(Uri.IsHexDigit(character)));
    }
    private sealed class FixedSourceReader(Arch7aPmsExecutionSource source) : IArch7aPmsExecutionSourceReader
    {
        public Task<Arch7aPmsExecutionSource> ReadAsync(
            string sourceSessionId,
            Arch7aExecutionSlot slot,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(source.SourceSessionId, sourceSessionId);
            Assert.Equal(source.Slot, slot);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(source);
        }
    }

    private sealed class CountingStore : IArch7aShadowExecutionStore
    {
        public int CallCount { get; private set; }

        public Task<Arch7aShadowStoreResult> PersistAsync(
            Arch7aShadowExecutionPlan plan,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Arch7aShadowStoreResult.Persisted);
        }
    }}