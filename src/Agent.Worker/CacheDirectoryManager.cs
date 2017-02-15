using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;

namespace Microsoft.VisualStudio.Services.Agent.Worker
{
    [ServiceLocator(Default = typeof(CacheDirectoryManager))]
    public interface ICacheDirectoryManager : IAgentService
    {
        Task Initialize(IExecutionContext jobContext);
        Task Cleanup(IExecutionContext jobContext);
    }

    public sealed class CacheDirectoryManager : AgentService, IJobRunner
    {
        public async Task Initialize(IExecutionContext jobContext)
        {
            ArgUtil.NotNull(jobContext, nameof(jobContext);

#if OS_WINDOWS
            // TEMP and TMP.
            if (ConvertToBoolean(Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.OverrideTemp)) == false)
            {
                jobContext.Debug($"Skipping override {Constants.EnvironmentVariables.Temp}");
            }
            else
            {
                string tempDirectory = Path.Combine(HostContext.GetDirectory(WellKnownDirectory.Work), Constants.Path.TempDirectory);
                jobContext.Debug($"SET {Constants.EnvironmentVariables.Temp}={tempDirectory}");
                Directory.CreateDirectory(tempDirectory);
                SetEnvironmentVariable(Constants.EnvironmentVariables.Temp, tempDirectory);
                SetEnvironmentVariable(Constants.EnvironmentVariables.Tmp, tempDirectory);

                jobContext.Debug($"Cleaning {Constants.EnvironmentVariables.Temp}");
                try
                {
                    IOUtil.Delete(tempDirectory, contentsOnly: true, cancellationToken: jobContext.CancellationToken);
                }
                catch (Exception ex)
                {
                    Trace.Error("Failed cleaning one or more temp file");
                    Trace.Error(ex);
                }
            }
#endif

            // NuGet cache directory.
            if (!string.IsNullOrEmpty(VarUtil.GetEnvironmentVariable(Constants.EnvironmentVariables.NuGetPackages, forceInsensitive: false)) ||
                !string.IsNullOrEmpty(jobContext.Variables.Get(Constants.EnvironmentVariables.NuGetPackages)) ||
                ConvertToBoolean(VarUtil.GetEnvironmentVariable(Constants.EnvironmentVariables.OverrideNuGetPackages, forceInsensitive: false) == false ||
                jobContext.Variables.GetBoolean(Constants.EnvironmentVariables.OverrideNuGetPackages) == false)
            {
                jobContext.Debug($"Skipping override {Constants.EnvironmentVariables.NuGetPackages}");
            }
            else
            {
                string nuGetCacheDirectory = Path.Combine(
                    HostContext.GetDirectory(WellKnownDirectory.Work),
                    Constants.Path.CacheDirectory,
                    Constants.Path.NuGetCacheDirectory);
                jobContext.Debug($"SET {Constants.EnvironmentVariables.NugetPackages}={nuGetCacheDirectory}");
                Directory.CreateDirectory(nuGetCacheDirectory);
                Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.NuGetPackages, nuGetCacheDirectory);
            }

            // NPM cache directory. NPM treats NPM_CONFIG_CACHE case insensitive.
            if (!string.IsNullOrEmpty(VarUtil.GetEnvironmentVariable(Constants.EnvironmentVariables.NpmConfigCache, forceInsensitive: true)) ||
                !string.IsNullOrEmpty(jobContext.Variables.Get(Constants.EnvironmentVariables.NpmConfigCache)) ||
                ConvertToBoolean(VarUtil.GetEnvironmentVariable(Constants.EnvironmentVariables.OverrideNpmConfigCache, forceInsensitive: false) == false ||
                jobContext.Variables.GetBoolean(Constants.EnvironmentVariables.OverrideNpmConfigCache) == false)
            {
                jobContext.Debug($"Skipping override {Constants.EnvironmentVariables.NpmConfigCache}");
            }
            else
            {
                string npmCacheDirectory = Path.Combine(
                    HostContext.GetDirectory(WellKnownDirectory.Work),
                    Constants.Path.CacheDirectory,
                    Constants.Path.NpmCacheDirectory);
                jobContext.Debug($"SET {Constants.EnvironmentVariables.NpmConfigCache}={npmCacheDirectory}");
                Directory.CreateDirectory(npmCacheDirectory);
                Environment.SetEnvironmentVariable(Constants.EnvironmentVariables.NpmConfigCache, npmCacheDirectory);
            }
        }

        public async Task Cleanup(IExecutionContext jobContext)
        {
            ArgUtil.NotNull(jobContext, nameof(jobContext);
        }

        private static bool? ConvertToBoolean(string value)
        {
            bool val;
            if (bool.TryParse(value ?? string.Empty, out val))
            {
                return val;
            }

            return null;
        }
    }
}
