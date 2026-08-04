using System.Text.Json.Nodes;
using QQ.Production.Intraday.Tools.Arch7bOneShotSupervisor;

namespace QQ.Production.Intraday.Tests.Unit;

public sealed class Arch7bCanonicalJsonTests
{
    [Fact]
    public void Serialize_MatchesNodeJsonStringifyEscapingForBrokerPayload()
    {
        var value = new JsonObject
        {
            ["z"] = "2026-08-04T13:50:25.696+00:00 <ready> & stable",
            ["a"] = "C:\\QQFund\\ARCH7B\\run"
        };

        var actual = Arch7bCanonicalJson.Serialize(value);

        Assert.Equal(
            "{\"a\":\"C:\\\\QQFund\\\\ARCH7B\\\\run\",\"z\":" +
            "\"2026-08-04T13:50:25.696+00:00 <ready> & stable\"}",
            actual);
        Assert.DoesNotContain("\\u002B", actual, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u003C", actual, StringComparison.Ordinal);
    }
}
