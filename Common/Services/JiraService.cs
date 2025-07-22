using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Common.Models;
using Common.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Common.Services
{
    public class JiraService : IJiraService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configService;
        private readonly ILogger<JiraService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public JiraService(HttpClient httpClient, IConfigurationService configService, ILogger<JiraService> logger)
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

        private (string baseUrl, string apiToken, string email) GetJiraConfig()
        {
            var config = _configService.GetConfig();
            if (config.JiraConfig == null || string.IsNullOrEmpty(config.JiraConfig.ApiToken) || string.IsNullOrEmpty(config.JiraConfig.Email))
            {
                throw new InvalidOperationException("Jira API configuration is missing");
            }

            var baseUrl = config.JiraConfig.BaseUrl.TrimEnd('/');
            return (baseUrl, config.JiraConfig.ApiToken, config.JiraConfig.Email);
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint)
        {
            var (baseUrl, apiToken, email) = GetJiraConfig();
            var request = new HttpRequestMessage(method, $"{baseUrl}/rest/api/3/{endpoint}");

            // Use Basic Auth for Jira API
            var authHeader = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{email}:{apiToken}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return request;
        }

        public async Task<string> GetIssueIdByKeyAsync(string issueKey)
        {
            try
            {
                var request = CreateRequest(HttpMethod.Get, $"issue/{Uri.EscapeDataString(issueKey)}?fields=id");
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to get issue ID for key {issueKey}. Status: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var issue = JsonSerializer.Deserialize<JsonElement>(content, _jsonOptions);

                if (issue.TryGetProperty("id", out var idProperty))
                {
                    return idProperty.GetString();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting issue ID for key {issueKey}");
                return null;
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var request = CreateRequest(HttpMethod.Get, "myself");
                var response = await _httpClient.SendAsync(request);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing Jira connection");
                return false;
            }
        }
    }
}