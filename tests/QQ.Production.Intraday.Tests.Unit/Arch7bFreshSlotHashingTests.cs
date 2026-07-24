using System.Security.Cryptography;
using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed partial class Arch7bFreshSlotHandoffTests
{
    [Fact]
    public void Slow_hashing_reports_the_precise_phase_and_hashes_only_contract_files()
    {
        var clock = new MutableTimeProvider(Utc(2026, 7, 24, 12, 0, 1));
        var options = Options(clock);
        Directory.CreateDirectory(options.SlotRoot);
        var artifact = Path.Combine(options.SlotRoot, "slot.jsonl");
        var manifest = Path.Combine(options.SlotRoot, "slot_manifest.json");
        File.WriteAllText(artifact, "{\"no_order\":true}\n");
        File.WriteAllText(manifest, "{\"complete\":true}\n");
        var calls = 0;
        string SlowHash(string path)
        {
            calls++;
            clock.UtcNow = clock.UtcNow.AddSeconds(3);
            return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        }

        var timeline = new PmsShadowFreshSlotHandoffTimeline(options, clock);
        var marker = PmsShadowFreshSlotReadyMarkerStore.Build(
            options, artifact, manifest, clock, timeline, SlowHash);

        Assert.Equal(2, calls);
        Assert.Equal(64, marker.ArtifactSha256.Length);
        Assert.Equal(64, marker.ManifestSha256.Length);
        var events = Events(options);
        Assert.Contains(events,
            value => value.EventName == PmsShadowFreshSlotHandoffEvents.IndispensableHashingStarted);
        Assert.Contains(events,
            value => value.EventName == PmsShadowFreshSlotHandoffEvents.IndispensableHashingCompleted &&
                value.Detail!.Contains("files=2", StringComparison.Ordinal));
        Assert.Contains(events,
            value => value.EventName == PmsShadowFreshSlotHandoffEvents.IndispensableHashingSlow &&
                value.Detail!.Contains("phase=ARTIFACT_AND_MANIFEST_SHA256", StringComparison.Ordinal));
    }
}
