using QQ.Production.Intraday.Infrastructure.PostgreSql;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bLmaxDemoRdsProfilePinTests
{
    [Fact]
    public void Direct_profiles_are_pinned_to_the_verified_lmax_demo_runtime()
    {
        var profiles = new[]
        {
            Arch7bPostgreSqlPinnedTransportProfile.DirectPrimary,
            Arch7bPostgreSqlTransportProfile.DirectPrimary
        };

        foreach (var profile in profiles)
        {
            Assert.Equal("i-05626133ca7892fb8",
                profile.ExecutionHostInstanceId);
            Assert.Equal("10.0.2.182", profile.ExecutionHostPrivateIp);
            Assert.Equal("subnet-06a16e14d266882ca",
                profile.ExecutionHostSubnetId);
            Assert.Equal("sg-03233b311b56d35cf",
                profile.ExecutionHostSecurityGroupId);
            Assert.Equal("sgr-07a13e1b3994ab26a",
                profile.RdsIngressRuleId);
            Assert.Equal("sgr-0f388b26ba25f8e91",
                profile.RunnerEgressRuleId);
            Assert.Equal("10.0.11", profile.DotNetRuntimeVersion);
        }
    }
}
