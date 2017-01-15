﻿using System.Threading;
using System.Threading.Tasks;

using FubarDev.WebDavServer.Model;

namespace FubarDev.WebDavServer
{
    public interface IWebDavResult
    {
        WebDavStatusCodes StatusCode { get; }

        Task ExecuteResultAsync(IWebDavResponse response, CancellationToken ct);
    }
}
