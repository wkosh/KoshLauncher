using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace KoshLauncher;

public partial class MainWindow
{
    private async Task InstallModpackAsync(CurseForgeProject project, CurseForgeService service, string versionFilter, CancellationToken token)
    {
        string staging = Path.Combine(Path.GetTempPath(), "KoshLauncher", "modpacks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        string? instanceDirectory = null;
        try
        {
            CurseForgeStatus.Text = "Baixando e lendo o manifesto do modpack...";
            var archive = await service.GetLatestFileAsync(project.Id, versionFilter, token);
            string zipPath = Path.Combine(staging, "modpack.zip");
            await service.DownloadAsync(archive.DownloadUrl, zipPath, token);
            using var zip = ZipFile.OpenRead(zipPath);
            var entry = zip.GetEntry("manifest.json") ?? throw new InvalidDataException("Este arquivo não contém manifest.json do CurseForge.");
            await using var manifestStream = entry.Open();
            using var manifest = await JsonDocument.ParseAsync(manifestStream, cancellationToken: token);
            var minecraft = manifest.RootElement.GetProperty("minecraft");
            string minecraftVersion = minecraft.GetProperty("version").GetString() ?? throw new InvalidDataException("O modpack não informou a versão do Minecraft.");
            string loaderId = minecraft.GetProperty("modLoaders").EnumerateArray().Select(x => x.GetProperty("id").GetString()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? throw new InvalidDataException("O modpack não informou um loader.");
            CurseForgeStatus.Text = "Preparando " + loaderId + " para Minecraft " + minecraftVersion + "...";
            string loaderProfile = await EnsureLoaderProfileAsync(minecraftVersion, loaderId, token);
            string instanceId = CreateModpackInstanceId(project.Slug, minecraftVersion);
            CloneLoaderProfile(loaderProfile, instanceId);
            instanceDirectory = _6db5d04762(instanceId);
            ExtractOverrides(zip, instanceDirectory);
            var files = manifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
            string mods = Path.Combine(instanceDirectory, "mods"); Directory.CreateDirectory(mods);
            int completed = 0;
            foreach (var fileRef in files)
            {
                token.ThrowIfCancellationRequested();
                int projectId = fileRef.GetProperty("projectID").GetInt32(); int fileId = fileRef.GetProperty("fileID").GetInt32();
                var file = await service.GetFileAsync(projectId, fileId, token);
                string target = Path.Combine(mods, file.FileName);
                if (!File.Exists(target)) await service.DownloadAsync(file.DownloadUrl, target, token);
                completed++;
                CurseForgeStatus.Text = $"Instalando modpack: {completed}/{files.Length} arquivos...";
            }
            SelectInstalledProfile(instanceId);
            CurseForgeStatus.Text = $"{project.Name} pronto: perfil próprio criado com {completed} arquivo(s).";
        }
        catch
        {
            if (instanceDirectory != null) { try { Directory.Delete(instanceDirectory, true); } catch { } }
            throw;
        }
        finally { try { Directory.Delete(staging, true); } catch { } }
    }

    private string CreateModpackInstanceId(string slug, string minecraftVersion)
    {
        string safe = new string(slug.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray()).Trim('-');
        if (string.IsNullOrWhiteSpace(safe)) safe = "modpack";
        string id = "Kosh-" + safe[..Math.Min(safe.Length, 36)] + "-" + minecraftVersion;
        int suffix = 2; string candidate = id;
        while (Directory.Exists(Path.Combine(_a4b4895acc, "Minecraft", "versions", candidate))) candidate = id + "-" + suffix++;
        return candidate;
    }

    private void CloneLoaderProfile(string sourceProfile, string instanceId)
    {
        string root = Path.Combine(_a4b4895acc, "Minecraft", "versions");
        string source = Path.Combine(root, sourceProfile); string target = Path.Combine(root, instanceId);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("Perfil base não encontrado: " + sourceProfile);
        Directory.CreateDirectory(target);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            if (relative.Equals(sourceProfile + ".json", StringComparison.OrdinalIgnoreCase)) relative = instanceId + ".json";
            string destination = Path.Combine(target, relative); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(file, destination, false);
        }
        string jsonPath = Path.Combine(target, instanceId + ".json");
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(jsonPath)) ?? throw new InvalidDataException("Perfil do loader inválido.");
        node["id"] = instanceId;
        File.WriteAllText(jsonPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void ExtractOverrides(ZipArchive zip, string destination)
    {
        string root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        foreach (var entry in zip.Entries.Where(item => item.FullName.StartsWith("overrides/", StringComparison.OrdinalIgnoreCase) && !item.FullName.EndsWith('/')))
        {
            string relative = entry.FullName["overrides/".Length..].Replace('/', Path.DirectorySeparatorChar);
            string target = Path.GetFullPath(Path.Combine(destination, relative));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("O modpack contém um caminho inseguro.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
        }
    }
}
