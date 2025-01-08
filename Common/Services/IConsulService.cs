﻿using Common.Models;

namespace Common.Services
{
    public record ConsulDiffResult(string Key, string DiffString);

    public interface IConsulService
    {
        Task DownloadConsulAsync(ConsulEnvironment consulEnv);
        Task<Dictionary<string, ConsulKeyValue>> GetConsulKeyValues(ConsulEnvironment consulEnv);
        Task OpenInVsCode(ConsulEnvironment env);
        void SaveKvToFile(string folderPath, string key, string value);
        Task UpdateConsulKeyValue(ConsulEnvironment consulEnv, string key, string value);
        IAsyncEnumerable<ConsulDiffResult> CompareAsyncEnumerable(string sourceEnv, string targetEnv, bool useRecursive = true, int? skip = null, int? take = null);
        ConsulDiffResult GetDiff(string key, ConsulKeyValue oldValue, ConsulKeyValue newValue, bool recursive);
    }
}
