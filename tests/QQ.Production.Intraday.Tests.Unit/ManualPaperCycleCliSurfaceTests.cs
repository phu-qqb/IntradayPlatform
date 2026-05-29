using System.Text.Json;
using System.Diagnostics;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Domain;
using QQ.Production.Intraday.Domain.PmsEmsOmsFoundation;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class ManualPaperCycleCliSurfaceTests
{
    private static readonly DateTimeOffset ProducedAt = new(2026, 05, 20, 09, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveAt = ProducedAt.AddMinutes(15);
    private const decimal PortfolioNotional = 1_000_000m;

    [Fact]
    public async Task Cli_accepts_valid_manual_no_external_request()
    {
        var result = await RunAsync();

        Assert.Equal(ManualPaperCycleCliStatus.CompletedNoExternal, result.CliStatus);
        Assert.Equal(PaperCycleRunMode.ManualNoExternal, result.ParsedRequest?.Request.RunMode);
        Assert.True(result.NoExternal);
    }

    [Fact]
    public async Task Cli_rejects_non_manual_no_external_mode()
    {
        var result = await RunAsync(Args("--mode", "SchedulerRequested"));

        Assert.Equal(ManualPaperCycleCliStatus.RejectedUnsafeMode, result.CliStatus);
        Assert.Equal(ManualPaperCycleCliRejectedReason.InvalidMode, result.RejectedReason);
        Assert.False(result.CycleExecuted);
    }

    [Fact]
    public async Task Cli_requires_qubes_run_id()
    {
        var result = await RunAsync(ArgsWithout("--qubes-run-id"));

        Assert.Equal(ManualPaperCycleCliStatus.RejectedInvalidArguments, result.CliStatus);
        Assert.Equal(ManualPaperCycleCliRejectedReason.MissingRequiredArgument, result.RejectedReason);
    }

    [Fact]
    public async Task Cli_requires_requested_cycle_run_id()
    {
        var result = await RunAsync(ArgsWithout("--requested-cycle-run-id"));

        Assert.Equal(ManualPaperCycleCliStatus.RejectedInvalidArguments, result.CliStatus);
        Assert.Equal(ManualPaperCycleCliRejectedReason.MissingRequiredArgument, result.RejectedReason);
    }

    [Fact]
    public async Task Cli_accepts_explicit_synthetic_pms_fixture_path()
    {
        var result = await RunAsync(ArgsWithPmsSyntheticFixturePath());

        Assert.Equal(ManualPaperCycleCliStatus.CompletedNoExternal, result.CliStatus);
        Assert.Equal("fixtures/pms-paper/synthetic-pms-weights-v0.txt", result.ParsedRequest?.Request.QubesFixturePath);
        Assert.True(result.NoExternal);
        Assert.False(result.CreatedOrder);
        Assert.False(result.CreatedRoute);
        Assert.False(result.SubmittedOrder);
    }

    [Theory]
    [InlineData("--allow-synthetic-pms-fixture")]
    [InlineData("--allow-not-qubes-economic-output-fixture")]
    [InlineData("--no-order")]
    [InlineData("--no-route")]
    [InlineData("--no-fill")]
    [InlineData("--no-broker")]
    [InlineData("--no-fix")]
    [InlineData("--no-executable-schedule")]
    [InlineData("--no-live-state-mutation")]
    [InlineData("--no-ledger-commit")]
    public async Task Cli_requires_explicit_r007_safety_flags(string option)
    {
        var result = await RunAsync(ArgsWithout(option));

        Assert.Equal(ManualPaperCycleCliStatus.RejectedUnsafeMode, result.CliStatus);
        Assert.Equal(ManualPaperCycleCliRejectedReason.MissingExplicitSafetyFlag, result.RejectedReason);
    }

    [Fact]
    public async Task Cli_rejects_false_explicit_r007_safety_flag()
    {
        var result = await RunAsync(Args("--no-order", "false"));

        Assert.Equal(ManualPaperCycleCliStatus.RejectedUnsafeMode, result.CliStatus);
        Assert.Equal(ManualPaperCycleCliRejectedReason.MissingExplicitSafetyFlag, result.RejectedReason);
    }

    [Fact]
    public async Task Cli_requires_prior_paper_ledger_baseline()
    {
        var services = CreateServices();
        var context = CreateContext(UnsafeBaselineReference(CreateArchive()));

        var result = await services.Surface.RunAsync(ValidArgs(), context, CancellationToken.None);

        Assert.Equal(ManualPaperCycleCliStatus.RejectedPreflightFailed, result.CliStatus);
        Assert.Equal(PaperCyclePreflightStatus.HeldMissingPriorBaseline, result.PreflightResult?.PreflightStatus);
    }

    [Fact]
    public async Task Cli_requires_prior_paper_continuity_ready_no_external()
    {
        var services = CreateServices();
        var context = CreateContext(CreateBaselineReference(CreateArchive()), CreateContinuityGate(PaperCycleContinuityStatus.HeldForReview));

        var result = await services.Surface.RunAsync(ValidArgs(), context, CancellationToken.None);

        Assert.Equal(ManualPaperCycleCliStatus.RejectedPreflightFailed, result.CliStatus);
        Assert.Equal(PaperCyclePreflightStatus.HeldMissingContinuityGate, result.PreflightResult?.PreflightStatus);
    }

    [Fact]
    public async Task Cli_requires_fifteen_minute_cadence()
    {
        var result = await RunAsync(Args("--expected-cadence-minutes", "10"));

        Assert.Equal(ManualPaperCycleCliStatus.RejectedUnsafeMode, result.CliStatus);
        Assert.Equal(ManualPaperCycleCliRejectedReason.InvalidCadence, result.RejectedReason);
    }

    [Fact]
    public async Task Cli_rejects_scheduler_service_or_polling_modes()
    {
        var scheduler = await RunAsync([.. ValidArgs(), "--scheduler", "true"]);
        var service = await RunAsync([.. ValidArgs(), "--service", "true"]);
        var polling = await RunAsync([.. ValidArgs(), "--polling", "true"]);

        Assert.Equal(ManualPaperCycleCliRejectedReason.UnsafeSchedulerServicePolling, scheduler.RejectedReason);
        Assert.Equal(ManualPaperCycleCliRejectedReason.UnsafeSchedulerServicePolling, service.RejectedReason);
        Assert.Equal(ManualPaperCycleCliRejectedReason.UnsafeSchedulerServicePolling, polling.RejectedReason);
        Assert.False(scheduler.CycleExecuted);
        Assert.False(service.CycleExecuted);
        Assert.False(polling.CycleExecuted);
    }

    [Fact]
    public async Task Cli_rejects_live_broker_or_live_input_mode()
    {
        var liveBroker = await RunAsync([.. ValidArgs(), "--live-broker", "true"]);
        var liveInput = await RunAsync([.. ValidArgs(), "--live-market-input", "true"]);

        Assert.Equal(ManualPaperCycleCliRejectedReason.UnsafeLiveBoundary, liveBroker.RejectedReason);
        Assert.Equal(ManualPaperCycleCliRejectedReason.UnsafeLiveBoundary, liveInput.RejectedReason);
        Assert.False(liveBroker.CycleExecuted);
        Assert.False(liveInput.CycleExecuted);
    }

    [Fact]
    public async Task Cli_rejects_order_or_trading_mode()
    {
        var trading = await RunAsync([.. ValidArgs(), "--trading", "true"]);
        var orders = await RunAsync([.. ValidArgs(), "--orders", "true"]);

        Assert.Equal(ManualPaperCycleCliRejectedReason.UnsafeOrderOrTradingMode, trading.RejectedReason);
        Assert.Equal(ManualPaperCycleCliRejectedReason.UnsafeOrderOrTradingMode, orders.RejectedReason);
    }

    [Fact]
    public async Task Cli_runs_exactly_one_cycle_for_valid_request()
    {
        var result = await RunAsync();

        Assert.True(result.CycleExecuted);
        Assert.Equal(1, result.CycleExecutionCount);
        Assert.False(result.MoreThanOneCycleAllowed);
        Assert.False(result.CycleRunResult?.MultipleCyclesRun);
    }

    [Fact]
    public async Task Cli_does_not_run_more_than_one_cycle_for_duplicate_request()
    {
        var services = CreateServices();
        var context = CreateContext();

        var first = await services.Surface.RunAsync(ValidArgs(), context, CancellationToken.None);
        var second = await services.Surface.RunAsync(ValidArgs(), context, CancellationToken.None);

        Assert.Equal(ManualPaperCycleCliStatus.CompletedNoExternal, first.CliStatus);
        Assert.Equal(ManualPaperCycleCliStatus.DuplicateReturned, second.CliStatus);
        Assert.False(second.CycleExecuted);
        Assert.Equal(1, second.CycleExecutionCount);
    }

    [Fact]
    public async Task Cli_does_not_commit_paper_ledger_state()
    {
        var result = await RunAsync();

        Assert.False(result.PaperLedgerCommitted);
        Assert.False(result.CycleRunResult?.CycleResult.MutatedPaperLedgerState);
    }

    [Fact]
    public async Task Cli_emits_preflight_result()
    {
        var result = await RunAsync();

        Assert.NotNull(result.PreflightResult);
        Assert.Equal(PaperCyclePreflightStatus.ReadyNoExternal, result.PreflightResult?.PreflightStatus);
    }

    [Fact]
    public async Task Cli_emits_cycle_result()
    {
        var result = await RunAsync();

        Assert.NotNull(result.CycleRunResult);
        Assert.Equal(ManualPaperCycleRunStatus.CompletedNoExternalFixture, result.CycleRunResult?.RunStatus);
    }

    [Fact]
    public async Task Cli_emits_target_vs_current_diff()
    {
        var result = await RunAsync();

        Assert.Contains(result.CycleRunResult!.CycleResult.TargetVsCurrentDiffLines, x => x.Symbol == "AUDUSD" && x.DeltaNotional == 17690m);
        Assert.Contains(result.CycleRunResult.CycleResult.TargetVsCurrentDiffLines, x => x.Symbol == "EURUSD" && x.DeltaNotional == 12236m);
        Assert.Contains(result.CycleRunResult.CycleResult.TargetVsCurrentDiffLines, x => x.Symbol == "GBPUSD" && x.DeltaNotional == 213616m);
    }

    [Fact]
    public async Task Cli_emits_non_executable_rebalance_intents()
    {
        var result = await RunAsync();

        Assert.True(result.CycleRunResult?.RebalanceIntentsRemainNonExecutable);
        Assert.All(result.CycleRunResult!.CycleResult.RebalanceIntents, intent => Assert.False(intent.IsExecutable));
    }

    [Fact]
    public async Task Cli_preserves_idempotency_for_duplicate_requested_cycle_run_id()
    {
        var services = CreateServices();
        var context = CreateContext();

        await services.Surface.RunAsync(ValidArgs(), context, CancellationToken.None);
        var duplicate = await services.Surface.RunAsync(ValidArgs(), context, CancellationToken.None);

        Assert.Equal(ManualPaperCycleCliStatus.DuplicateReturned, duplicate.CliStatus);
        Assert.Equal(ManualPaperCycleRunStatus.DuplicateReturned, duplicate.CycleRunResult?.RunStatus);
    }

    [Fact]
    public async Task Cli_does_not_create_orders_fills_reports_routes_or_submissions()
    {
        var result = await RunAsync();

        Assert.False(result.CreatedOrder);
        Assert.False(result.CreatedFill);
        Assert.False(result.CreatedExecutionReport);
        Assert.False(result.CreatedRoute);
        Assert.False(result.SubmittedOrder);
    }

    [Fact]
    public void Cli_surface_introduces_no_runtime_boundary_primitives()
    {
        var source = File.ReadAllText(CliSurfaceSourcePath());
        var program = File.ReadAllText(CliProgramSourcePath());

        foreach (var text in new[] { source, program })
        {
            Assert.DoesNotContain("TcpClient", text, StringComparison.Ordinal);
            Assert.DoesNotContain("SslStream", text, StringComparison.Ordinal);
            Assert.DoesNotContain("MarketDataRequest", text, StringComparison.Ordinal);
            Assert.DoesNotContain("MarketDataResponse", text, StringComparison.Ordinal);
            Assert.DoesNotContain("FixSession", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ConnectAsync", text, StringComparison.Ordinal);
            Assert.DoesNotContain("SendOrderAsync", text, StringComparison.Ordinal);
            Assert.DoesNotContain("SubmitOrder", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Api_and_worker_live_gateway_remain_disabled()
    {
        var apiSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Api/appsettings.json"))).RootElement;
        var workerSettings = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Worker/appsettings.json"))).RootElement;

        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(apiSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(apiSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowExternalConnections").GetBoolean());
        Assert.False(workerSettings.GetProperty("Safety").GetProperty("AllowLiveTrading").GetBoolean());
        Assert.True(workerSettings.GetProperty("Safety").GetProperty("RequireFakeExecutionGateway").GetBoolean());
    }

    [Fact]
    public void Cli_surface_introduces_no_scheduler_timer_service_or_background_job()
    {
        var source = File.ReadAllText(CliSurfaceSourcePath());
        var program = File.ReadAllText(CliProgramSourcePath());

        foreach (var text in new[] { source, program })
        {
            Assert.DoesNotContain("IHostedService", text, StringComparison.Ordinal);
            Assert.DoesNotContain("BackgroundService", text, StringComparison.Ordinal);
            Assert.DoesNotContain("PeriodicTimer", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Task.Delay", text, StringComparison.Ordinal);
            Assert.DoesNotContain("System.Threading.Timer", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Manual_no_external_tool_emits_line_level_paper_plan_artifacts()
    {
        var root = RepoRoot();
        var runId = $"cycle-r006-line-artifact-{Guid.NewGuid():N}";
        var qubesRunId = $"qubes-r006-line-artifact-{Guid.NewGuid():N}";
        var output = Path.Combine(Path.GetTempPath(), $"manual-paper-cycle-r006-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);
        var fixture = Path.Combine(output, "qubes-fixture.txt");
        await File.WriteAllLinesAsync(
            fixture,
            ["AUDUSD Curncy;0.150000", "EURUSD Curncy;0.150000", "GBPUSD Curncy;-0.260000"]);

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }.AddArguments(
                "run",
                "--no-build",
                "--no-restore",
                "--project",
                "tools/QQ.Production.Intraday.Tools.ManualPaperCycle/QQ.Production.Intraday.Tools.ManualPaperCycle.csproj",
                "--",
                "--mode",
                "ManualNoExternal",
                "--requested-cycle-run-id",
                runId,
                "--qubes-run-id",
                qubesRunId,
                "--qubes-fixture-path",
                fixture,
                "--prior-paper-ledger-state-id",
                "paper-ledger-commit-r025-sample:paper-ledger-state",
                "--prior-continuity-gate-id",
                "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate",
                "--requested-by",
                "operator-sanitized",
                "--expected-cadence-minutes",
                "15",
                "--output-artifacts-dir",
                output,
                "--allow-synthetic-pms-fixture",
                "true",
                "--allow-not-qubes-economic-output-fixture",
                "true",
                "--no-order",
                "true",
                "--no-route",
                "true",
                "--no-fill",
                "true",
                "--no-broker",
                "true",
                "--no-fix",
                "true",
                "--no-executable-schedule",
                "true",
                "--no-live-state-mutation",
                "true",
                "--no-ledger-commit",
                "true",
                "--no-paper-ledger-commit",
                "true")) ?? throw new InvalidOperationException("Manual paper cycle process could not start.");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(process.ExitCode == 0, $"stdout={stdout}; stderr={stderr}");
            var planPath = Path.Combine(output, "phase-pms-ems-oms-manual-noexternal-paper-execution-plan.json");
            var linesPath = Path.Combine(output, "phase-pms-ems-oms-manual-noexternal-paper-execution-plan-lines.json");
            Assert.True(File.Exists(planPath));
            Assert.True(File.Exists(linesPath));

            using var plan = JsonDocument.Parse(await File.ReadAllTextAsync(planPath));
            Assert.Equal(runId, plan.RootElement.GetProperty("CycleRunId").GetString());
            Assert.True(plan.RootElement.GetProperty("NonExecutable").GetBoolean());
            Assert.True(plan.RootElement.GetProperty("NotAnOrder").GetBoolean());
            Assert.True(plan.RootElement.GetProperty("NoBrokerRoute").GetBoolean());
            Assert.True(plan.RootElement.GetProperty("NoFixMessage").GetBoolean());
            Assert.False(plan.RootElement.GetProperty("ExecutionAllowed").GetBoolean());
            Assert.True(plan.RootElement.GetProperty("NoPaperLedgerCommit").GetBoolean());

            using var lines = JsonDocument.Parse(await File.ReadAllTextAsync(linesPath));
            var emitted = lines.RootElement.GetProperty("Lines").EnumerateArray().ToArray();
            Assert.NotEmpty(emitted);
            Assert.All(emitted, line =>
            {
                Assert.Equal(runId, line.GetProperty("CycleRunId").GetString());
                Assert.Equal(qubesRunId, line.GetProperty("QubesRunId").GetString());
                Assert.True(line.GetProperty("NonExecutable").GetBoolean());
                Assert.False(line.GetProperty("ExecutionAllowed").GetBoolean());
                Assert.True(line.GetProperty("NotAnOrder").GetBoolean());
                Assert.True(line.GetProperty("NotSubmitted").GetBoolean());
                Assert.True(line.GetProperty("NoBrokerRoute").GetBoolean());
                Assert.True(line.GetProperty("NoFixMessage").GetBoolean());
                Assert.True(line.GetProperty("NoChildSlices").GetBoolean());
                Assert.True(line.GetProperty("NoExecutableSchedule").GetBoolean());
                Assert.True(line.GetProperty("NoFill").GetBoolean());
                Assert.True(line.GetProperty("NoExecutionReport").GetBoolean());
                Assert.True(line.GetProperty("NoRoute").GetBoolean());
                Assert.True(line.GetProperty("NoSubmission").GetBoolean());
                Assert.True(line.GetProperty("NoPaperLedgerCommit").GetBoolean());
                Assert.Contains("MissingCanonicalTargetClose", line.GetProperty("MissingEvidence").EnumerateArray().Select(x => x.GetString()));
            });
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Manual_no_external_tool_accepts_synthetic_pms_fixture_adapter()
    {
        var root = RepoRoot();
        var runId = $"pms-paper-r008-synthetic-adapter-{Guid.NewGuid():N}";
        var output = Path.Combine(Path.GetTempPath(), $"manual-paper-cycle-r008-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);
        var fixture = Path.Combine(output, "synthetic-pms-weights-v0.txt");
        await File.WriteAllTextAsync(fixture, "EURUSD;0.10000000\r\n");

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }.AddArguments(
                "run",
                "--no-build",
                "--no-restore",
                "--project",
                "tools/QQ.Production.Intraday.Tools.ManualPaperCycle/QQ.Production.Intraday.Tools.ManualPaperCycle.csproj",
                "--",
                "--mode",
                "ManualNoExternal",
                "--requested-cycle-run-id",
                runId,
                "--qubes-run-id",
                "core-synthetic-pms-fixture-v0",
                "--pms-synthetic-fixture-path",
                fixture,
                "--prior-paper-ledger-state-id",
                "paper-ledger-commit-r025-sample:paper-ledger-state",
                "--prior-continuity-gate-id",
                "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate",
                "--requested-by",
                "operator-sanitized",
                "--expected-cadence-minutes",
                "15",
                "--output-artifacts-dir",
                output,
                "--allow-synthetic-pms-fixture",
                "true",
                "--allow-not-qubes-economic-output-fixture",
                "true",
                "--no-order",
                "true",
                "--no-route",
                "true",
                "--no-fill",
                "true",
                "--no-broker",
                "true",
                "--no-fix",
                "true",
                "--no-executable-schedule",
                "true",
                "--no-live-state-mutation",
                "true",
                "--no-ledger-commit",
                "true",
                "--no-paper-ledger-commit",
                "true")) ?? throw new InvalidOperationException("Manual paper cycle process could not start.");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(process.ExitCode == 0, $"stdout={stdout}; stderr={stderr}");
            var runOutputPath = Path.Combine(output, "phase-pms-ems-oms-r031-cli-manual-run-output.json");
            var linesPath = Path.Combine(output, "phase-pms-ems-oms-manual-noexternal-paper-execution-plan-lines.json");
            Assert.True(File.Exists(runOutputPath));
            Assert.True(File.Exists(linesPath));

            using var runOutput = JsonDocument.Parse(await File.ReadAllTextAsync(runOutputPath));
            Assert.Equal((int)ManualPaperCycleCliStatus.CompletedNoExternal, runOutput.RootElement.GetProperty("CliStatus").GetInt32());
            Assert.True(runOutput.RootElement.GetProperty("noOrder").GetBoolean());
            Assert.True(runOutput.RootElement.GetProperty("noFill").GetBoolean());
            Assert.True(runOutput.RootElement.GetProperty("noRoute").GetBoolean());
            Assert.True(runOutput.RootElement.GetProperty("noSubmission").GetBoolean());

            using var lines = JsonDocument.Parse(await File.ReadAllTextAsync(linesPath));
            var emitted = lines.RootElement.GetProperty("Lines").EnumerateArray().ToArray();
            Assert.NotEmpty(emitted);
            Assert.All(emitted, line =>
            {
                var currentWeight = line.GetProperty("CurrentWeight").GetDecimal();
                var targetWeight = line.GetProperty("TargetWeight").GetDecimal();
                var deltaWeight = line.GetProperty("DeltaWeight").GetDecimal();
                var currentNotional = line.GetProperty("CurrentNotional").GetDecimal();
                var targetNotional = line.GetProperty("TargetNotional").GetDecimal();
                var deltaNotional = line.GetProperty("DeltaNotional").GetDecimal();

                Assert.Equal(targetWeight - currentWeight, deltaWeight);
                Assert.Equal(targetNotional - currentNotional, deltaNotional);
                Assert.True(line.GetProperty("ArithmeticValid").GetBoolean());
                Assert.True(line.GetProperty("NonExecutable").GetBoolean());
                Assert.False(line.GetProperty("ExecutionAllowed").GetBoolean());
                Assert.True(line.GetProperty("NotAnOrder").GetBoolean());
                Assert.True(line.GetProperty("NotSubmitted").GetBoolean());
                Assert.True(line.GetProperty("NoBrokerRoute").GetBoolean());
                Assert.True(line.GetProperty("NoFixMessage").GetBoolean());
                Assert.True(line.GetProperty("NoExecutableSchedule").GetBoolean());
                Assert.True(line.GetProperty("NoFill").GetBoolean());
                Assert.True(line.GetProperty("NoRoute").GetBoolean());
                Assert.True(line.GetProperty("NoSubmission").GetBoolean());
            });

            var eurusd = emitted.Single(line => line.GetProperty("Symbol").GetString() == "EURUSD");
            var audusd = emitted.Single(line => line.GetProperty("Symbol").GetString() == "AUDUSD");
            var gbpusd = emitted.Single(line => line.GetProperty("Symbol").GetString() == "GBPUSD");
            Assert.Equal("SyntheticTarget", eurusd.GetProperty("PreviewLineReason").GetString());
            Assert.Equal("PriorBaselineZeroTarget", audusd.GetProperty("PreviewLineReason").GetString());
            Assert.Equal("PriorBaselineZeroTarget", gbpusd.GetProperty("PreviewLineReason").GetString());
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact]
    public void Audusd_is_not_misclassified_as_failed()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var audusd = universe.Single(x => x.InternalInstrumentKey == "AUDUSD");

        Assert.Equal(ApprovedInstrumentValidationStatus.PausedTlsBoundaryInconclusiveNotFailed, audusd.ValidationStatus);
    }

    [Fact]
    public void Usdjpy_caveat_remains_preserved()
    {
        var universe = PmsEmsOmsR001ApprovedUniverse.Create(InstrumentId.New(), InstrumentId.New(), InstrumentId.New(), InstrumentId.New());
        var usdjpy = universe.Single(x => x.InternalInstrumentKey == "USDJPY");

        Assert.Equal("4004", usdjpy.SecurityId);
        Assert.Equal("8", usdjpy.SecurityIdSource);
        Assert.Equal(ApprovedInstrumentValidationStatus.NotProvenNotFailed, usdjpy.ValidationStatus);
    }

    private static async Task<ManualPaperCycleCliResult> RunAsync(string[]? args = null)
    {
        var services = CreateServices();
        return await services.Surface.RunAsync(args ?? ValidArgs(), CreateContext(), CancellationToken.None);
    }

    private static string[] ValidArgs()
        =>
        [
            "--mode", "ManualNoExternal",
            "--requested-cycle-run-id", "cycle-r031-cli-manual-paper-fixture",
            "--qubes-run-id", "qubes-r031-cli-fixture",
            "--qubes-fixture-path", "fixtures/qubes-fx/r031-cli-manual-cycle-fixture.csv",
            "--prior-paper-ledger-state-id", "paper-ledger-commit-r025-sample:paper-ledger-state",
            "--prior-continuity-gate-id", "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate",
            "--requested-by", "operator-sanitized",
            "--expected-cadence-minutes", "15",
            "--output-artifacts-dir", "artifacts/readiness/pms-ems-oms-integration",
            "--allow-synthetic-pms-fixture", "true",
            "--allow-not-qubes-economic-output-fixture", "true",
            "--no-order", "true",
            "--no-route", "true",
            "--no-fill", "true",
            "--no-broker", "true",
            "--no-fix", "true",
            "--no-executable-schedule", "true",
            "--no-live-state-mutation", "true",
            "--no-ledger-commit", "true",
            "--no-paper-ledger-commit", "true"
        ];

    private static string[] Args(string option, string value)
    {
        var args = ValidArgs();
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == option)
            {
                args[index + 1] = value;
                return args;
            }
        }

        return [.. args, option, value];
    }

    private static string[] ArgsWithPmsSyntheticFixturePath()
    {
        var args = ValidArgs().ToList();
        var index = args.IndexOf("--qubes-fixture-path");
        args[index] = "--pms-synthetic-fixture-path";
        args[index + 1] = "fixtures/pms-paper/synthetic-pms-weights-v0.txt";
        return args.ToArray();
    }

    private static string[] ArgsWithout(string option)
    {
        var args = ValidArgs().ToList();
        var index = args.IndexOf(option);
        if (index >= 0)
        {
            args.RemoveAt(index);
            if (index < args.Count && !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                args.RemoveAt(index);
            }
        }

        return args.ToArray();
    }

    private static TestServices CreateServices()
    {
        var state = SeedData.Create(ProducedAt);
        EnsureInstruments(state, ["AUDUSD", "EURUSD", "GBPUSD"]);
        var idsBySymbol = state.Instruments
            .Where(x => x.Symbol is "AUDUSD" or "EURUSD" or "GBPUSD")
            .ToDictionary(x => x.Symbol, x => x.Id, StringComparer.OrdinalIgnoreCase);
        var clock = new FixedClock(ProducedAt);
        var intradayRepository = new InMemoryIntradayRepository(state);
        var batchRepository = new InMemoryModelWeightBatchRepository(state);
        var integrity = new ReferenceDataIntegrityService(intradayRepository, clock);
        var cycleService = new PaperBaselineSecondCycleService(
            new FakeModelWeightGenerator(batchRepository, clock),
            new ModelWeightPromotionService(batchRepository, intradayRepository, integrity, clock),
            new QubesWeightPersistenceService(new InMemoryQubesWeightAuditRepository(), clock),
            new InMemoryPaperBaselineSecondCycleRepository());
        var runner = new ManualPaperCycleFixtureRunner(
            new ManualPaperCycleRunnerContractService(new InMemoryManualPaperCycleRunnerContractRepository(), clock),
            cycleService,
            new InMemoryManualPaperCycleRunResultRepository());
        var surface = new ManualPaperCycleCliSurface(
            new ManualPaperCycleRunnerContractService(new InMemoryManualPaperCycleRunnerContractRepository(), clock),
            runner);

        return new TestServices(surface, idsBySymbol);
    }

    private static ManualPaperCycleCliExecutionContext CreateContext()
        => CreateContext(CreateBaselineReference(CreateArchive()), CreateContinuityGate());

    private static ManualPaperCycleCliExecutionContext CreateContext(
        PaperNextCycleBaselineReference baselineReference,
        PaperCycleContinuityDecisionGate? continuityGate = null)
    {
        var services = CreateServices();
        var archive = CreateArchive();
        return new ManualPaperCycleCliExecutionContext(
            continuityGate ?? CreateContinuityGate(),
            baselineReference,
            archive,
            ProducedAt,
            EffectiveAt,
            ProducedAt,
            EffectiveAt,
            "QQ_MASTER",
            "IntradayFxModel",
            PortfolioNotional,
            ["AUDUSD Curncy;0.150000", "EURUSD Curncy;0.150000", "GBPUSD Curncy;-0.260000"],
            services.InstrumentIdsBySymbol,
            0.0000000001m,
            1m,
            1m,
            TimeSpan.FromMinutes(30),
            1);
    }

    private static void EnsureInstruments(PlatformState state, IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (state.Instruments.Any(x => x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            state.Instruments.Add(new Instrument(
                InstrumentId.New(),
                symbol,
                AssetClass.FxSpot,
                new Currency(symbol[..3]),
                Currency.Usd,
                5,
                1));
        }
    }

    private static PaperCycleContinuityDecisionGate CreateContinuityGate(
        PaperCycleContinuityStatus status = PaperCycleContinuityStatus.PaperContinuityReadyNoExternal)
        => new(
            "cycle-r026-second-paper-baseline:paper-continuity-archive:paper-continuity-gate",
            new SecondCyclePaperContinuityArchiveId("cycle-r026-second-paper-baseline:paper-continuity-archive"),
            status,
            FutureManualPaperCyclesMayUseLatestPaperLedgerFixtureBaseline: status is PaperCycleContinuityStatus.PaperContinuityReadyNoExternal,
            StartsSchedulerOrService: false,
            RunsAnotherCycle: false,
            IngestsNewQubesBatch: false,
            MutatesPaperLedgerState: false,
            MutatesLivePositionState: false,
            MutatesBrokerPositionState: false,
            MutatesProductionLedgerState: false,
            MutatesTradingState: false,
            CreatesOrderCandidates: false,
            CreatesExecutionPlans: false,
            CreatesOrders: false,
            CreatesFills: false,
            CreatesExecutionReports: false,
            CreatesRoutes: false,
            SubmitsOrders: false,
            NoExternal: true,
            PreservesNonExecutableRebalanceIntents: true);

    private static PaperLedgerStateArchiveRecord CreateArchive()
    {
        var stateArchiveId = new PaperLedgerStateArchiveId("paper-ledger-commit-r025-sample:paper-ledger-state:archive");
        var stateId = new PaperLedgerStateId("paper-ledger-commit-r025-sample:paper-ledger-state");
        var commitId = new PaperLedgerCommitId("paper-ledger-commit-r025-sample");
        var previewId = new PaperPositionLedgerPreviewId("paper-ledger-preview-r025-sample");
        var decisionId = new OperatorDecisionId("decision-r025-approve-ledger-commit-readiness");
        var lines = new[]
        {
            ArchiveLine(stateId, commitId, previewId, decisionId, "AUDUSD", "AUD", 131000m),
            ArchiveLine(stateId, commitId, previewId, decisionId, "EURUSD", "EUR", 124000m),
            ArchiveLine(stateId, commitId, previewId, decisionId, "GBPUSD", "GBP", -368000m)
        };

        return new PaperLedgerStateArchiveRecord(
            stateArchiveId,
            stateId,
            commitId,
            previewId,
            new PaperPositionPreviewId("paper-position-preview-r025-sample"),
            new PaperSimulationResultId("paper-simulation-result-r025-sample"),
            new PaperSimulationPlanId("paper-simulation-plan-r025-sample"),
            new PaperExecutionPlanId("paper-execution-plan-r025-sample"),
            "cycle-r025-sample",
            "qubes-r025-sample",
            decisionId,
            ProducedAt,
            PaperLedgerCommitStatus.PaperLedgerCommittedNoExternal,
            "PaperLedgerFixtureStateOnly",
            3,
            10,
            PaperLedgerStateArchiveStatus.ArchivedPaperFixtureState,
            lines,
            Array.Empty<BlockedPaperReviewLineRecord>(),
            QubesLineagePreserved: true,
            CycleLineagePreserved: true,
            OperatorDecisionLineagePreserved: true,
            LedgerCommitLineagePreserved: true,
            LedgerPreviewLineagePreserved: true,
            PositionPreviewLineagePreserved: true,
            SimulationResultLineagePreserved: true,
            SimulationPlanLineagePreserved: true,
            PaperExecutionPlanLineagePreserved: true,
            PaperCandidateLineagePreserved: true,
            RiskLineagePreserved: true,
            RebalanceIntentLineagePreserved: true,
            LotSizingLineagePreserved: true,
            MissingStaleMarkWarningsPreserved: true,
            DriftAcknowledgementPreserved: true,
            PaperOnly: true,
            NoExternal: true,
            FixtureState: true,
            NotProductionLedger: true,
            NotBrokerPosition: true,
            NotTradingState: true,
            NoProductionLedgerMutation: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NotSubmitted: true,
            PaperLedgerMutatedAgain: false,
            NewCycleRan: false,
            NewQubesBatchIngested: false,
            LivePositionStateMutated: false,
            BrokerPositionStateMutated: false,
            ProductionLedgerStateMutated: false,
            TradingStateMutated: false,
            FillCreated: false,
            ExecutionReportCreated: false,
            OmsOrderCreated: false,
            ParentOrderCreated: false,
            ChildOrderCreated: false,
            BrokerOrderCreated: false,
            OrderStateCreated: false,
            SubmittedOrders: false,
            BrokerRouteCreated: false);
    }

    private static PaperLedgerStateLineArchiveRecord ArchiveLine(
        PaperLedgerStateId stateId,
        PaperLedgerCommitId commitId,
        PaperPositionLedgerPreviewId previewId,
        OperatorDecisionId decisionId,
        string symbol,
        string currency,
        decimal quantity)
        => new(
            new PaperLedgerStateLineId($"{stateId.Value}:{symbol}:line"),
            stateId,
            commitId,
            previewId,
            "cycle-r025-sample",
            "qubes-r025-sample",
            decisionId,
            InstrumentId.New(),
            symbol,
            currency,
            quantity,
            "PaperLedgerFixtureStateOnly",
            $"paperLedgerStateLine={symbol}",
            PaperOnly: true,
            NoExternal: true,
            FixtureState: true,
            NotProductionLedger: true,
            NotBrokerPosition: true,
            NotTradingState: true,
            NoLivePositionMutation: true,
            NoBrokerPositionMutation: true,
            NoProductionLedgerMutation: true,
            NoTradingStateMutation: true,
            NoFillCreated: true,
            NoExecutionReportCreated: true,
            NoOrderCreated: true,
            NoBrokerRoute: true,
            NotSubmitted: true);

    private static PaperNextCycleBaselineReference CreateBaselineReference(PaperLedgerStateArchiveRecord archive)
        => new(
            "paper-next-cycle-baseline-r025",
            archive.PaperLedgerStateArchiveId,
            archive.PaperLedgerStateId,
            archive.PaperLedgerCommitId,
            archive.CycleRunId,
            archive.QubesRunId,
            "PaperLedgerFixture",
            "R024 committed paper ledger state",
            BaselineIsProduction: false,
            BaselineIsBroker: false,
            BaselineIsLiveTrading: false,
            PaperOnly: true,
            NoExternal: true,
            FixtureState: true,
            NewCycleRan: false,
            NewQubesBatchIngested: false,
            PaperLedgerMutatedAgain: false);

    private static PaperNextCycleBaselineReference UnsafeBaselineReference(PaperLedgerStateArchiveRecord archive)
        => CreateBaselineReference(archive) with { PaperOnly = false };

    private static string CliSurfaceSourcePath()
        => Path.Combine(RepoRoot(), "src/QQ.Production.Intraday.Application/ManualPaperCycleCliSurface.cs");

    private static string CliProgramSourcePath()
        => Path.Combine(RepoRoot(), "tools/QQ.Production.Intraday.Tools.ManualPaperCycle/Program.cs");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "QQ.Production.Intraday.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed record TestServices(
        ManualPaperCycleCliSurface Surface,
        IReadOnlyDictionary<string, InstrumentId> InstrumentIdsBySymbol);
}

file static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo AddArguments(this ProcessStartInfo startInfo, params string[] arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
