using Soenneker.Tests.Attributes.Local;
using Soenneker.OpenApi.Fixer.Abstract;
using Soenneker.Tests.HostedUnit;
using Soenneker.Utils.File.Abstract;
using System.Threading.Tasks;
using Soenneker.Facts.Manual;
using Soenneker.Runners.Telnyx.OpenApiClient.Utils.Abstract;

namespace Soenneker.Runners.Telnyx.OpenApiClient.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public class TelnyxOpenApiClientTests : HostedUnitTest
{
    public TelnyxOpenApiClientTests(Host host) : base(host)
    {
    }

    [Test]
    public void Default()
    {
    }

    [ManualFact]
    // [LocalOnly]
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