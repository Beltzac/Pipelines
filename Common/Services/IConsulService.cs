﻿﻿﻿﻿﻿﻿﻿using Common.Models;

namespace Common.Services
{
    public interface IConsulService
    {
        Task DownloadConsulAsync(ConsulEnvironment consulEnv);
        Task<Dictionary<string, ConsulKeyValue>> GetConsulKeyValues(ConsulEnvironment consulEnv);
        Task OpenInVsCode(ConsulEnvironment env);
        void SaveKvToFile(string folderPath, string key, string value);
        Task UpdateConsulKeyValue(ConsulEnvironment consulEnv, string key, string value);
        Task<Dictionary<string, string>> CompareAsync(string sourceEnv, string targetEnv, bool useRecursive = true);
    }
}
