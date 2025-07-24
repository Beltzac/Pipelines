using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Common.Models;
using Common.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Common.Services
{
    public class TempoService : ITempoService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configService;
        private readonly ILogger<TempoService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public TempoService(HttpClient httpClient, IConfigurationService configService, ILogger<TempoService> logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        private (string baseUrl, string apiToken) GetTempoConfig()
        {
            var config = _configService.GetConfig();
            if (config.TempoConfig == null || string.IsNullOrEmpty(config.TempoConfig.ApiToken))
            {
                throw new InvalidOperationException("Tempo API configuration is missing");
            }

            // Ensure the base URL ends with the correct API path
            var baseUrl = config.TempoConfig.BaseUrl.TrimEnd('/');

            return (baseUrl, config.TempoConfig.ApiToken);
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint)
        {
            var (baseUrl, apiToken) = GetTempoConfig();
            var request = new HttpRequestMessage(method, $"{baseUrl}/{endpoint}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return request;
        }

        public async Task<List<TempoWorklog>> GetWorklogsAsync(DateTime? from = null, DateTime? to = null)
        {
            var queryParams = new List<string>();
            if (from.HasValue)
                queryParams.Add($"from={from.Value:yyyy-MM-dd}");
            if (to.HasValue)
                queryParams.Add($"to={to.Value:yyyy-MM-dd}");

            var endpoint = $"worklogs" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");
            var request = CreateRequest(HttpMethod.Get, endpoint);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<TempoWorklogResponse>(content, _jsonOptions);
            return responseData?.Results ?? new List<TempoWorklog>();
        }

        public async Task<List<TempoWorklog>> GetWorklogsByUserAsync(string accountId, DateTime? from = null, DateTime? to = null)
        {
            var requestBody = new
            {
                authorIds = new[] { accountId },
                from = from?.ToString("yyyy-MM-dd"),
                to = to?.ToString("yyyy-MM-dd")
            };

            var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = CreateRequest(HttpMethod.Post, "worklogs/search");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<TempoWorklogResponse>(responseContent, _jsonOptions);
            return responseData?.Results ?? new List<TempoWorklog>();
        }

        public async Task<TempoWorklog> CreateWorklogAsync(CreateWorklogRequest request)
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpRequest = CreateRequest(HttpMethod.Post, "worklogs");
            httpRequest.Content = content;

            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TempoWorklog>(responseContent, _jsonOptions);
        }

        public async Task<TempoWorklog> UpdateWorklogAsync(string worklogId, CreateWorklogRequest request)
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var httpRequest = CreateRequest(HttpMethod.Put, $"worklogs/{worklogId}");
            httpRequest.Content = content;

            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TempoWorklog>(responseContent, _jsonOptions);
        }

        public async Task<bool> DeleteWorklogAsync(string worklogId)
        {
            var request = CreateRequest(HttpMethod.Delete, $"worklogs/{worklogId}");
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        public async Task<List<TempoWorklog>> GetWorklogsByIssueAsync(string issueId)
        {
            // Use the correct worklogs endpoint with issue filter
            var request = CreateRequest(HttpMethod.Get, $"worklogs/issue/{issueId}");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<TempoWorklogResponse>(content, _jsonOptions);
            return responseData?.Results ?? new List<TempoWorklog>();
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var request = CreateRequest(HttpMethod.Get, "worklogs?limit=1");
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test Tempo API connection");
                return false;
            }
        }
    }
}