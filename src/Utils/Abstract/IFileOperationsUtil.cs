﻿using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Runners.Telnyx.OpenApiClient.Utils.Abstract;

public interface IFileOperationsUtil
{
    ValueTask Process(string filePath, CancellationToken cancellationToken = default);
}
