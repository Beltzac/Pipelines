﻿using ClosedXML.Excel;
using Common.ExternalApis.Interfaces;
using Common.Models;
using Common.Repositories.Interno;
using Common.Services.Interfaces;
using Common.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Common.Services
{
    public class CommitExportService : ICommitExportService
    {
        private readonly IProjectHttpClient _projectClient;
        private readonly IGitHttpClient _gitClient;
        private readonly IConfigurationService _configService;
        private readonly ILogger<CommitExportService> _logger;
        private readonly RepositoryDbContext _dbContext;

        public CommitExportService(
            IProjectHttpClient projectClient,
            IGitHttpClient gitClient,
            IConfigurationService configService,
            ILogger<CommitExportService> logger,
            RepositoryDbContext dbContext)
        {
            _projectClient = projectClient;
            _gitClient = gitClient;
            _configService = configService;
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task FetchCommitDataAsync(IProgress<int> progress = null, DateTime? dateFilter = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // 1. List all projects
                var projects = await _projectClient.GetProjects();

                cancellationToken.ThrowIfCancellationRequested();

                int totalProjects = projects.Count;
                int currentProject = 0;

                foreach (var project in projects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    currentProject++;
                    progress?.Report((currentProject * 100) / totalProjects);
                    string projectName = project.Name;

                    // Skip specific projects
                    if (projectName.Equals("Repositories Backups", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"Pulando projeto: {projectName}");
                        continue;
                    }

                    _logger.LogInformation($"Processando projeto: {projectName}");

                    // 2. List all repositories in the project
                    var repositories = await _gitClient.GetRepositoriesAsync(project.Id);

                    foreach (var repo in repositories)
                    {
                        string repoName = repo.Name;

                        // Skip specific repositories
                        if (repoName.Equals("IdentityServer4", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation($"Pulando repositório: {repoName}");
                            continue;
                        }

                        _logger.LogInformation($"Processando repositório: {repoName}");

                        // 3. List all branches in the repository
                        try
                        {
                            var branches = await _gitClient.GetBranchesAsync(project.Id, repo.Id);

                            foreach (var branch in branches)
                            {
                                // Validar o token de cancelamento
                                cancellationToken.ThrowIfCancellationRequested();

                                try
                                {
                                    string branchName = GetBranchName(branch.Name);

                                    _logger.LogInformation($"Processando branch: {branchName} no repositório: {repoName}");

                                    // 4. List commits by the user from the configuration in the last 30 days
                                    // 4. List commits by the user from the configuration within the date filter
                                    var commits = await _gitClient.GetCommitsAsync(
                                        project.Id,
                                        repo.Id,
                                        branchName,
                                        _configService.GetConfig().Username,
                                        dateFilter ?? DateTime.UtcNow.AddDays(-30),
                                        DateTime.UtcNow);

                                    foreach (var commit in commits)
                                    {
                                        var commitDate = commit.Author.Date.ToUniversalTime();
                                        var commitMessage = commit.Comment;
                                        var jiraCardID = ExtractJiraCardID(commitMessage);

                                        var model = new Commit
                                        {
                                            Id = commit.CommitId,
                                            Url = commit.Url,
                                            AuthorEmail = commit.Author.Email,
                                            AuthorName = commit.Author.Name,
                                            ProjectName = projectName,
                                            RepoName = repoName,
                                            BranchName = branchName,
                                            CommitDate = commitDate,
                                            CommitMessage = commitMessage,
                                            JiraCardID = jiraCardID
                                        };

                                        await _dbContext.UpsertGraphAsync(model);

                                        _logger.LogInformation($"Commit adicionado ao banco de dados: {commitDate} - {commitMessage}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"Ocorreu um erro ao processar a branch: {branch.Name} no repositório: {repo.Name}");
                                   //throw;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Ocorreu um erro ao processar o repositório: {repo.Name}");
                            //throw;
                        }
                    }
                }


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro ao exportar os dados do commit.");
                throw;
            }

        }

        public static string GetBranchName(string fullBranchName)
        {
            // Azure DevOps branch names are in the format 'refs/heads/branchName'
            return fullBranchName.Replace("refs/heads/", string.Empty);
        }

        public static string ExtractJiraCardID(string commitMessage)
        {
            var match = Regex.Match(commitMessage, @"\b[A-Z0-9]+-[0-9]+\b");
            return match.Success ? match.Value : string.Empty;
        }

        private string GenerateExcelFilePath()
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            string fileName = $"CommitData_{timestamp}.xlsx";
            string directory = Path.GetTempPath();

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return Path.Combine(directory, fileName);
        }

        private void ExportToExcel(List<Commit> data, string filePath)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Commit Data");
                worksheet.Cell(1, 1).Value = "Commit ID";
                worksheet.Cell(1, 2).Value = "Commit Message";
                worksheet.Cell(1, 3).Value = "URL";
                worksheet.Cell(1, 4).Value = "Author Name";
                worksheet.Cell(1, 5).Value = "Author Email";
                worksheet.Cell(1, 6).Value = "Project Name";
                worksheet.Cell(1, 7).Value = "Repository Name";
                worksheet.Cell(1, 8).Value = "Branch Name";
                worksheet.Cell(1, 9).Value = "Commit Date";
                worksheet.Cell(1, 10).Value = "JIRA Card ID";

                for (int i = 0; i < data.Count; i++)
                {
                    var commit = data[i];
                    int row = i + 2;

                    worksheet.Cell(row, 1).Value = commit.Id;
                    worksheet.Cell(row, 2).Value = commit.CommitMessage;
                    worksheet.Cell(row, 3).Value = commit.Url;
                    worksheet.Cell(row, 4).Value = commit.AuthorName;
                    worksheet.Cell(row, 5).Value = commit.AuthorEmail;
                    worksheet.Cell(row, 6).Value = commit.ProjectName;
                    worksheet.Cell(row, 7).Value = commit.RepoName;
                    worksheet.Cell(row, 8).Value = commit.BranchName;
                    worksheet.Cell(row, 9).Value = commit.CommitDate.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cell(row, 10).Value = commit.JiraCardID;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }
        public async Task<List<Commit>> GetRecentCommitsAsync(string username, DateTime? dateFilter = null)
        {
            var filterDate = dateFilter ?? DateTime.UtcNow.AddMonths(-1);

            return await _dbContext.Commits
                .Where(x => EF.Functions.Like(x.AuthorName, $"%{username}%") && x.CommitDate >= filterDate)
                .OrderByDescending(c => c.CommitDate)
                .ToListAsync();
        }

        public async Task ExportCommitDataAsync()
        {
            try
            {
                var config = _configService.GetConfig();
                var twoMonthsAgo = DateTime.UtcNow.AddMonths(-2);

                var commitDataList = await _dbContext.Commits
                    .Where(c => EF.Functions.Like(c.AuthorName, $"%{config.Username}%") && c.CommitDate >= twoMonthsAgo)
                    .ToListAsync();

                if (commitDataList != null && commitDataList.Any())
                {
                    // Export to Excel
                    string filePath = GenerateExcelFilePath();
                    ExportToExcel(commitDataList, filePath);

                    // Open the file in the default application
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });


                    _logger.LogInformation($"Dados do commit exportados para {filePath}");
                }
                else
                    _logger.LogInformation("Nenhum dado de commit disponível para exportação.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("A operação foi cancelada pelo usuário.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ocorreu um erro ao exportar os dados do commit.");
                throw;
            }
        }
    }
}
