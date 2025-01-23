﻿using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Xml;

namespace Common.Utils
{
    public static class OpenFolderUtils
    {

        public static async Task OpenUrlAsync(string url)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        public static void OpenFolder(string localPath)
        {
            if (Directory.Exists(localPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = localPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
        }

        public static void OpenWithVSCode(ILogger logger, IConfigurationService configService, string folderPath, bool disableExtensions = false)
        {
            try
            {
                var args = $"\"{folderPath}\"";
                if (disableExtensions)
                {
                    args += " --disable-extensions";
                }

                var config = configService.GetConfig();
                var vscodePath = config.VSCodePath;

                Process.Start(new ProcessStartInfo
                {
                    FileName = vscodePath,
                    Arguments = args,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                logger.LogInformation($"Opening {folderPath} with VS Code.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error opening {folderPath} with VS Code");
            }
        }

        public static bool SolutionContainsTopshelf(string slnFile)
        {
            var solutionDirectory = Path.GetDirectoryName(slnFile);
            var projectFiles = Directory.GetFiles(solutionDirectory, "*.csproj", SearchOption.AllDirectories);

            return projectFiles.Any(ProjectContainsTopshelfReference);
        }

        public static bool ProjectContainsTopshelfReference(string projectFile)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(projectFile);

            var references = xmlDoc.GetElementsByTagName("Reference");
            if (references.Cast<XmlNode>().Any(node => node.Attributes["Include"]?.Value.Contains("Topshelf") == true))
                return true;

            var packageReferences = xmlDoc.GetElementsByTagName("PackageReference");
            return packageReferences.Cast<XmlNode>().Any(node => node.Attributes["Include"]?.Value == "Topshelf");
        }

        public static string FindSolutionFile(string folderPath)
        {
            // Search in the top directory
            string slnFile = Directory.GetFiles(folderPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (slnFile == null)
            {
                // Search in the src folder if the solution file wasn't found in the top directory
                string srcFolderPath = Path.Combine(folderPath, "src");
                if (Directory.Exists(srcFolderPath))
                {
                    slnFile = Directory.GetFiles(srcFolderPath, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
                }
            }

            return slnFile;
        }

        public static void OpenProject(ILogger logger, IConfigurationService configService, string folderPath)
        {
            if (Directory.Exists(folderPath))
            {
                // Check for Android project indicators
                var projectType = DetermineProjectType(folderPath);

                if (projectType == Repository.PROJECT_TYPE_ANDROID)
                {
                    OpenWithAndroidStudio(logger, configService, folderPath);
                }
                else if (projectType == Repository.PROJECT_TYPE_VISUAL_STUDIO)
                {
                    var slnFile = FindSolutionFile(folderPath);
                    OpenWithVisualStudio(logger, configService, slnFile);
                }
                else
                {
                    OpenWithVSCode(logger, configService, folderPath);
                }
            }
            else
            {
                logger.LogInformation($"Directory {folderPath} does not exist.");
            }
        }

        public static void OpenWithAndroidStudio(ILogger logger, IConfigurationService configService, string folderPath)
        {
            try
            {
                var config = configService.GetConfig();
                var androidStudioPath = config.AndroidStudioPath;

                if (string.IsNullOrWhiteSpace(androidStudioPath))
                {
                    throw new InvalidOperationException("Android Studio path is not configured.");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = androidStudioPath,
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });

                logger.LogInformation($"Opening {folderPath} with Android Studio.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error opening {folderPath} with Android Studio");
            }
        }

        public static void OpenWithVisualStudio(ILogger logger, IConfigurationService configService, string slnFile)
        {
            try
            {
                var requiresAdmin = SolutionContainsTopshelf(slnFile);

                var config = configService.GetConfig();
                var visualStudioPath = config.VisualStudioPath;

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = visualStudioPath,
                    Arguments = $"\"{slnFile}\"",
                    UseShellExecute = true,
                    Verb = requiresAdmin ? "runas" : ""
                };

                Process.Start(processStartInfo);
                logger.LogInformation($"Opening {slnFile} with Visual Studio{(requiresAdmin ? " as Administrator" : "")}.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error opening {slnFile} with Visual Studio");
            }
        }

        public static string DetermineProjectType(string folderPath)
        {
            if (Directory.GetFiles(folderPath, "build.gradle", SearchOption.AllDirectories).Any() ||
                Directory.GetFiles(folderPath, "settings.gradle", SearchOption.AllDirectories).Any() ||
                Directory.GetFiles(folderPath, "AndroidManifest.xml", SearchOption.AllDirectories).Any())
            {
                return Repository.PROJECT_TYPE_ANDROID;
            }

            if (FindSolutionFile(folderPath) != null)
            {
                return Repository.PROJECT_TYPE_VISUAL_STUDIO;
            }

            if (Directory.Exists(folderPath))
            {
                return Repository.PROJECT_TYPE_VISUAL_STUDIO_CODE;
            }

            return Repository.PROJECT_TYPE_FOLDER;
        }
    }
}
