﻿using Microsoft.Extensions.Logging;
using Soenneker.Extensions.String;
using Soenneker.Git.Util.Abstract;
using Soenneker.Runners.Telnyx.OpenApiClient.Utils.Abstract;
using Soenneker.Utils.Dotnet.Abstract;
using Soenneker.Utils.Environment;
using Soenneker.Utils.Process.Abstract;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.File.Download.Abstract;
using Soenneker.Utils.FileSync.Abstract;
using System.Collections.Generic;

namespace Soenneker.Runners.Telnyx.OpenApiClient.Utils;

///<inheritdoc cref="IFileOperationsUtil"/>
public class FileOperationsUtil : IFileOperationsUtil
{
    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IGitUtil _gitUtil;
    private readonly IDotnetUtil _dotnetUtil;
    private readonly IProcessUtil _processUtil;
    private readonly ITelnyxOpenApiFixer _telnyxOpenApiFixer;
    private readonly IFileDownloadUtil _fileDownloadUtil;
    private readonly IFileUtilSync _fileUtilSync;
    private readonly IFileUtil _fileUtil;

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IGitUtil gitUtil, IDotnetUtil dotnetUtil, IProcessUtil processUtil,
        ITelnyxOpenApiFixer telnyxOpenApiFixer, IFileDownloadUtil fileDownloadUtil, IFileUtilSync fileUtilSync, IFileUtil fileUtil)
    {
        _logger = logger;
        _gitUtil = gitUtil;
        _dotnetUtil = dotnetUtil;
        _processUtil = processUtil;
        _telnyxOpenApiFixer = telnyxOpenApiFixer;
        _fileDownloadUtil = fileDownloadUtil;
        _fileUtilSync = fileUtilSync;
        _fileUtil = fileUtil;
    }

    public async ValueTask Process(CancellationToken cancellationToken = default)
    {
        string gitDirectory = _gitUtil.CloneToTempDirectory($"https://github.com/soenneker/{Constants.Library.ToLowerInvariantFast()}");

        string targetFilePath = Path.Combine(gitDirectory, "spec3.json");

        _fileUtilSync.DeleteIfExists(targetFilePath);

        string? filePath = await _fileDownloadUtil.Download("https://raw.githubusercontent.com/team-telnyx/openapi/refs/heads/master/openapi/spec3.json",
            targetFilePath, fileExtension: ".json", cancellationToken: cancellationToken);

        await _processUtil.Start("dotnet", null, "tool update --global Microsoft.OpenApi.Kiota", waitForExit: true, cancellationToken: cancellationToken);

        string fixedFilePath = Path.Combine(gitDirectory, "fixed.json");

        await _telnyxOpenApiFixer.Fix(filePath, fixedFilePath, cancellationToken).NoSync();

        string srcDirectory = Path.Combine(gitDirectory, "src");

        DeleteAllExceptCsproj(srcDirectory);

        await _processUtil.Start("kiota", gitDirectory, $"kiota generate -l CSharp -d \"{fixedFilePath}\" -o src -c TelnyxOpenApiClient -n Soenneker.Telnyx",
            waitForExit: true, cancellationToken: cancellationToken).NoSync();

        await FixLoopcountNamespaces(srcDirectory, cancellationToken).NoSync();

        await BuildAndPush(gitDirectory, cancellationToken).NoSync();
    }

    // Needed because of bug in Kiota
    public async ValueTask FixLoopcountNamespaces(string directory, CancellationToken cancellationToken = default)
    {
        const string fileName = "Loopcount.cs";

        string[] requiredNamespaces =
        [
            "using System;",
            "using System.Collections.Generic;"
        ];

        // Search for the file in the directory
        string[] files = Directory.GetFiles(directory, fileName, SearchOption.AllDirectories);

        if (files.Length == 0)
        {
            _logger.LogWarning("File '{FileName}' not found in directory '{Directory}'.", fileName, directory);
            return;
        }

        string filePath = files[0]; // Assuming only one Loopcount.cs file
        List<string> lines = await _fileUtil.ReadAsLines(filePath, cancellationToken);

        // Check if any required namespaces are missing
        bool needsUpdate = requiredNamespaces.Any(ns => !lines.Contains(ns));

        if (needsUpdate)
        {
            _logger.LogInformation("Updating namespaces in {FilePath}...", filePath);
            string updatedContent = string.Join(Environment.NewLine, requiredNamespaces) + Environment.NewLine + string.Join(Environment.NewLine, lines);

            await _fileUtil.Write(filePath, updatedContent, cancellationToken);

            _logger.LogInformation("Namespaces added successfully to {FilePath}.", filePath);
        }
        else
        {
            _logger.LogInformation("All required namespaces are already present in {FilePath}.", filePath);
        }
    }

    public void DeleteAllExceptCsproj(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
            return;
        }

        try
        {
            // Delete all files except .csproj
            foreach (string file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogInformation("Deleted file: {FilePath}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete file: {FilePath}", file);
                    }
                }
            }

            // Delete all empty subdirectories
            foreach (string dir in Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
                                            .OrderByDescending(d => d.Length)) // Sort by depth to delete from deepest first
            {
                try
                {
                    if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir, recursive: false);
                        _logger.LogInformation("Deleted empty directory: {DirectoryPath}", dir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete directory: {DirectoryPath}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while cleaning the directory: {DirectoryPath}", directoryPath);
        }
    }

    private async ValueTask BuildAndPush(string gitDirectory, CancellationToken cancellationToken)
    {
        string projFilePath = Path.Combine(gitDirectory, "src", $"{Constants.Library}.csproj");

        await _dotnetUtil.Restore(projFilePath, cancellationToken: cancellationToken);

        bool successful = await _dotnetUtil.Build(projFilePath, true, "Release", false, cancellationToken: cancellationToken);

        if (!successful)
        {
            _logger.LogError("Build was not successful, exiting...");
            return;
        }

        string gitHubToken = EnvironmentUtil.GetVariableStrict("GITHUB_TOKEN");

        await _gitUtil.CommitAndPush(gitDirectory, "soenneker", "Jake Soenneker", "jake@soenneker.com", gitHubToken, "Automated update");
    }
}