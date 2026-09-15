using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using System.Text.Json.Nodes;
using KoshLauncher;

class Smoke
{
    static BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
    static T Control<T>(MainWindow w, string n) where T : class => (T)w.FindName(n);
    static object? Call(MainWindow w, string n, params object[] args) =>
        typeof(MainWindow).GetMethod(n, flags)!.Invoke(w, args);
    static void Assert(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }

    [STAThread]
    static void Main()
    {
        OptifineChecks.Run();
        var app = new Application();

        var window = new MainWindow();
        var versions = Control<ComboBox>(window, "VersionComboBox");
        versions.Items.Add("1.20.1");
        versions.SelectedIndex = 0;
        Call(window, "AtualizarResumoInicial");
        Assert(Control<TextBlock>(window, "SelectedVersionText").Text == "1.20.1", "selected vanilla summary");
        versions.Items.Add("fabric-loader-0.16.0-1.20.1");
        versions.SelectedIndex = 1;
        Call(window, "AtualizarResumoInicial");
        Assert(Control<TextBlock>(window, "SelectedVersionText").Text.Contains("fabric-loader"), "selected fabric summary");
        Call(window, "DefinirOcupado", true);
        Assert(!Control<Button>(window, "PlayButton").IsEnabled &&
            !Control<Button>(window, "InstallFabricButton").IsEnabled &&
            !Control<TextBox>(window, "NicknameTextBox").IsEnabled, "busy controls locked");
        Call(window, "DefinirOcupado", false);
        Assert(Control<Button>(window, "PlayButton").IsEnabled, "busy controls restored");
        Control<CheckBox>(window, "NeonToggle").IsChecked = false;
        Assert(Control<TextBlock>(window, "BrandText").Foreground is SolidColorBrush, "neon disabled");
        Control<CheckBox>(window, "NeonToggle").IsChecked = true;
        Assert(Control<TextBlock>(window, "BrandText").Foreground is LinearGradientBrush, "neon enabled");
        var settings = JsonSerializer.Deserialize<LauncherSettings>("{\"Nickname\":\"Player\",\"Version\":\"1.20.1\",\"MaximumRamMb\":4096}")!;
        Assert(settings.ScreenWidth == 1280 && settings.NeonEnabled, "old settings compatible");
        settings.ScreenWidth = 1920; settings.FullScreen = true;
        var restored = JsonSerializer.Deserialize<LauncherSettings>(JsonSerializer.Serialize(settings))!;
        Assert(restored.ScreenWidth == 1920 && restored.FullScreen, "new settings round trip");
        var storageType = typeof(MainWindow).Assembly.GetType("KoshLauncher.ProtectedAccountStorage")!;
        string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".protected");
        try
        {
            var storage = Activator.CreateInstance(storageType, temp)!;
            storageType.GetMethod("Write")!.Invoke(storage, new object?[] { JsonNode.Parse("{\"test\":\"sentinel-secret\"}")!, null });
            Assert(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(temp)).Contains("sentinel-secret"), "account file encrypted");
            var decoded = (JsonNode)storageType.GetMethod("ReadAsJsonNode")!.Invoke(storage, null)!;
            Assert(decoded["test"]!.GetValue<string>() == "sentinel-secret", "account encryption round trip");
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }

        string scratch = Path.Combine(Path.GetTempPath(), "KoshLauncher-check-" + Guid.NewGuid());
        Directory.CreateDirectory(scratch);
        using (var reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("--accessToken TEST_SECRET\nnormal output\n"))))
        {
            var task = (System.Threading.Tasks.Task)Call(window, "GravarLogAsync", reader, Path.Combine(scratch, "log.txt"))!;
            task.GetAwaiter().GetResult();
            string text = File.ReadAllText(Path.Combine(scratch, "log.txt"));
            Assert(!text.Contains("TEST_SECRET") && text.Contains("normal output"), "log token redaction");
        }
        using (var reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("drain me\n"))))
        {
            var task = (System.Threading.Tasks.Task)Call(window, "GravarLogAsync", reader, Path.Combine(scratch, "missing", "log.txt"))!;
            task.GetAwaiter().GetResult();
            Assert(typeof(MainWindow).GetField("logFailure", flags)!.GetValue(window) != null, "log write failure does not block drain");
        }
        using (var cancel = new System.Threading.CancellationTokenSource())
        {
            cancel.Cancel();
            var launcher = new CmlLib.Core.MinecraftLauncher(new CmlLib.Core.MinecraftPath(Path.Combine(scratch, "mc")));
            bool cancelled = false;
            try { launcher.InstallAsync("1.20.1", cancel.Token).AsTask().GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { cancelled = true; }
            Assert(cancelled, "installer observes cancellation");
        }
        var validateSkin = typeof(MainWindow).GetMethod("ValidarSkin", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (int height in new[] { 64, 32 })
        {
            var skin = BitmapSource.Create(64, height, 96, 96, PixelFormats.Bgra32, null, new byte[64 * height * 4], 64 * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(skin));
            using var data = new MemoryStream();
            encoder.Save(data);
            var result = (BitmapSource)validateSkin.Invoke(null, new object[] { data.ToArray() })!;
            Assert(result.PixelHeight == height, "valid skin height " + height);
        }
        bool invalidSkin = false;
        try { validateSkin.Invoke(null, new object[] { new byte[64] }); }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { invalidSkin = true; }
        Assert(invalidSkin, "invalid PNG rejected");
        Assert(!Control<Button>(window, "ApplySkinButton").IsEnabled, "skin upload disabled without account");
        string fakeJar = Path.Combine(scratch, "OptiFine_1.20.1_HD_U_TEST.jar");
        using (var zip = System.IO.Compression.ZipFile.Open(fakeJar, System.IO.Compression.ZipArchiveMode.Create))
            zip.CreateEntry("optifine/Installer.class");
        var validateInstaller = typeof(MainWindow).GetMethod("ValidarInstaladorOptifine", BindingFlags.NonPublic | BindingFlags.Static)!;
        validateInstaller.Invoke(null, new object[] { fakeJar, "1.20.1" });
        bool wrongVersion = false;
        try { validateInstaller.Invoke(null, new object[] { fakeJar, "1.20.4" }); }
        catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { wrongVersion = true; }
        Assert(wrongVersion, "mismatched OptiFine version rejected");
        var optifineService = typeof(MainWindow).Assembly.GetType("KoshLauncher.OptifineService")!;
        var catalog = optifineService.GetMethod("ParseCatalog", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { "OptiFine_1.20.1_HD_U_I6.jar preview_OptiFine_1.21_HD_U_J1_pre1.jar" });
        typeof(MainWindow).GetField("optifineCatalog", flags)!.SetValue(window, catalog);
        Call(window, "FilterOptifine_Click", window, new RoutedEventArgs());
        Assert(Control<ComboBox>(window, "OptifineVersionCombo").Items.Count == 1, "OptiFine previews hidden by default");
        Control<CheckBox>(window, "OptifinePreviewToggle").IsChecked = true;
        Call(window, "FilterOptifine_Click", window, new RoutedEventArgs());
        Assert(Control<ComboBox>(window, "OptifineVersionCombo").Items.Count == 2, "OptiFine previews available on request");
        Call(window, "DefinirOcupado", true);
        Assert(!Control<Button>(window, "OptifineInstallButton").IsEnabled && !Control<Button>(window, "ImportOptifineButton").IsEnabled, "OptiFine actions locked during operation");
        Call(window, "DefinirOcupado", false);
        Control<CheckBox>(window, "OptifinePreviewToggle").IsChecked = false;
        Call(window, "FilterOptifine_Click", window, new RoutedEventArgs());
        Control<TextBlock>(window, "OptifineStatus").Text = "Escolha uma edição e clique em Instalar OptiFine.";
        string output = Path.Combine(Path.GetTempPath(), "KoshLauncher-smoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var root = (FrameworkElement)window.Content;
        window.Content = null;
        var host = new Border { Child = root, Background = new SolidColorBrush(Color.FromRgb(9,9,11)) };
        host.Resources = window.Resources;
        System.Windows.Documents.TextElement.SetForeground(host, new SolidColorBrush(Color.FromRgb(238,232,234)));
        foreach (string page in new[] { "Jogar", "Instalações", "Aparência", "Personalizar" })
        {
            Call(window, "Navigate_Click", new Button { Tag = page }, new RoutedEventArgs());
            foreach (var size in new[] { (1180, 760), (1000, 680) })
            {
                window.Width = size.Item1; window.Height = size.Item2;
                host.Measure(new Size(size.Item1, size.Item2));
                host.Arrange(new Rect(0, 0, size.Item1, size.Item2));
                host.UpdateLayout();
                var bitmap = new RenderTargetBitmap(size.Item1, size.Item2, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(host);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, $"{page}-{size.Item1}.png"));
                encoder.Save(file);
            }
        }
        Console.WriteLine("PASS eight page renders: " + output);
        // Do not invoke window closing/save during a read-only smoke check.
    }
}





