using System.Net.Http;
using System.IO;
using System.Windows;
using CmlLib.Core;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.QuiltMC;

namespace KoshLauncher;

public partial class MainWindow
{
    private async void InstalarForge_Click(object sender, RoutedEventArgs e) => await InstallLoaderAsync("Forge");
    private async void InstalarNeoForge_Click(object sender, RoutedEventArgs e) => await InstallLoaderAsync("NeoForge");
    private async void InstalarQuilt_Click(object sender, RoutedEventArgs e) => await InstallLoaderAsync("Quilt");

    private async Task InstallLoaderAsync(string loader)
    {
        if (_f351f16348 || VersionComboBox.SelectedItem is not string minecraftVersion) return;
        if (!IsVanillaMinecraftVersion(minecraftVersion))
        {
            MessageBox.Show("Selecione uma versão Vanilla antes de instalar " + loader + ".", loader); return;
        }
        DefinirOcupado(true);
        SetLoaderButtons(false);
        try
        {
            string root = Path.Combine(_a4b4895acc, "Minecraft");
            var minecraft = new MinecraftLauncher(new MinecraftPath(root));
            AccountStatus.Text = "Preparando Minecraft " + minecraftVersion + "...";
            await minecraft.InstallAsync(minecraftVersion);
            if (loader == "Quilt")
            {
                AccountStatus.Text = "Instalando Quilt...";
                using var http = new HttpClient();
                var installer = new QuiltInstaller(http);
                string profile = await installer.Install(minecraftVersion, new MinecraftPath(root));
                SelectInstalledProfile(profile);
            }
            else
            {
                var version = await minecraft.GetVersionAsync(minecraftVersion);
                string java = minecraft.GetJavaPath(version) ?? minecraft.GetDefaultJavaPath() ?? throw new FileNotFoundException("Java não encontrado.");
                AccountStatus.Text = "Instalando " + loader + "...";
                var before = _1016c867ff().ToHashSet(StringComparer.Ordinal);
                if (loader == "Forge") await LoaderInstallerService.InstallForgeAsync(minecraftVersion, root, java, CancellationToken.None);
                else await LoaderInstallerService.InstallNeoForgeAsync(minecraftVersion, root, java, CancellationToken.None);
                string profile = FindInstalledLoaderProfile(before, loader, minecraftVersion)
                    ?? throw new InvalidOperationException("O instalador concluiu, mas não criou um perfil local reconhecível.");
                SelectInstalledProfile(profile);
            }
            AccountStatus.Text = loader + " pronto. Clique em JOGAR.";
        }
        catch (Exception ex) { AccountStatus.Text = "Não foi possível instalar " + loader + "."; MessageBox.Show(ex.Message, loader, MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { DefinirOcupado(false); SetLoaderButtons(true); _60c86214ef(); AtualizarResumoInicial(); }
    }

    private void SelectInstalledProfile(string profile)
    {
        if (!VersionComboBox.Items.Contains(profile)) VersionComboBox.Items.Add(profile);
        VersionComboBox.SelectedItem = profile; _ca0459865b();
    }
    private static bool IsVanillaMinecraftVersion(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, @"^(?:1\.\d+(?:\.\d+)?|\d{2}\.\d+(?:\.\d+)?)$");
    private string? FindInstalledLoaderProfile(HashSet<string> before, string loader, string minecraftVersion)
    {
        string marker = loader.Equals("NeoForge", StringComparison.OrdinalIgnoreCase) ? "neoforge" : loader.ToLowerInvariant();
        string[] installed = _1016c867ff().ToArray();
        return installed.FirstOrDefault(item => !before.Contains(item) && item.Contains(marker, StringComparison.OrdinalIgnoreCase))
            ?? installed.FirstOrDefault(item => item.Contains(marker, StringComparison.OrdinalIgnoreCase) && item.Contains(minecraftVersion, StringComparison.OrdinalIgnoreCase));
    }
    private void SetLoaderButtons(bool enabled)
    {
        InstallFabricButton.IsEnabled = enabled && !_f351f16348;
        InstallForgeButton.IsEnabled = enabled && !_f351f16348;
        InstallNeoForgeButton.IsEnabled = enabled && !_f351f16348;
        InstallQuiltButton.IsEnabled = enabled && !_f351f16348;
    }

    private async Task<string> EnsureLoaderProfileAsync(string minecraftVersion, string loaderId, CancellationToken token)
    {
        string root = Path.Combine(_a4b4895acc, "Minecraft");
        var minecraft = new MinecraftLauncher(new MinecraftPath(root));
        await minecraft.InstallAsync(minecraftVersion, token);
        var before = _1016c867ff().ToHashSet(StringComparer.Ordinal);
        if (loaderId.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase))
        {
            using var http = new HttpClient();
            await new FabricInstaller(http).Install(minecraftVersion, new MinecraftPath(root));
        }
        else if (loaderId.StartsWith("quilt-", StringComparison.OrdinalIgnoreCase))
        {
            using var http = new HttpClient();
            await new QuiltInstaller(http).Install(minecraftVersion, new MinecraftPath(root));
        }
        else
        {
            var version = await minecraft.GetVersionAsync(minecraftVersion, token);
            string java = minecraft.GetJavaPath(version) ?? minecraft.GetDefaultJavaPath() ?? throw new FileNotFoundException("Java não encontrado.");
            if (loaderId.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
                await LoaderInstallerService.InstallForgeVersionAsync(minecraftVersion + "-" + loaderId[6..], root, java, token);
            else if (loaderId.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
                await LoaderInstallerService.InstallNeoForgeVersionAsync(loaderId[9..], root, java, token);
            else throw new InvalidOperationException("Loader do modpack não é suportado: " + loaderId);
        }
        string loaderName = loaderId.Split('-')[0];
        return FindInstalledLoaderProfile(before, loaderName, minecraftVersion)
            ?? _1016c867ff().FirstOrDefault(item => item.Contains(minecraftVersion, StringComparison.OrdinalIgnoreCase) && item.Contains(loaderId.Split('-')[0], StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("O loader foi instalado, mas o perfil não foi localizado.");
    }
}
