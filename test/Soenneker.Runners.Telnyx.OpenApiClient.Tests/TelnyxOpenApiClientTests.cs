using Soenneker.Facts.Local;
using Soenneker.OpenApi.Fixer.Abstract;
using Soenneker.Tests.FixturedUnit;
using Soenneker.Utils.File.Abstract;
using System.Threading.Tasks;
using Soenneker.Runners.Telnyx.OpenApiClient.Utils.Abstract;
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

        var fileOperationsUtil = Resolve<IFileOperationsUtil>(true);

        var fileUtil = Resolve<IFileUtil>();

        await fixer.Fix("c:\\telnyx\\spec3.json", "c:\\telnyx\\fixed.json");

       // var processUtil = Resolve<IProcessUtil>(true);

      //  await processUtil.Start("kiota", @"c:\telnyx\", $"kiota generate -l CSharp -d \"fixed.json\" -o src -c TelnyxOpenApiClient -n {Constants.Library} --clean-output --clear-cache",
      //      waitForExit: true, cancellationToken: CancellationToken).NoSync();

    }

}
