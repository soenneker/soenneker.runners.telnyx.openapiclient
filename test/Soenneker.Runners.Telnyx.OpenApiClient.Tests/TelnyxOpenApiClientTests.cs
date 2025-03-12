using Soenneker.Tests.FixturedUnit;
using Xunit;

namespace Soenneker.Runners.Telnyx.OpenApiClient.Tests;

[Collection("Collection")]
public class TelnyxOpenApiClientTests : FixturedUnitTest
{
    public TelnyxOpenApiClientTests(Fixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    [Fact]
    public void Default()
    {

    }
}
