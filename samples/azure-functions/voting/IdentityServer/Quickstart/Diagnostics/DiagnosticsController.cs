// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServerHost.Quickstart.UI
{
    [SecurityHeaders]
    [Authorize]
    public class DiagnosticsController : Controller
    {
        public async Task<IActionResult> Index()
        {
            var localAddresses = new string[] { "127.0.0.1", "::1", HttpContext.Connection.LocalIpAddress.ToString() };
            if (!localAddresses.Contains(HttpContext.Connection.RemoteIpAddress.ToString()))
            {
                return NotFound();
            }

            var model = new DiagnosticsViewModel(await HttpContext.AuthenticateAsync());
            return View(model);
        }
    }
}