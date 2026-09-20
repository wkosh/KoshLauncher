using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace KoshLauncher;

public partial class MainWindow
{
    private async void ApplyOfflineSkin_Click(object sender, RoutedEventArgs e)
    {
        if (_f351f16348) return;
        if (_91a781ff39 == null) { SkinStatus.Text = "Escolha uma imagem de skin primeiro."; return; }
        if (VersionComboBox.SelectedItem is not string profile) { SkinStatus.Text = "Selecione um perfil Fabric na barra inferior."; return; }
        if (!profile.StartsWith("fabric-loader-", StringComparison.OrdinalIgnoreCase))
        {
            SkinStatus.Text = "A skin offline precisa de um perfil Fabric. Crie ou selecione um em Instalações.";
            return;
        }
        string nickname = NicknameTextBox.Text.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(nickname, @"\A[A-Za-z0-9_]{3,16}\z"))
        {
            SkinStatus.Text = "Informe um nick offline válido antes de aplicar a skin.";
            return;
        }
        ApplyOfflineSkinButton.IsEnabled = false;
        string? temporary = null;
        try
        {
            SkinStatus.Text = "Instalando o suporte de skin offline...";
            string version = ExtractMinecraftVersion(profile);
            var modFile = await GetOfflineSkinModAsync(version, CancellationToken.None);
            string instance = _6db5d04762(profile);
            string mods = Path.Combine(instance, "mods"); Directory.CreateDirectory(mods);
            string modTarget = Path.Combine(mods, modFile.FileName);
            if (!File.Exists(modTarget))
            {
                temporary = modTarget + ".download";
                await DownloadOfflineSkinModAsync(modFile.Url, temporary, CancellationToken.None);
                File.Move(temporary, modTarget); temporary = null;
            }
            string skins = Path.Combine(instance, "CustomSkinLoader", "LocalSkin", "skins");
            Directory.CreateDirectory(skins);
            File.WriteAllBytes(Path.Combine(skins, nickname + ".png"), _91a781ff39);
            SkinStatus.Text = $"Skin offline aplicada para {nickname} em {profile}. Reabra o jogo para carregar.";
        }
        catch (Exception ex) { SkinStatus.Text = "Não foi possível aplicar a skin offline: " + ex.Message; }
        finally
        {
            if (temporary != null && File.Exists(temporary)) try { File.Delete(temporary); } catch { }
            ApplyOfflineSkinButton.IsEnabled = true;
        }
    }

    private static async Task<(string FileName, string Url)> GetOfflineSkinModAsync(string minecraftVersion, CancellationToken token)
    {
        string loaders = Uri.EscapeDataString("[\"fabric\"]");
        string versions = Uri.EscapeDataString(JsonSerializer.Serialize(new[] { minecraftVersion }));
        string endpoint = $"https://api.modrinth.com/v2/project/customskinloader/version?loaders={loaders}&game_versions={versions}";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KoshLauncher/1.0.0");
        using var response = await client.GetAsync(endpoint, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        foreach (var release in document.RootElement.EnumerateArray())
        {
            foreach (var file in release.GetProperty("files").EnumerateArray())
            {
                string? name = file.GetProperty("filename").GetString();
                string? url = file.GetProperty("url").GetString();
                if (name?.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) == true &&
                    Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
                    uri.Host.Equals("cdn.modrinth.com", StringComparison.OrdinalIgnoreCase)) return (name, url!);
            }
        }
        throw new InvalidOperationException("CustomSkinLoader não possui uma edição Fabric para Minecraft " + minecraftVersion + ".");
    }

    private static async Task DownloadOfflineSkinModAsync(string url, string destination, CancellationToken token)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KoshLauncher/1.0.0");
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = File.Create(destination);
        byte[] buffer = new byte[81920]; long total = 0; int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            total += read;
            if (total > 32 * 1024 * 1024) throw new InvalidDataException("O arquivo do suporte de skin excedeu o tamanho esperado.");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
        }
        if (total < 1024) throw new InvalidDataException("O arquivo do suporte de skin está incompleto.");
    }
}
