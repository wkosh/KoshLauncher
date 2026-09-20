using System.Net.Http;
using System.IO;
using System.Windows;
using CmlLib.Core;
using CmlLib.Core.ModLoaders.FabricMC;

namespace KoshLauncher;

public partial class MainWindow
{
    private async void ChooseFabric_Click(object sender, RoutedEventArgs e)
    {
        if (_f351f16348) return;
        DefinirOcupado(true);
        try
        {
            AccountStatus.Text = "Consultando versões do Fabric...";
            using var http = new HttpClient();
            var installer = new FabricInstaller(http);
            var minecraftVersions = await installer.GetSupportedVersionNames();
            string? preferred = VersionComboBox.SelectedItem is string selected && IsVanillaMinecraftVersion(selected) ? selected : null;
            var dialog = new FabricInstallDialog(minecraftVersions, installer.GetLoaders, preferred) { Owner = this };
            DefinirOcupado(false);
            if (dialog.ShowDialog() != true) { AccountStatus.Text = "Instalação do Fabric cancelada."; return; }
            DefinirOcupado(true);
            string root = Path.Combine(_a4b4895acc, "Minecraft");
            AccountStatus.Text = $"Instalando Fabric Loader {dialog.LoaderVersion} para Minecraft {dialog.MinecraftVersion}...";
            string profile = await installer.Install(dialog.MinecraftVersion, dialog.LoaderVersion, new MinecraftPath(root));
            SelectInstalledProfile(profile);
            _60c86214ef(); AtualizarResumoInicial();
            AccountStatus.Text = $"Fabric {dialog.LoaderVersion} pronto para Minecraft {dialog.MinecraftVersion}.";
        }
        catch (Exception ex)
        {
            AccountStatus.Text = "Não foi possível instalar o Fabric.";
            MessageBox.Show(ex.Message, "Fabric", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { DefinirOcupado(false); }
    }
}
