using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Runners.Telnyx.OpenApiClient.Utils.Abstract;

/// <summary>
/// Defines the file operations util contract.
/// </summary>
public interface IFileOperationsUtil
{
    /// <summary>
    /// Executes the process operation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask Process(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all except csproj.
    /// </summary>
    /// <param name="directoryPath">The directory path.</param>
    /// <param name="log">The log.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask DeleteAllExceptCsproj(string directoryPath, bool log = true, CancellationToken cancellationToken = default);
}
