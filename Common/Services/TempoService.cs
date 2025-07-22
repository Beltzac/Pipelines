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

        private void ConfigureHttpClient()
        {
            var config = _configService.GetConfig();
            if (config.TempoConfig == null || string.IsNullOrEmpty(config.TempoConfig.ApiToken))
            {
                throw new InvalidOperationException("Tempo API configuration is missing");
            }

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.TempoConfig.ApiToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.BaseAddress = new Uri(config.TempoConfig.BaseUrl);
        }

        public async Task<List<TempoWorklog>> GetWorklogsAsync(DateTime? from = null, DateTime? to = null)
        {
            ConfigureHttpClient();

            var queryParams = new List<string>();
            if (from.HasValue)
                queryParams.Add($"from={from.Value:yyyy-MM-dd}");
            if (to.HasValue)
                queryParams.Add($"to={to.Value:yyyy-MM-dd}");

            var url = $"worklogs" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<TempoWorklog>>(content, _jsonOptions) ?? new List<TempoWorklog>();
        }

        public async Task<TempoWorklog> CreateWorklogAsync(CreateWorklogRequest request)
        {
            ConfigureHttpClient();

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("worklogs", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TempoWorklog>(responseContent, _jsonOptions);
        }

        public async Task<TempoWorklog> UpdateWorklogAsync(string worklogId, CreateWorklogRequest request)
        {
            ConfigureHttpClient();

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"worklogs/{worklogId}", content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<TempoWorklog>(responseContent, _jsonOptions);
        }

        public async Task<bool> DeleteWorklogAsync(string worklogId)
        {
            ConfigureHttpClient();

            var response = await _httpClient.DeleteAsync($"worklogs/{worklogId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<List<TempoWorklog>> GetWorklogsByIssueAsync(string issueKey)
        {
            ConfigureHttpClient();

            var response = await _httpClient.GetAsync($"worklogs/issue/{issueKey}");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<TempoWorklog>>(content, _jsonOptions) ?? new List<TempoWorklog>();
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                ConfigureHttpClient();
                var response = await _httpClient.GetAsync("worklogs?limit=1");
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