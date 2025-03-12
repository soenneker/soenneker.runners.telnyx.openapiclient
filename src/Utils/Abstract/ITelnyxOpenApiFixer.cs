using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Runners.Telnyx.OpenApiClient.Utils.Abstract;

public interface ITelnyxOpenApiFixer
{
    ValueTask Fix(string sourceFilePath, string targetFilePath, CancellationToken cancellationToken = default);
}