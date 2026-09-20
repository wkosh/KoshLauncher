using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace KoshLauncher;

internal static class LoaderInstallerService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(3) };

    internal static async Task<string> InstallForgeAsync(string minecraftVersion, string minecraftRoot, string javaPath, CancellationToken token)
    {
        string metadata = await Client.GetStringAsync("https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml", token);
        string version = LatestVersion(metadata, value => value.StartsWith(minecraftVersion + "-", StringComparison.Ordinal));
        string url = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{version}/forge-{version}-installer.jar";
        return await DownloadAndRunInstallerAsync("Forge", url, minecraftRoot, javaPath, token);
    }
    internal static Task<string> InstallForgeVersionAsync(string fullVersion, string minecraftRoot, string javaPath, CancellationToken token) =>
        DownloadAndRunInstallerAsync("Forge", $"https://maven.minecraftforge.net/net/minecraftforge/forge/{fullVersion}/forge-{fullVersion}-installer.jar", minecraftRoot, javaPath, token);

    internal static async Task<string> InstallNeoForgeAsync(string minecraftVersion, string minecraftRoot, string javaPath, CancellationToken token)
    {
        string metadata = await Client.GetStringAsync("https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml", token);
        string prefix = minecraftVersion.StartsWith("1.", StringComparison.Ordinal) ? minecraftVersion[2..] + "." : minecraftVersion + ".";
        string version = LatestVersion(metadata, value => value.StartsWith(prefix, StringComparison.Ordinal));
        string url = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{version}/neoforge-{version}-installer.jar";
        return await DownloadAndRunInstallerAsync("NeoForge", url, minecraftRoot, javaPath, token);
    }
    internal static Task<string> InstallNeoForgeVersionAsync(string version, string minecraftRoot, string javaPath, CancellationToken token) =>
        DownloadAndRunInstallerAsync("NeoForge", $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{version}/neoforge-{version}-installer.jar", minecraftRoot, javaPath, token);

    private static string LatestVersion(string xml, Func<string, bool> predicate)
    {
        var versions = XDocument.Parse(xml).Descendants("version").Select(x => x.Value.Trim()).Where(predicate).ToArray();
        if (versions.Length == 0) throw new InvalidOperationException("Não existe uma edição compatível deste loader para " + "a versão escolhida.");
        return versions[^1];
    }

    private static async Task<string> DownloadAndRunInstallerAsync(string loaderName, string url, string minecraftRoot, string javaPath, CancellationToken token)
    {
        string staging = Path.Combine(Path.GetTempPath(), "KoshLauncher", loaderName + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        string installer = Path.Combine(staging, "installer.jar");
        try
        {
            Directory.CreateDirectory(minecraftRoot);
            string launcherProfiles = Path.Combine(minecraftRoot, "launcher_profiles.json");
            if (!File.Exists(launcherProfiles))
                await File.WriteAllTextAsync(launcherProfiles, "{\"profiles\":{},\"settings\":{},\"version\":3}", token);
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(token))
            await using (var output = File.Create(installer)) await input.CopyToAsync(output, token);
            var start = new ProcessStartInfo(javaPath) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = minecraftRoot, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("-jar"); start.ArgumentList.Add(installer); start.ArgumentList.Add("--installClient"); start.ArgumentList.Add(minecraftRoot);
            using var process = Process.Start(start) ?? throw new IOException("Não foi possível iniciar o instalador do " + loaderName + ".");
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(); Task<string> stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(token); string errors = await stderr; string outputText = await stdout;
            if (process.ExitCode != 0)
            {
                string details = string.Join(Environment.NewLine, new[] { errors, outputText }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
                if (details.Length > 1200) details = details[^1200..];
                throw new IOException(loaderName + " não concluiu a instalação." + (details.Length == 0 ? "" : Environment.NewLine + details));
            }
            return loaderName;
        }
        finally { try { Directory.Delete(staging, true); } catch { } }
    }
}
