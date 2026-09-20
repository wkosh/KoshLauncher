using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CmlLib.Core.ModLoaders.FabricMC;

namespace KoshLauncher;

public partial class FabricInstallDialog : Window
{
    private readonly Func<string, Task<IReadOnlyCollection<FabricLoader>>> loaderProvider;
    public string MinecraftVersion => MinecraftVersionBox.SelectedItem as string ?? "";
    public string LoaderVersion => LoaderVersionBox.SelectedItem as string ?? "";

    public FabricInstallDialog(IEnumerable<string> minecraftVersions, Func<string, Task<IReadOnlyCollection<FabricLoader>>> loaderProvider, string? preferredMinecraft)
    {
        InitializeComponent();
        this.loaderProvider = loaderProvider;
        MinecraftVersionBox.ItemsSource = minecraftVersions.ToArray();
        MinecraftVersionBox.SelectedItem = preferredMinecraft != null && MinecraftVersionBox.Items.Contains(preferredMinecraft)
            ? preferredMinecraft : MinecraftVersionBox.Items.Cast<string>().FirstOrDefault();
    }

    private async void MinecraftVersionBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshLoadersAsync();
    private async Task RefreshLoadersAsync()
    {
        if (LoaderVersionBox == null || MinecraftVersionBox.SelectedItem is not string minecraftVersion) return;
        string? previous = LoaderVersionBox.SelectedItem as string;
        LoaderVersionBox.ItemsSource = null; LoaderVersionBox.IsEnabled = false; InstallActionButton.IsEnabled = false;
        HintText.Text = "Consultando versões compatíveis...";
        FabricLoader[] loaders;
        try { loaders = (await loaderProvider(minecraftVersion)).ToArray(); }
        catch { HintText.Text = "Não foi possível consultar o catálogo do Fabric."; return; }
        string[] versions = loaders.OrderByDescending(item => item.Stable).ThenByDescending(item => item.Build).Select(item => item.Version).OfType<string>().Distinct().ToArray();
        LoaderVersionBox.ItemsSource = versions;
        LoaderVersionBox.SelectedItem = previous != null && versions.Contains(previous) ? previous : versions.FirstOrDefault();
        LoaderVersionBox.IsEnabled = versions.Length > 0; InstallActionButton.IsEnabled = versions.Length > 0;
        HintText.Text = versions.Length > 0 ? "A versão estável mais recente é selecionada automaticamente." : "Não há Fabric Loader compatível com esta versão.";
    }
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void Install_Click(object sender, RoutedEventArgs e)
    {
        if (MinecraftVersion.Length == 0 || LoaderVersion.Length == 0) { HintText.Text = "Escolha as duas versões antes de continuar."; return; }
        DialogResult = true; Close();
    }
}
