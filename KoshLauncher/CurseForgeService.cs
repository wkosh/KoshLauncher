using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.IO;
using System.Reflection;

namespace KoshLauncher;

internal sealed class CurseForgeService
{
    private readonly HttpClient client;
    internal CurseForgeService(string apiKey)
    {
        client = new HttpClient { BaseAddress = new Uri("https://api.curseforge.com"), Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KoshLauncher/0.5");
    }
    internal async Task<IReadOnlyList<CurseForgeProject>> SearchAsync(string query, int classId, string? version, CancellationToken token)
    {
        var args = new List<string> { "gameId=432", $"classId={classId}", "sortField=2", "sortOrder=desc", "pageSize=30" };
        if (!string.IsNullOrWhiteSpace(query)) args.Add("searchFilter=" + Uri.EscapeDataString(query.Trim()));
        if (!string.IsNullOrWhiteSpace(version)) args.Add("gameVersion=" + Uri.EscapeDataString(version.Trim()));
        using var response = await client.GetAsync("/v1/mods/search?" + string.Join("&", args), token);
        await EnsureSuccessAsync(response, token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token));
        var projects = new List<CurseForgeProject>();
        foreach (var item in json.RootElement.GetProperty("data").EnumerateArray())
        {
            string? logo = item.TryGetProperty("logo", out var l) && l.ValueKind == JsonValueKind.Object && l.TryGetProperty("thumbnailUrl", out var t) ? t.GetString() : null;
            projects.Add(new(item.GetProperty("id").GetInt32(), item.GetProperty("name").GetString() ?? "Projeto sem nome",
                item.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "", item.TryGetProperty("slug", out var g) ? g.GetString() ?? "" : "",
                logo, classId, item.TryGetProperty("downloadCount", out var d) ? d.GetDouble() : 0));
        }
        return projects;
    }
    internal async Task<CurseForgeFile> GetLatestFileAsync(int modId, string? version, CancellationToken token, int? modLoaderType = null)
    {
        string path = $"/v1/mods/{modId}/files?pageSize=50" + (string.IsNullOrWhiteSpace(version) ? "" : "&gameVersion=" + Uri.EscapeDataString(version.Trim()));
        if (modLoaderType.HasValue) path += "&modLoaderType=" + modLoaderType.Value;
        using var response = await client.GetAsync(path, token);
        await EnsureSuccessAsync(response, token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token));
        var files = json.RootElement.GetProperty("data").EnumerateArray().ToArray();
        if (files.Length == 0) throw new InvalidOperationException("Nenhum arquivo compatível foi encontrado para esta versão.");
        var file = files.OrderByDescending(x => x.GetProperty("fileDate").GetDateTimeOffset()).First();
        int id = file.GetProperty("id").GetInt32();
        string name = Path.GetFileName(file.GetProperty("fileName").GetString() ?? $"curseforge-{id}.bin");
        string? url = file.TryGetProperty("downloadUrl", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            using var urlResponse = await client.GetAsync($"/v1/mods/{modId}/files/{id}/download-url", token);
            await EnsureSuccessAsync(urlResponse, token);
            using var urlJson = JsonDocument.Parse(await urlResponse.Content.ReadAsStreamAsync(token));
            url = urlJson.RootElement.GetProperty("data").GetString();
        }
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("O autor não disponibilizou download direto pela API.");
        return new(id, name, url);
    }
    internal async Task<CurseForgeFile> GetFileAsync(int modId, int fileId, CancellationToken token)
    {
        using var response = await client.GetAsync($"/v1/mods/{modId}/files/{fileId}", token);
        await EnsureSuccessAsync(response, token);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(token));
        var file = json.RootElement.GetProperty("data");
        string name = Path.GetFileName(file.GetProperty("fileName").GetString() ?? $"curseforge-{fileId}.bin");
        string? url = file.TryGetProperty("downloadUrl", out var direct) && direct.ValueKind == JsonValueKind.String ? direct.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            using var urlResponse = await client.GetAsync($"/v1/mods/{modId}/files/{fileId}/download-url", token);
            await EnsureSuccessAsync(urlResponse, token);
            using var urlJson = JsonDocument.Parse(await urlResponse.Content.ReadAsStreamAsync(token));
            url = urlJson.RootElement.GetProperty("data").GetString();
        }
        if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("Um arquivo do modpack não possui download direto pela API.");
        return new CurseForgeFile(fileId, name, url);
    }
    internal async Task DownloadAsync(string url, string destination, CancellationToken token)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        await EnsureSuccessAsync(response, token);
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await input.CopyToAsync(output, token);
    }
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new InvalidOperationException("A chave do CurseForge não tem permissão para downloads. Solicite acesso para launcher de terceiros no CurseForge for Studios.");
        throw new HttpRequestException($"CurseForge respondeu {(int)response.StatusCode}: " + await response.Content.ReadAsStringAsync(token));
    }
}

[Obfuscation(Exclude = true, ApplyToMembers = true)]
internal sealed record CurseForgeProject(int Id, string Name, string Summary, string Slug, string? LogoUrl, int ClassId, double DownloadCount)
{
    public string Details => DownloadCount >= 1_000_000 ? $"{DownloadCount / 1_000_000:0.#} mi downloads" : $"{DownloadCount / 1_000:0.#} mil downloads";
    public string PageUrl => $"https://www.curseforge.com/minecraft/{(ClassId == 4471 ? "modpacks" : ClassId == 6552 ? "shaders" : ClassId == 12 ? "texture-packs" : ClassId == 17 ? "worlds" : "mc-mods")}/{Slug}";
}
internal sealed record CurseForgeFile(int Id, string FileName, string DownloadUrl);
