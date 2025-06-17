using Soenneker.Facts.Local;
using Soenneker.OpenApi.Fixer.Abstract;
using Soenneker.Tests.FixturedUnit;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.FileSync.Abstract;
using Soenneker.Utils.Process.Abstract;
using System.Threading.Tasks;
using Soenneker.Extensions.ValueTask;
using Soenneker.Runners.Telnyx.OpenApiClient.Utils.Abstract;
using Xunit;
using Soenneker.Utils.Usings.Abstract;

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

        var fileSyncUtil = Resolve<IFileUtilSync>(true);

        var fileOperationsUtil = Resolve<IFileOperationsUtil>(true);

        var fileUtil = Resolve<IFileUtil>();

        await fixer.Fix("c:\\telnyx\\spec3.json", "c:\\telnyx\\fixed.json");

       // fileSyncUtil.DeleteAll(@"c:\telnyx\src", false);

       // var processUtil = Resolve<IProcessUtil>(true);

      //  await processUtil.Start("kiota", @"c:\telnyx\", $"kiota generate -l CSharp -d \"fixed.json\" -o src -c TelnyxOpenApiClient -n {Constants.Library} --clean-output --clear-cache",
      //      waitForExit: true, cancellationToken: CancellationToken).NoSync();

    }

}
