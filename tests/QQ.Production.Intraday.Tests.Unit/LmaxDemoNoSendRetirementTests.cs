using System.Text.Json;
using QQ.Production.Intraday.Application;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class LmaxDemoNoSendRetirementTests
{
    [Fact]
    public void ValidRetirementRetainsTheFaultedJournalAndPermitsOneNewOwner()
    {
        using var f = new Fixture();
        var bytes = File.ReadAllBytes(f.Path);
        Assert.Throws<InvalidOperationException>(() => LmaxDemoSessionOwnership.Begin(f.NextStart, f.Now, f.Root));
        f.Retire();
        Assert.Equal(bytes, File.ReadAllBytes(f.Path));
        using var prior = LmaxDemoSessionJournal.OpenForInspection(f.Path);
        Assert.False(LmaxDemoControlledSession.Inspect(prior).IsClosed);
        Assert.Equal("COORDINATOR_RECONCILIATION_REQUIRED", LmaxDemoControlledSession.Inspect(prior).BlockingReason);
        using var next = LmaxDemoSessionOwnership.Begin(f.NextStart, f.Now, f.Root);
        Assert.Equal(f.NextStart.SessionId, next.Session.Start.SessionId);
        Assert.Throws<IOException>(() => LmaxDemoSessionOwnership.Begin(f.NextStart with { SessionId = "third-owner" }, f.Now, f.Root));
    }

    [Theory]
    [InlineData("cycle")]
    [InlineData("send")]
    [InlineData("completed")]
    public void AnyStartedExecutionOrAmbiguousSendPreventsRetirement(string lifecycle)
    {
        using var f = new Fixture(lifecycle);
        Assert.Throws<InvalidOperationException>(() => f.Prepare());
        Assert.False(File.Exists(LmaxDemoNoSendRetirement.CertificatePath(f.Path)));
    }

    [Fact]
    public void OtherFaultCannotUseCoordinatorRetirement()
    {
        using var f = new Fixture(fault: "TRANSPORT_LOST_RECONCILIATION_REQUIRED");
        Assert.Throws<InvalidOperationException>(() => f.Prepare());
    }

    [Fact]
    public void SimulationCannotProduceARealAccountRetirement()
    {
        using var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => LmaxDemoNoSendRetirement.Prepare(f.Path,
            LmaxDemoNoSendRetirement.HashFile(f.Path), Fixture.Approval, f.Observation, f.Database, f.Now));
    }

    [Theory]
    [InlineData("fill")]
    [InlineData("working")]
    [InlineData("position")]
    [InlineData("parent")]
    [InlineData("target")]
    [InlineData("processed")]
    [InlineData("portfolio")]
    public void DatabaseDisagreementPreventsRetirement(string condition)
    {
        using var f = new Fixture();
        var database = condition switch
        {
            "fill" => f.Database with { NewFills = 1 },
            "working" => f.Database with { OpenChildOrders = 1 },
            "position" => f.Database with { NonZeroPositions = 1 },
            "parent" => f.Database with { NewParentOrders = 1 },
            "target" => f.Database with { FailedRunTargets = 1 },
            "processed" => f.Database with { FailedRunProcessed = true },
            _ => f.Database with { FullPortfolioWeightCount = 38 }
        };
        Assert.Throws<InvalidOperationException>(() => f.Prepare(database: database));
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("before_stop")]
    [InlineData("working")]
    [InlineData("not_flat")]
    [InlineData("wrong_account")]
    public void ObservationMustProveThisAccountFlatAfterItsLastJournalEntry(string condition)
    {
        using var f = new Fixture();
        var observation = condition switch
        {
            "stale" => f.Observation with { ObservedAtUtc = f.Now.AddMinutes(-20) },
            "before_stop" => f.Observation with { ObservedAtUtc = f.StartAt },
            "working" => f.Observation with { NoWorkingOrders = false },
            "not_flat" => f.Observation with { Flat = false },
            _ => f.Observation with { AccountId = "different-account" }
        };
        Assert.Throws<InvalidOperationException>(() => f.Prepare(observation: observation));
    }

    [Fact]
    public void ApprovalAndExactJournalHashAreRequired()
    {
        using var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => LmaxDemoNoSendRetirement.Prepare(f.Path,
            new string('0', 64), Fixture.Approval, f.Observation, f.Database, f.Now, true));
        Assert.Throws<InvalidOperationException>(() => LmaxDemoNoSendRetirement.Prepare(f.Path,
            LmaxDemoNoSendRetirement.HashFile(f.Path), "", f.Observation, f.Database, f.Now, true));
    }

    [Fact]
    public void JournalMutationInvalidatesAnOtherwiseValidRetirement()
    {
        using var f = new Fixture();
        f.Retire();
        // JSON whitespace leaves the event-chain values intact but changes the bound file bytes.
        File.WriteAllText(f.Path, " " + File.ReadAllText(f.Path));
        Assert.Throws<InvalidOperationException>(() => LmaxDemoSessionOwnership.Begin(f.NextStart, f.Now, f.Root));
    }

    [Fact]
    public void CertificateCannotBeOverwrittenOrForgedForAnotherJournal()
    {
        using var f = new Fixture();
        var certificate = f.Retire();
        var path = LmaxDemoNoSendRetirement.CertificatePath(f.Path);
        var bytes = File.ReadAllBytes(path);
        using (f.Lease()) Assert.Throws<IOException>(() => LmaxDemoNoSendRetirement.WriteUnderOwnerLease(f.Path, certificate, f.Now, true));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        File.WriteAllText(path, JsonSerializer.Serialize(certificate with { LastEntrySha256 = new string('0', 64) }));
        Assert.Throws<InvalidOperationException>(() => LmaxDemoSessionOwnership.Begin(f.NextStart, f.Now, f.Root));
    }

    private sealed class Fixture : IDisposable
    {
        internal const string Approval = "https://github.com/phu-qqb/IntradayPlatform/issues/84#issuecomment-123456789";
        internal string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "simulated-no-send-retirement-" + Guid.NewGuid().ToString("N"));
        internal DateTimeOffset StartAt { get; } = new(2026, 9, 17, 14, 0, 0, TimeSpan.Zero);
        internal DateTimeOffset Now => StartAt.AddSeconds(4);
        internal string Path { get; }
        internal LmaxDemoSessionStart Start { get; }
        internal LmaxDemoSessionStart NextStart => Start with { SessionId = "next-owner", ObservedAtUtc = Now };
        internal LmaxDemoRetirementObservation Observation => new(Start.SessionId, Start.AccountId, StartAt.AddSeconds(3), true, true, true, "SIMULATED_UI_EVIDENCE");
        internal LmaxDemoRetirementDatabaseEvidence Database => new(Now, "LMAX_DEMO_LOCAL", LmaxDemoNoSendRetirement.FailedModelRunId,
            false, 39, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        internal Fixture(string? lifecycle = null, string fault = "COORDINATOR_RECONCILIATION_REQUIRED")
        {
            Start = new("prior-owner", "1754288005", "Demo", "simulated-owner-approval", StartAt, StartAt.AddHours(4),
                true, true, true, true, [new("EURUSD", "4001", 10000m)], InternalBrokerAccountCode: "LMAX_DEMO_LOCAL");
            Path = System.IO.Path.Combine(Root, Start.SessionId + ".journal.jsonl");
            using var owner = LmaxDemoSessionOwnership.Begin(Start, StartAt, Root);
            owner.Session.RecordInboundControl(1, "A", StartAt);
            if (lifecycle is not null)
            {
                owner.Session.BeginCycle("cycle", new Dictionary<string, decimal> { ["EURUSD"] = 1000m }, StartAt);
                if (lifecycle is "send" or "completed")
                {
                    owner.Session.RecordSendIntent(new("cycle", "parent", "child", "D", null, "EURUSD", "4001", "BUY", .1m,
                        new string('a', 64), "1", "3"), StartAt.AddSeconds(1));
                    if (lifecycle == "completed") owner.Session.RecordSendCompleted("child", StartAt.AddSeconds(1));
                }
            }
            owner.Session.RecordRuntimeFault(fault, StartAt.AddSeconds(2));
        }
        internal FileStream Lease() => new(System.IO.Path.Combine(Root, "owner.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        internal LmaxDemoNoSendRetirementCertificate Prepare(LmaxDemoRetirementObservation? observation = null,
            LmaxDemoRetirementDatabaseEvidence? database = null) => LmaxDemoNoSendRetirement.Prepare(Path,
                LmaxDemoNoSendRetirement.HashFile(Path), Approval, observation ?? Observation, database ?? Database, Now, true);
        internal LmaxDemoNoSendRetirementCertificate Retire()
        {
            using var lease = Lease();
            var certificate = Prepare();
            LmaxDemoNoSendRetirement.WriteUnderOwnerLease(Path, certificate, Now, true);
            return certificate;
        }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
