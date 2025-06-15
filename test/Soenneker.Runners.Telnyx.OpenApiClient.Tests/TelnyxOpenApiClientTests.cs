using Soenneker.Facts.Local;
using Soenneker.OpenApi.Fixer.Abstract;
using Soenneker.Tests.FixturedUnit;
using System.Threading.Tasks;
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

    [LocalFact]
    public async ValueTask Fix()
    {
        var fixer = Resolve<IOpenApiFixer>(true);

        await fixer.Fix("c:\\telnyx\\spec3.json", "c:\\telnyx\\fixed.json");
    }

}
