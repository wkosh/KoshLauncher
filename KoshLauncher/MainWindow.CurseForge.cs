using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KoshLauncher;

public partial class MainWindow
{
    private static readonly string[] CurseForgeMinecraftVersions =
    [
        "26.3", "26.2", "26.1.2", "26.1",
        "1.21.11", "1.21.10", "1.21.9", "1.21.8", "1.21.7", "1.21.6", "1.21.5", "1.21.4", "1.21.3", "1.21.2", "1.21.1", "1.21",
        "1.20.6", "1.20.5", "1.20.4", "1.20.3", "1.20.2", "1.20.1", "1.20",
        "1.19.4", "1.19.3", "1.19.2", "1.19.1", "1.19", "1.18.2", "1.18.1", "1.18",
        "1.17.1", "1.17", "1.16.5", "1.16.4", "1.16.3", "1.16.2", "1.16.1", "1.16",
        "1.15.2", "1.15.1", "1.15", "1.14.4", "1.14.3", "1.14.2", "1.14.1", "1.14",
        "1.13.2", "1.13.1", "1.13", "1.12.2", "1.12.1", "1.12", "1.11.2", "1.11.1", "1.11",
        "1.10.2", "1.10.1", "1.10", "1.9.4", "1.9.2", "1.9.1", "1.9", "1.8.9", "1.8.8", "1.8", "1.7.10"
    ];
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        AddHandler(Button.ClickEvent, new RoutedEventHandler(HideExploreWhenNavigating));
    }
    private void HideExploreWhenNavigating(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is Button button && button.Tag is string tag && tag is "Jogar" or "Instalações" or "Personalizar" or "Aparência")
            ExplorePanel.Visibility = Visibility.Collapsed;
    }
    private void NavigateAndCloseExplore_Click(object sender, RoutedEventArgs e)
    {
        ExplorePanel.Visibility = Visibility.Collapsed;
        Navigate_Click(sender, e);
    }
    private void ExploreNavigate_Click(object sender, RoutedEventArgs e)
    {
        PageTitle.Text = "Explorar";
        HomePanel.Visibility = InstallPanel.Visibility = CustomizationPanel.Visibility = SettingsPanel.Visibility = Visibility.Collapsed;
        ExplorePanel.Visibility = Visibility.Visible;
        foreach (var nav in new[] { HomeNav, InstallNav, ExploreNav, CustomizationNav, SettingsNav })
        {
            bool selected = ReferenceEquals(nav, ExploreNav);
            nav.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected ? "#85122D" : "#121015"));
            nav.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selected ? "#D42A4D" : "#292029"));
        }
        string? selectedVersion = VersionComboBox.SelectedItem is string profile ? ExtractMinecraftVersion(profile) : null;
        CurseForgeVersionBox.ItemsSource = CurseForgeMinecraftVersions;
        CurseForgeVersionBox.SelectedItem = selectedVersion != null && CurseForgeMinecraftVersions.Contains(selectedVersion)
            ? selectedVersion : CurseForgeMinecraftVersions[0];
    }
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            var current = source;
            while (current != null && current is not Button) current = VisualTreeHelper.GetParent(current);
            if (current is Button button && button.Tag is string tag && tag is "Jogar" or "Instalações" or "Personalizar" or "Aparência")
                ExplorePanel.Visibility = Visibility.Collapsed;
        }
        base.OnPreviewMouseDown(e);
    }
    private string? ReadCurseForgeKey()
    {
        string? environmentKey = Environment.GetEnvironmentVariable("CURSEFORGE_API_KEY");
        return string.IsNullOrWhiteSpace(environmentKey) ? null : environmentKey.Trim();
    }
    private async void CurseForgeSearch_Click(object sender, RoutedEventArgs e) => await SearchCurseForgeAsync();
    private async void CurseForgeSearch_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; await SearchCurseForgeAsync(); } }
    private async Task SearchCurseForgeAsync()
    {
        string? key = ReadCurseForgeKey();
        if (string.IsNullOrWhiteSpace(key)) { CurseForgeStatus.Text = "A integração com o CurseForge não está disponível nesta compilação."; return; }
        int classId = int.Parse((string)((ComboBoxItem)CurseForgeTypeCombo.SelectedItem).Tag);
        CurseForgeSearchButton.IsEnabled = false; CurseForgeStatus.Text = "Consultando o catálogo do CurseForge...";
        try
        {
            var results = await new CurseForgeService(key).SearchAsync(CurseForgeSearchBox.Text, classId, CurseForgeVersionBox.Text, CancellationToken.None);
            CurseForgeResultsList.ItemsSource = results;
            CurseForgeStatus.Text = results.Count == 0 ? "Nenhum resultado com esses filtros." : $"{results.Count} resultado(s). Escolha um perfil abaixo antes de baixar.";
        }
        catch (Exception ex) { CurseForgeStatus.Text = "Falha na pesquisa: " + ex.Message; }
        finally { CurseForgeSearchButton.IsEnabled = true; }
    }
    private async void InstallCurseForge_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not CurseForgeProject project) return;
        string? key = ReadCurseForgeKey(); if (string.IsNullOrWhiteSpace(key)) return;
        var button = (Button)sender; button.IsEnabled = false; string? temporary = null;
        try
        {
            CurseForgeStatus.Text = $"Procurando a versão compatível de {project.Name}...";
            var service = new CurseForgeService(key);
            if (project.ClassId == 4471)
            {
                DefinirOcupado(true);
                try { await InstallModpackAsync(project, service, CurseForgeVersionBox.Text, CancellationToken.None); }
                finally { DefinirOcupado(false); _60c86214ef(); AtualizarResumoInicial(); }
                return;
            }
            if (VersionComboBox.SelectedItem is not string profile) { MessageBox.Show("Escolha uma instalação na barra inferior antes de baixar.", "CurseForge"); return; }
            string version = string.IsNullOrWhiteSpace(CurseForgeVersionBox.Text) ? ExtractMinecraftVersion(profile) : CurseForgeVersionBox.Text.Trim();
            int? loaderType = GetCurseForgeLoaderType(profile);
            if (project.ClassId == 6 && loaderType == null)
            {
                CurseForgeStatus.Text = "Para instalar mods, selecione um perfil Fabric, Forge, NeoForge ou Quilt na barra inferior.";
                return;
            }
            var file = await service.GetLatestFileAsync(project.Id, version, CancellationToken.None, loaderType);
            string folder = project.ClassId switch { 6552 => "shaderpacks", 12 => "resourcepacks", 17 => "saves", 4471 => "modpacks", _ => "mods" };
            string directory = Path.Combine(_6db5d04762(profile), folder); Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, file.FileName);
            if (File.Exists(target)) { CurseForgeStatus.Text = "Este arquivo já está instalado: " + file.FileName; return; }
            temporary = target + ".download"; await service.DownloadAsync(file.DownloadUrl, temporary, CancellationToken.None);
            if (project.ClassId == 17 && Path.GetExtension(file.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ExtractWorldArchive(temporary, directory); File.Delete(temporary);
            }
            else File.Move(temporary, target);
            temporary = null;
            CurseForgeStatus.Text = $"{project.Name} instalado em {folder} para {profile}.";
        }
        catch (Exception ex) { CurseForgeStatus.Text = "Falha no download: " + ex.Message; }
        finally { if (temporary != null && File.Exists(temporary)) try { File.Delete(temporary); } catch { } button.IsEnabled = true; }
    }
    private static string ExtractMinecraftVersion(string profile)
    {
        var match = System.Text.RegularExpressions.Regex.Match(profile, @"(?<!\d)((?:1\.\d+(?:\.\d+)?|\d{2}\.\d+(?:\.\d+)?))(?!\d)");
        return match.Success ? match.Groups[1].Value : profile;
    }
    private static int? GetCurseForgeLoaderType(string profile) => profile.StartsWith("fabric-loader-", StringComparison.OrdinalIgnoreCase) ? 4
        : profile.Contains("quilt", StringComparison.OrdinalIgnoreCase) ? 5
        : profile.Contains("neoforge", StringComparison.OrdinalIgnoreCase) ? 6
        : profile.Contains("forge", StringComparison.OrdinalIgnoreCase) ? 1 : null;
    private static void ExtractWorldArchive(string archive, string saves)
    {
        using var zip = System.IO.Compression.ZipFile.OpenRead(archive);
        string root = Path.GetFullPath(saves) + Path.DirectorySeparatorChar;
        foreach (var entry in zip.Entries.Where(item => !item.FullName.EndsWith('/')))
        {
            string target = Path.GetFullPath(Path.Combine(saves, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("O mundo contém um caminho inseguro.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open(); using var output = File.Create(target); input.CopyTo(output);
        }
    }
    private bool filteringUnsupportedVersions;
    private void VersionComboBox_FilterUnsupported(object sender, SelectionChangedEventArgs e)
    {
        if (filteringUnsupportedVersions || VersionComboBox.SelectedItem is not string selected ||
            IsVanillaMinecraftVersion(ExtractMinecraftVersion(selected))) return;
        Dispatcher.BeginInvoke(FilterUnsupportedMinecraftVersions);
    }
    private void VersionComboBox_DropDownOpened(object sender, EventArgs e) => FilterUnsupportedMinecraftVersions();
    private void FilterUnsupportedMinecraftVersions()
    {
        if (filteringUnsupportedVersions) return;
        filteringUnsupportedVersions = true;
        try
        {
        string? selected = VersionComboBox.SelectedItem as string;
        var unsupported = VersionComboBox.Items.OfType<string>()
            .Where(item => !IsVanillaMinecraftVersion(ExtractMinecraftVersion(item)))
            .ToArray();
        foreach (string item in unsupported) VersionComboBox.Items.Remove(item);
        if (selected != null && VersionComboBox.Items.Contains(selected)) VersionComboBox.SelectedItem = selected;
        else
        {
            string? preferred = VersionComboBox.Items.OfType<string>().FirstOrDefault(item => ExtractMinecraftVersion(item) == "26.3")
                ?? VersionComboBox.Items.OfType<string>().FirstOrDefault(item => ExtractMinecraftVersion(item) == "26.2")
                ?? VersionComboBox.Items.OfType<string>().FirstOrDefault(item => ExtractMinecraftVersion(item) == "1.21.11")
                ?? VersionComboBox.Items.OfType<string>().FirstOrDefault();
            if (preferred != null) VersionComboBox.SelectedItem = preferred;
        }
        }
        finally { filteringUnsupportedVersions = false; }
    }
    private void OpenCurseForgePage_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is CurseForgeProject project) Process.Start(new ProcessStartInfo(project.PageUrl) { UseShellExecute = true });
    }
}
