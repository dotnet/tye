using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Blazor
{
    public interface IEndpointService
    {
        Task<string> GetBackend();
    }

    public class EndpointService : IEndpointService
    {
        private readonly HttpClient httpClient;

        public EndpointService(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<string> GetBackend()
        {
            var endpoints = await getEndpoints();
            return endpoints.backend;
        }

        private async Task<Endpoints> getEndpoints()
        {
            return await httpClient.GetFromJsonAsync<Endpoints>("/endpoints.json");
        }

        private class Endpoints
        {
            public string backend { get; set; }
            public string blazorHost { get; set; }
        }
    }
}