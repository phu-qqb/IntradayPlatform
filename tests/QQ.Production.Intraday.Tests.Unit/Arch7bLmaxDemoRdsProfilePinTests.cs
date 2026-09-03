using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bLmaxDemoRdsProfilePinTests
{
    [Fact]
    public void Direct_profiles_are_pinned_to_the_verified_lmax_demo_runtime()
    {
        var pinned = Arch7bPostgreSqlPinnedTransportProfile.DirectPrimary;
        AssertFacts(
            pinned.ExecutionHostInstanceId, pinned.ExecutionHostPrivateIp,
            pinned.ExecutionHostSubnetId, pinned.ExecutionHostSecurityGroupId,
            pinned.RdsIngressRuleId, pinned.RunnerEgressRuleId,
            pinned.DotNetRuntimeVersion);

        var pooled = Arch7bPostgreSqlTransportProfile.DirectPrimary;
        AssertFacts(
            pooled.ExecutionHostInstanceId, pooled.ExecutionHostPrivateIp,
            pooled.ExecutionHostSubnetId, pooled.ExecutionHostSecurityGroupId,
            pooled.RdsIngressRuleId, pooled.RunnerEgressRuleId,
            pooled.DotNetRuntimeVersion);
    }

    private static void AssertFacts(
        string instanceId,
        string privateIp,
        string subnetId,
        string securityGroupId,
        string rdsIngressRuleId,
        string runnerEgressRuleId,
        string dotNetRuntimeVersion)
    {
        Assert.Equal("i-05626133ca7892fb8", instanceId);
        Assert.Equal("10.0.2.182", privateIp);
        Assert.Equal("subnet-06a16e14d266882ca", subnetId);
        Assert.Equal("sg-03233b311b56d35cf", securityGroupId);
        Assert.Equal("sgr-07a13e1b3994ab26a", rdsIngressRuleId);
        Assert.Equal("sgr-0f388b26ba25f8e91", runnerEgressRuleId);
        Assert.Equal("10.0.11", dotNetRuntimeVersion);
    }
}
