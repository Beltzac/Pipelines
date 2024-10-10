using ClosedXML.Excel;
using Common.ExternalApis;
using Common.Models;
using Common.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Common.Services
{
    public class CommitDataExportService : ICommitDataExportService
    {
        private readonly IProjectHttpClient _projectClient;
        private readonly IGitHttpClient _gitClient;
        private readonly IConfigurationService _configService;
        private readonly ILogger<CommitDataExportService> _logger;
        private readonly RepositoryDbContext _dbContext;

        public CommitDataExportService(
            IProjectHttpClient projectClient,
            IGitHttpClient gitClient,
            IConfigurationService configService,
            ILogger<CommitDataExportService> logger,
            RepositoryDbContext dbContext)
        {
            _projectClient = projectClient;
            _gitClient = gitClient;
            _configService = configService;
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task FetchCommitDataAsync(IProgress<int> progress = null)
        {
            try
            {
                // 1. List all projects
                var projects = await _projectClient.GetProjects();

                int totalProjects = projects.Count;
                int currentProject = 0;

                foreach (var project in projects)
                {
                    currentProject++;
                    progress?.Report((currentProject * 100) / totalProjects);
                    string projectName = project.Name;

                    // Skip specific projects
                    if (projectName.Equals("Repositories Backups", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"Skipping project: {projectName}");
                        continue;
                    }

                    _logger.LogInformation($"Processing project: {projectName}");

                    // 2. List all repositories in the project
                    var repositories = await _gitClient.GetRepositoriesAsync(project.Id);

                    foreach (var repo in repositories)
                    {
                        string repoName = repo.Name;

                        // Skip specific repositories
                        if (repoName.Equals("IdentityServer4", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation($"Skipping repository: {repoName}");
                            continue;
                        }

                        _logger.LogInformation($"Processing repository: {repoName}");

                        // 3. List all branches in the repository
                        try
                        {
                            var branches = await _gitClient.GetBranchesAsync(project.Id, repo.Id);

                            foreach (var branch in branches)
                            {
                                try
                                {
                                    string branchName = GetBranchName(branch.Name);

                                    _logger.LogInformation($"Processing branch: {branchName} in repository: {repoName}");

                                    // 4. List commits by the user from the configuration in the last 30 days
                                    var commits = await _gitClient.GetCommitsAsync(project.Id, repo.Id, branchName, _configService.GetConfig().Username, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow);

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

                                        await _dbContext.SingleMergeAsync(model, options =>
                                        {
                                            options.IncludeGraph = true;
                                            options.InsertKeepIdentity = true;
                                            options.MergeKeepIdentity = true;
                                        });

                                        _logger.LogInformation($"Commit added to database: {commitDate} - {commitMessage}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"An error occurred while processing branch: {branch.Name} in repository: {repo.Name}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"An error occurred while processing repository: {repo.Name}");
                        }
                    }
                }


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while exporting commit data.");
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
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");

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
        public async Task<List<Commit>> GetRecentCommitsAsync(string username, int limit = 100)
        {
            string trimmedFilter = username.Trim();

            return await _dbContext.Commits
                .Where(x => EF.Functions.Like(x.AuthorName, $"%{trimmedFilter}%"))
                .OrderByDescending(c => c.CommitDate)
                .Take(limit)
                .ToListAsync();
        }

        public async Task ExportCommitDataAsync()
        {
            var commitDataList = await _dbContext.Commits.ToListAsync();

            if (commitDataList != null && commitDataList.Any())
            {
                // Export to Excel
                string filePath = GenerateExcelFilePath();
                ExportToExcel(commitDataList, filePath);

                // Open the file in the default application
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });


                _logger.LogInformation($"Commit data exported to {filePath}");
            }
            else
            {
                _logger.LogInformation("No commit data available to export.");
            }
        }
    }
}
