// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared;

namespace Frontend.Pages
{
    public class IndexModel : PageModel
    {
        public void OnGet()
        {

        }

        public async Task<IActionResult> OnPost([FromServices] IOrderService client)
        {
            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                CreatedTime = DateTime.UtcNow,
                UserId = User.Identity.Name!
            };

            await client.PlaceOrderAsync(order);

            return Redirect("/");
        }
    }
}
