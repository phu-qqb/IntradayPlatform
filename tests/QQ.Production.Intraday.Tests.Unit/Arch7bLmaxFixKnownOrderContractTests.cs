using System.Security.Cryptography;
using System.Text;
using QQ.Production.Intraday.Application;
using QQ.Production.Intraday.Lmax.ConnectivityLab;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bLmaxFixKnownOrderContractTests
{
    [Fact]
    public void OrderCancelRequest_MatchesHistoricalR215Contract()
    {
        var message = LmaxFixRecoveryCodec.BuildOrderCancelRequest(
            "SENDER",
            "LMXBD",
            3,
            "A7BC0123456789ABCDEF",
            "A7BO0123456789ABCDEF",
            "GBPUSD",
            "1",
            0.1m,
            "4002",
            "8");

        Assert.Equal("F", LmaxFixMarketDataCodec.GetTag(message, "35"));
        Assert.Equal("A7BC0123456789ABCDEF", LmaxFixMarketDataCodec.GetTag(message, "11"));
        Assert.Equal("A7BO0123456789ABCDEF", LmaxFixMarketDataCodec.GetTag(message, "41"));
        Assert.Equal("GBPUSD", LmaxFixMarketDataCodec.GetTag(message, "55"));
        Assert.Equal("1", LmaxFixMarketDataCodec.GetTag(message, "54"));
        Assert.Equal("0.1", LmaxFixMarketDataCodec.GetTag(message, "38"));
        Assert.Equal("4002", LmaxFixMarketDataCodec.GetTag(message, "48"));
        Assert.Equal("8", LmaxFixMarketDataCodec.GetTag(message, "22"));
    }

    [Fact]
    public void ExecutionReport_PreservesSequencePossDupAndRawSha256()
    {
        var message = LmaxFixMarketDataCodec.BuildMessage("8", 42, "LMXBD", "SENDER",
        [
            ("43", "Y"),
            ("17", "EXEC-1"),
            ("37", "ORDER-1"),
            ("11", "A7BO0123456789ABCDEF"),
            ("150", "F"),
            ("39", "2"),
            ("48", "4002"),
            ("22", "8"),
            ("55", "GBPUSD"),
            ("54", "1"),
            ("38", "0.1"),
            ("151", "0"),
            ("14", "0.1"),
            ("32", "0.1"),
            ("31", "1.25000"),
            ("6", "1.25000"),
            ("44", "1.25000"),
            ("59", "0"),
            ("40", "2"),
            ("60", "20260723-01:00:00.000"),
            ("1", Arch7bKnownOrderQualificationPolicy.DemoAccountId)
        ]);

        var report = LmaxFixRecoveryCodec.NormalizeExecutionReport(message, null).Report;

        Assert.Equal(42, report.FixSequenceNumber);
        Assert.True(report.PossDup);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(message))).ToLowerInvariant(),
            report.RawMessageSha256);
    }

    [Fact]
    public async Task RawClient_IsDisabledByDefaultWithoutOpeningSocket()
    {
        var client = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());

        var result = await client.Arch7bKnownOrderLifecycleAsync(
            LiveLikeOptions(),
            LmaxFixArch7bKnownOrderRequest.Disabled(),
            CancellationToken.None);

        Assert.Equal("Skipped", result.Status);
        Assert.Equal("ARCH7B_EXECUTION_DISABLED_BY_DEFAULT", result.Blocker);
        Assert.False(result.Connected);
        Assert.False(result.OpeningSent);
        Assert.False(result.FlattenSent);
    }

    [Fact]
    public async Task RawClient_DryRunBuildsBoundedDayLifecycleWithoutNetwork()
    {
        var client = new RawLmaxFixSessionClient(new LmaxConnectivityLabSafetyValidator());
        var request = Request(LmaxFixArch7bActivation.DryRun);

        var result = await client.Arch7bKnownOrderLifecycleAsync(
            LiveLikeOptions(),
            request,
            CancellationToken.None);

        Assert.Equal("Ok", result.Status);
        Assert.False(result.Connected);
        Assert.Contains(result.Diagnostics, value => value.Contains("35=D", StringComparison.Ordinal) && value.Contains("59=0", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, value => value.Contains("35=F", StringComparison.Ordinal) && value.Contains("41=A7BO0123456789ABCDEF", StringComparison.Ordinal));
        Assert.Equal(0, result.OrderStatusRequestCount);
        Assert.Contains("ARCH7B_DRY_RUN_NO_NETWORK_NO_SEND", result.Diagnostics);
    }

    [Fact]
    public void LiveValidation_RejectsRealAccountAndMissingExactAuthorization()
    {
        var request = Request(LmaxFixArch7bActivation.AuthorizedOnce) with
        {
            AccountId = Arch7bKnownOrderQualificationPolicy.ForbiddenRealAccountId,
            ExactOperatorAuthorizationPresent = false
        };

        var blockers = LmaxFixArch7bKnownOrderContract.Validate(
            LiveLikeOptions(),
            request,
            DateTimeOffset.UtcNow);

        Assert.Contains("ARCH7B_DEMO_ACCOUNT_IDENTITY_MISMATCH", blockers);
        Assert.Contains("ARCH7B_REAL_ACCOUNT_FORBIDDEN", blockers);
        Assert.Contains("ARCH7B_EXACT_OPERATOR_AUTHORIZATION_MISSING", blockers);
        Assert.Contains("ARCH7B_AUTHORIZATION_PACKET_SHA256_MISMATCH", blockers);
    }

    [Fact]
    public void LiveValidation_BindsExactLmaxBboEconomicsIntoAuthorizationPacket()
    {
        var request = Request(LmaxFixArch7bActivation.AuthorizedOnce) with
        {
            BboAsk = 1.25001m,
            BboSource = "POLYGON"
        };

        var blockers = LmaxFixArch7bKnownOrderContract.Validate(
            LiveLikeOptions(),
            request,
            DateTimeOffset.UtcNow);

        Assert.Contains("ARCH7B_BBO_SOURCE_NOT_LMAX", blockers);
        Assert.Contains("ARCH7B_AUTHORIZATION_PACKET_SHA256_MISMATCH", blockers);
    }

    [Fact]
    public void Restart_after_open_send_before_ack_queries_known_order_without_resend()
    {
        var plan = LmaxFixArch7bRecoveryPlanner.Build(State(
            openingSent: true,
            openingCum: 0m,
            openingLeaves: 0m,
            openingTerminal: false));

        Assert.False(plan.MaySendOpeningNewOrderSingle);
        Assert.True(plan.QueryOpeningKnownOrder);
    }

    [Fact]
    public void Restart_after_partial_fill_and_cancel_send_never_resends_cancel()
    {
        var plan = LmaxFixArch7bRecoveryPlanner.Build(State(
            openingSent: true,
            cancelSent: true,
            openingCum: 0.04m,
            openingLeaves: 0.06m,
            openingTerminal: false));

        Assert.False(plan.MaySendOpeningNewOrderSingle);
        Assert.False(plan.MaySendOpeningResidualCancel);
        Assert.True(plan.QueryOpeningKnownOrder);
    }

    [Fact]
    public void Restart_after_open_fill_before_flatten_allows_only_first_flatten()
    {
        var plan = LmaxFixArch7bRecoveryPlanner.Build(State(
            openingSent: true,
            openingCum: 0.1m,
            openingLeaves: 0m,
            openingTerminal: true));

        Assert.False(plan.MaySendOpeningNewOrderSingle);
        Assert.True(plan.MaySendFlattenNewOrderSingle);
        Assert.False(plan.QueryOpeningKnownOrder);
    }

    [Fact]
    public void Restart_after_flatten_send_queries_known_flatten_without_resend()
    {
        var plan = LmaxFixArch7bRecoveryPlanner.Build(State(
            openingSent: true,
            flattenSent: true,
            openingCum: 0.1m,
            openingLeaves: 0m,
            openingTerminal: true,
            flattenCum: 0m,
            flattenLeaves: 0.1m,
            flattenTerminal: false));

        Assert.False(plan.MaySendFlattenNewOrderSingle);
        Assert.True(plan.QueryFlattenKnownOrder);
    }

    private static LmaxFixArch7bRecoveryState State(
        bool openingSent = false,
        bool cancelSent = false,
        bool flattenSent = false,
        int statusRequestCount = 0,
        decimal openingCum = 0m,
        decimal openingLeaves = 0m,
        bool openingTerminal = false,
        decimal flattenCum = 0m,
        decimal flattenLeaves = 0m,
        bool flattenTerminal = false)
        => new(
            openingSent,
            cancelSent,
            flattenSent,
            statusRequestCount,
            openingCum,
            openingLeaves,
            openingTerminal,
            flattenCum,
            flattenLeaves,
            flattenTerminal);

    private static LmaxFixArch7bKnownOrderRequest Request(LmaxFixArch7bActivation activation)
    {
        var now = DateTimeOffset.UtcNow;
        var request = new LmaxFixArch7bKnownOrderRequest(
            activation,
            Guid.Parse("a7000000-0000-0000-0000-000000000001"),
            Guid.Parse("a7000000-0000-0000-0000-000000000002"),
            "arch7b-test-session",
            "arch7b-test-owner",
            Arch7bKnownOrderQualificationPolicy.DemoAccountId,
            "A7BO0123456789ABCDEF",
            "A7BC0123456789ABCDEF",
            "A7BF0123456789ABCDEF",
            1.25000m,
            1.24980m,
            1.25020m,
            1.24990m,
            1.24990m,
            1.25000m,
            now,
            "LMAX",
            new string('b', 64),
            now,
            now.AddSeconds(30),
            now.AddSeconds(120),
            new string('c', 64),
            new string('a', 64),
            true,
            true,
            true,
            false);
        return request with
        {
            AuthorizationPacketSha256 =
                LmaxFixArch7bKnownOrderContract.ComputeAuthorizationPacketSha256(request)
        };
    }

    private static LmaxConnectivityLabOptions LiveLikeOptions() =>
        new()
        {
            EnvironmentName = "Demo",
            AllowExternalConnections = true,
            AllowOrderSubmission = true,
            AllowLiveTrading = false,
            DryRun = false,
            FixOrderTargetCompId = "LMXBD",
            FixSenderCompId = "SENDER"
        };
}
