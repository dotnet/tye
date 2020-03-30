using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace products
{
    public class Startup
    {
        private readonly JsonSerializerOptions options = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDaprClient(client =>
            {
                client.UseJsonSerializationOptions(options);
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });

                endpoints.MapPost("/list", async context =>
                {
                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.Body, AllProducts, options: options);
                });
            });
        }

        private class Product
        {
            public int Id { get; set; }

            public string Name { get; set; } = default!;

            public string Description { get; set; } = default!;

            public decimal Price { get; set; }
        }

        private static readonly Product[] AllProducts = new Product[]
        {
            new Product()
            {
                Id = 1,
                Name = "Do it yourself haircut kit",
                Description = "Some of us needed a haircut before Coronavirus hit...",
                Price = 199.95m,
            },
            new Product()
            {
                Id = 2,
                Name = "That book you've been meaning to read",
                Description = "You know you have some free time now that you're stuck at home.",
                Price = 15.73m,
            },
            new Product()
            {
                Id = 3,
                Name = "That new video game you really want to play (preorder)",
                Description = "This is the perfect way to self-isolate. Let's hope it ships on time.",
                Price = 59.99m,
            },
            new Product()
            {
                Id = 4,
                Name = "The Juice Loosener",
                Description = "It's whisper quiet. Invented by Dr. Nick Riviera!",
                Price = 199.95m,
            }
        };
    }
}
