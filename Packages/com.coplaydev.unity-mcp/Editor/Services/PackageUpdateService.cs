using System;
using System.Net;
using System.Text.RegularExpressions;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Service for checking package updates from GitHub or Asset Store metadata
    /// </summary>
    public class PackageUpdateService : IPackageUpdateService
    {
        private const int DefaultRequestTimeoutMs = 3000;
        private const string LastCheckDateKey = EditorPrefKeys.LastUpdateCheck;
        private const string CachedVersionKey = EditorPrefKeys.LatestKnownVersion;
        private const string LastBetaCheckDateKey = EditorPrefKeys.LastUpdateCheck + ".beta";
        private const string CachedBetaVersionKey = EditorPrefKeys.LatestKnownVersion + ".beta";
        private const string LastAssetStoreCheckDateKey = EditorPrefKeys.LastAssetStoreUpdateCheck;
        private const string CachedAssetStoreVersionKey = EditorPrefKeys.LatestKnownAssetStoreVersion;
        private const string MainPackageJsonUrl = "https://raw.githubusercontent.com/CoplayDev/unity-mcp/main/MCPForUnity/package.json";
        private const string BetaPackageJsonUrl = "https://raw.githubusercontent.com/CoplayDev/unity-mcp/beta/MCPForUnity/package.json";
        private const string AssetStoreVersionUrl = "https://gqoqjkkptwfbkwyssmnj.supabase.co/storage/v1/object/public/coplay-images/assetstoreversion.json";

        /// <inheritdoc/>
        public UpdateCheckResult CheckForUpdate(string currentVersion)
        {
            var context = CreateContext(currentVersion);
            var cachedResult = TryGetCachedResult(context);
            if (cachedResult != null)
            {
                return cachedResult;
            }

            var result = FetchAndCompare(context);
            if (result != null && result.CheckSucceeded && !string.IsNullOrEmpty(result.LatestVersion))
            {
                CacheFetchResult(context, result.LatestVersion);
            }

            return result;
        }

        /// <inheritdoc/>
        public UpdateCheckContext CreateContext(string currentVersion)
        {
            var sourceContext = ResolveUpdateSource(currentVersion);

            return new UpdateCheckContext
            {
                CurrentVersion = currentVersion,
                IsGitInstallation = sourceContext.IsGitInstallation,
                UseGitHubSource = sourceContext.UseGitHubSource,
                Channel = sourceContext.Channel,
                LastCheckKey = sourceContext.UseGitHubSource
                    ? (string.Equals(sourceContext.Channel, "beta", StringComparison.OrdinalIgnoreCase) ? LastBetaCheckDateKey : LastCheckDateKey)
                    : LastAssetStoreCheckDateKey,
                CachedVersionKey = sourceContext.UseGitHubSource
                    ? (string.Equals(sourceContext.Channel, "beta", StringComparison.OrdinalIgnoreCase) ? CachedBetaVersionKey : CachedVersionKey)
                    : CachedAssetStoreVersionKey
            };
        }

        /// <inheritdoc/>
        public UpdateCheckResult TryGetCachedResult(UpdateCheckContext context)
        {
            if (context == null)
            {
                return null;
            }

            string lastCheckDate = EditorPrefs.GetString(context.LastCheckKey, "");
            string cachedLatestVersion = EditorPrefs.GetString(context.CachedVersionKey, "");

            if (lastCheckDate == DateTime.Now.ToString("yyyy-MM-dd") && !string.IsNullOrEmpty(cachedLatestVersion))
            {
                return new UpdateCheckResult
                {
                    CheckSucceeded = true,
                    LatestVersion = cachedLatestVersion,
                    UpdateAvailable = IsNewerVersion(cachedLatestVersion, context.CurrentVersion),
                    Message = "Using cached version check"
                };
            }

            return null;
        }

        /// <inheritdoc/>
        public UpdateCheckResult FetchAndCompare(UpdateCheckContext context)
        {
            if (context == null)
            {
                return new UpdateCheckResult
                {
                    CheckSucceeded = false,
                    UpdateAvailable = false,
                    Message = "Update check context was not provided"
                };
            }

            string latestVersion = context.UseGitHubSource
                ? FetchLatestVersionFromGitHub(context.Channel)
                : FetchLatestVersionFromAssetStoreJson();

            if (!string.IsNullOrEmpty(latestVersion))
            {
                return new UpdateCheckResult
                {
                    CheckSucceeded = true,
                    LatestVersion = latestVersion,
                    UpdateAvailable = IsNewerVersion(latestVersion, context.CurrentVersion),
                    Message = "Successfully checked for updates"
                };
            }

            return new UpdateCheckResult
            {
                CheckSucceeded = false,
                UpdateAvailable = false,
                Message = context.UseGitHubSource
                    ? "Failed to check for updates (network issue or offline)"
                    : "Failed to check for Asset Store updates (network issue or offline)"
            };
        }

        /// <inheritdoc/>
        public void CacheFetchResult(UpdateCheckContext context, string fetchedVersion)
        {
            if (context == null || string.IsNullOrEmpty(fetchedVersion))
            {
                return;
            }

            EditorPrefs.SetString(context.LastCheckKey, DateTime.Now.ToString("yyyy-MM-dd"));
            EditorPrefs.SetString(context.CachedVersionKey, fetchedVersion);
        }

        /// <inheritdoc/>
        public bool IsNewerVersion(string version1, string version2)
        {
            if (!TryParseVersion(version1, out var left) || !TryParseVersion(version2, out var right))
            {
                return false;
            }

            return CompareVersions(left, right) > 0;
        }

        private static int CompareVersions(ParsedVersion left, ParsedVersion right)
        {
            int cmp = left.Major.CompareTo(right.Major);
            if (cmp != 0) return cmp;

            cmp = left.Minor.CompareTo(right.Minor);
            if (cmp != 0) return cmp;

            cmp = left.Patch.CompareTo(right.Patch);
            if (cmp != 0) return cmp;

            // Stable is newer than prerelease when core version matches.
            if (!left.IsPrerelease && right.IsPrerelease) return 1;
            if (left.IsPrerelease && !right.IsPrerelease) return -1;
            if (!left.IsPrerelease && !right.IsPrerelease) return 0;

            cmp = GetPrereleaseRank(left.PrereleaseLabel).CompareTo(GetPrereleaseRank(right.PrereleaseLabel));
            if (cmp != 0) return cmp;

            cmp = left.PrereleaseNumber.CompareTo(right.PrereleaseNumber);
            if (cmp != 0) return cmp;

            return string.Compare(left.PrereleaseLabel, right.PrereleaseLabel, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetPrereleaseRank(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return 0;
            }

            switch (label.ToLowerInvariant())
            {
                case "a":
                case "alpha":
                    return 1;
                case "b":
                case "beta":
                    return 2;
                case "rc":
                    return 3;
                case "preview":
                case "pre":
                    return 4;
                default:
                    return 5;
            }
        }

        private static bool TryParseVersion(string version, out ParsedVersion parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            string normalized = version.Trim().TrimStart('v', 'V');
            var match = Regex.Match(
                normalized,
                @"^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<label>[A-Za-z]+)(?:\.(?<number>\d+))?)?$");

            if (!match.Success)
            {
                return false;
            }

            if (!int.TryParse(match.Groups["major"].Value, out int major) ||
                !int.TryParse(match.Groups["minor"].Value, out int minor) ||
                !int.TryParse(match.Groups["patch"].Value, out int patch))
            {
                return false;
            }

            string prereleaseLabel = match.Groups["label"].Success ? match.Groups["label"].Value : string.Empty;
            int prereleaseNumber = 0;
            if (match.Groups["number"].Success)
            {
                int.TryParse(match.Groups["number"].Value, out prereleaseNumber);
            }

            parsed = new ParsedVersion
            {
                Major = major,
                Minor = minor,
                Patch = patch,
                PrereleaseLabel = prereleaseLabel,
                PrereleaseNumber = prereleaseNumber,
                IsPrerelease = !string.IsNullOrEmpty(prereleaseLabel)
            };
            return true;
        }

        private static bool IsPreReleaseVersion(string version)
        {
            return !string.IsNullOrWhiteSpace(version) &&
                   version.IndexOf('-', StringComparison.Ordinal) >= 0;
        }

        private static string InferChannelFromVersion(string currentVersion)
        {
            return IsPreReleaseVersion(currentVersion) ? "beta" : "main";
        }

        /// <inheritdoc/>
        public virtual bool IsGitInstallation()
        {
            try
            {
                var packageInfo = PackageInfo.FindForAssembly(typeof(PackageUpdateService).Assembly);
                if (packageInfo != null)
                {
                    if (!string.IsNullOrEmpty(packageInfo.packageId))
                    {
                        return packageInfo.packageId.StartsWith("com.coplaydev.unity-mcp", StringComparison.OrdinalIgnoreCase) &&
                               packageInfo.packageId.Contains("#", StringComparison.Ordinal);
                    }
                }
            }
            catch
            {
                // Background update checks can run off the main thread during editor load.
                // In that path we avoid AssetDatabase-based fallbacks and simply treat the
                // installation type as unknown.
            }

            return false;
        }

        private static UpdateSourceContext ResolveUpdateSource(string currentVersion)
        {
            string channel = InferChannelFromVersion(currentVersion);
            bool isGitInstallation = false;
            bool useGitHubSource = false;

            try
            {
                var packageInfo = PackageInfo.FindForAssembly(typeof(PackageUpdateService).Assembly);
                string packageId = packageInfo?.packageId ?? string.Empty;
                string assetPath = packageInfo?.assetPath ?? string.Empty;

                if (packageId.IndexOf("#beta", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    channel = "beta";
                    isGitInstallation = true;
                    useGitHubSource = true;
                }
                else if (packageId.IndexOf("#main", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    channel = "main";
                    isGitInstallation = true;
                    useGitHubSource = true;
                }
                else if (packageId.Contains("#", StringComparison.Ordinal))
                {
                    isGitInstallation = true;
                    useGitHubSource = true;
                }
                else if (packageId.IndexOf("file:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         assetPath.StartsWith("Packages/com.coplaydev.unity-mcp", StringComparison.OrdinalIgnoreCase))
                {
                    useGitHubSource = true;
                }
            }
            catch
            {
                useGitHubSource = IsPreReleaseVersion(currentVersion);
            }

            return new UpdateSourceContext
            {
                IsGitInstallation = isGitInstallation,
                UseGitHubSource = useGitHubSource,
                Channel = channel
            };
        }

        private struct UpdateSourceContext
        {
            public bool IsGitInstallation;

            public bool UseGitHubSource;

            public string Channel;
        }

        /// <inheritdoc/>
        public void ClearCache()
        {
            EditorPrefs.DeleteKey(LastCheckDateKey);
            EditorPrefs.DeleteKey(CachedVersionKey);
            EditorPrefs.DeleteKey(LastBetaCheckDateKey);
            EditorPrefs.DeleteKey(CachedBetaVersionKey);
            EditorPrefs.DeleteKey(LastAssetStoreCheckDateKey);
            EditorPrefs.DeleteKey(CachedAssetStoreVersionKey);
        }

        /// <summary>
        /// Fetches the latest version from GitHub package.json for the requested branch.
        /// </summary>
        protected virtual string FetchLatestVersionFromGitHub(string branch)
        {
            try
            {
                // GitHub API endpoint (Option 1 - has rate limits):
                // https://api.github.com/repos/CoplayDev/unity-mcp/releases/latest
                //
                // We use Option 2 (package.json directly) because:
                // - No API rate limits (GitHub serves raw files freely)
                // - Simpler - just parse JSON for version field
                // - More reliable - doesn't require releases to be published
                // - Direct source of truth from the main branch

                using (var client = CreateWebClient())
                {
                    client.Headers.Add("User-Agent", "Unity-MCPForUnity-UpdateChecker");
                    string packageJsonUrl = string.Equals(branch, "beta", StringComparison.OrdinalIgnoreCase)
                        ? BetaPackageJsonUrl
                        : MainPackageJsonUrl;
                    string jsonContent = client.DownloadString(packageJsonUrl);

                    var packageJson = JObject.Parse(jsonContent);
                    string version = packageJson["version"]?.ToString();

                    return string.IsNullOrEmpty(version) ? null : version;
                }
            }
            catch (Exception ex)
            {
                // Silent fail - don't interrupt the user if network is unavailable
                McpLog.Info($"Update check failed (this is normal if offline): {ex.Message}");
                return null;
            }
        }

        private struct ParsedVersion
        {
            public int Major;
            public int Minor;
            public int Patch;
            public string PrereleaseLabel;
            public int PrereleaseNumber;
            public bool IsPrerelease;
        }

        /// <summary>
        /// Fetches the latest Asset Store version from a hosted JSON file.
        /// </summary>
        protected virtual string FetchLatestVersionFromAssetStoreJson()
        {
            try
            {
                using (var client = CreateWebClient())
                {
                    client.Headers.Add("User-Agent", "Unity-MCPForUnity-AssetStoreUpdateChecker");
                    string jsonContent = client.DownloadString(AssetStoreVersionUrl);

                    var versionJson = JObject.Parse(jsonContent);
                    string version = versionJson["version"]?.ToString();

                    return string.IsNullOrEmpty(version) ? null : version;
                }
            }
            catch (Exception ex)
            {
                // Silent fail - don't interrupt the user if network is unavailable
                McpLog.Info($"Asset Store update check failed (this is normal if offline): {ex.Message}");
                return null;
            }
        }

        protected virtual WebClient CreateWebClient()
        {
            return new TimeoutWebClient(GetRequestTimeoutMs());
        }

        protected virtual int GetRequestTimeoutMs()
        {
            return DefaultRequestTimeoutMs;
        }

        private sealed class TimeoutWebClient : WebClient
        {
            private readonly int _timeoutMs;

            public TimeoutWebClient(int timeoutMs)
            {
                _timeoutMs = timeoutMs;
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                if (request != null)
                {
                    request.Timeout = _timeoutMs;

                    if (request is HttpWebRequest httpRequest)
                    {
                        httpRequest.ReadWriteTimeout = _timeoutMs;
                    }
                }

                return request;
            }
        }
    }
}
