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
