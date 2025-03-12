using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Soenneker.Extensions.ValueTask;
using Soenneker.Runners.Telnyx.OpenApiClient.Utils.Abstract;
using Soenneker.Utils.File.Download.Abstract;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Runners.Telnyx.OpenApiClient;

public class ConsoleHostedService : IHostedService
{
    private readonly ILogger<ConsoleHostedService> _logger;

    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IFileDownloadUtil _fileDownloadUtil;
    private readonly ITelnyxOpenApiFixer _telnyxOpenApiFixer;
    private readonly IFileOperationsUtil _fileOperationsUtil;

    private int? _exitCode;

    public ConsoleHostedService(ILogger<ConsoleHostedService> logger, IHostApplicationLifetime appLifetime,
        IFileDownloadUtil fileDownloadUtil, ITelnyxOpenApiFixer telnyxOpenApiFixer, IFileOperationsUtil fileOperationsUtil)
    {
        _logger = logger;
        _appLifetime = appLifetime;
        _fileDownloadUtil = fileDownloadUtil;
        _telnyxOpenApiFixer = telnyxOpenApiFixer;
        _fileOperationsUtil = fileOperationsUtil;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _appLifetime.ApplicationStarted.Register(() =>
        {
            Task.Run(async () =>
            {
                _logger.LogInformation("Running console hosted service ...");

                try
                {
                    string? filePath = await _fileDownloadUtil.Download("https://raw.githubusercontent.com/team-telnyx/openapi/refs/heads/master/openapi/spec3.json", fileExtension: ".json", cancellationToken: cancellationToken);

                    string fixedFilePath = Path.Combine(Path.GetTempPath(), "fixed.json");

                    await _telnyxOpenApiFixer.Fix(filePath, fixedFilePath, cancellationToken).NoSync();

                    await _fileOperationsUtil.Process(fixedFilePath, cancellationToken);

                    _logger.LogInformation("Complete!");

                    _exitCode = 0;
                }
                catch (Exception e)
                {
                    if (Debugger.IsAttached)
                        Debugger.Break();

                    _logger.LogError(e, "Unhandled exception");

                    await Task.Delay(2000, cancellationToken);
                    _exitCode = 1;
                }
                finally
                {
                    // Stop the application once the work is done
                    _appLifetime.StopApplication();
                }
            }, cancellationToken);
        });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Exiting with return code: {exitCode}", _exitCode);

        // Exit code may be null if the user cancelled via Ctrl+C/SIGTERM
        Environment.ExitCode = _exitCode.GetValueOrDefault(-1);
        return Task.CompletedTask;
    }
}
