﻿// <copyright file="TestWebDavController.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using FubarDev.WebDavServer.AspNetCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FubarDev.WebDavServer.Tests.Support
{
    [Route("{*path}")]
    /* [Authorize] */
    public class TestWebDavController : WebDavControllerBase
    {
        public TestWebDavController(
            IWebDavContext context,
            IWebDavDispatcher dispatcher,
            ILoggerFactory? loggerFactory = null)
            : base(context, dispatcher, loggerFactory)
        {
        }
    }
}
