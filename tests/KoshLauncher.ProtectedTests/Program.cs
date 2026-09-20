using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

internal static class Program
{
    static Assembly assembly = null!;
    static XDocument mapping = null!;
    static Type MappedType(string name)
    {
        var item = mapping.Descendants("renamedClass").FirstOrDefault(x => (string?)x.Attribute("oldName") == "[KoshLauncher]" + name);
        string mappedName = item == null ? name : ((string)item.Attribute("newName")!).Replace("[KoshLauncher]", "");
        return assembly.GetTypes().Single(t => t.FullName == mappedName);
    }
    static object? Call(string type, string method, object? instance, params object[] args)
    {
        string prefix = "[KoshLauncher]" + type + "::" + method + "[" + args.Length + "](";
        var item = mapping.Descendants("renamedMethod").FirstOrDefault(x => ((string?)x.Attribute("oldName"))?.StartsWith(prefix) == true);
        string name = item == null ? method : (string)item.Attribute("newName")!;
        return MappedType(type).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Single(m => m.Name == name && m.GetParameters().Length == args.Length && m.GetParameters().Select((p, i) => p.ParameterType.IsInstanceOfType(args[i])).All(x => x))
            .Invoke(instance, args);
    }
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
    [STAThread]
    static void Main(string[] args)
    {
        string protectedDir = Path.GetFullPath(args[0]);
        string dependencyDir = Path.GetFullPath(args[1]);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string path = Path.Combine(dependencyDir, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(protectedDir, "KoshLauncher.dll"));
        mapping = XDocument.Load(Path.Combine(protectedDir, "Mapping.xml"));
        Check(assembly.GetType("KoshLauncher.OptifineService") == null, "service name obfuscated");
        Check(assembly.GetType("KoshLauncher.MainWindow")!.GetMethod("Jogar_Click", BindingFlags.Instance | BindingFlags.NonPublic) == null, "launch method name obfuscated");
        var curseForgeProjectType = MappedType("KoshLauncher.CurseForgeProject");
        Check(curseForgeProjectType.GetProperty("Name") != null && curseForgeProjectType.GetProperty("Summary") != null &&
            curseForgeProjectType.GetProperty("LogoUrl") != null && curseForgeProjectType.GetProperty("Details") != null,
            "CurseForge card bindings preserved after obfuscation");
        var app = new Application();
        var window = (Window)Activator.CreateInstance(assembly.GetType("KoshLauncher.MainWindow")!)!;
        var settingsType = MappedType("KoshLauncher.LauncherSettings");
        var settings = JsonSerializer.Deserialize("{\"Nickname\":\"Compatibility\",\"MaximumRamMb\":3072}", settingsType)!;
        Check(JsonSerializer.Serialize(settings, settingsType).Contains("\"MaximumRamMb\":3072"), "existing settings JSON contract");
        Call("KoshLauncher.MainWindow", "DefinirOcupado", window, true);
        Check(!((Button)window.FindName("PlayButton")).IsEnabled, "busy state after obfuscation");
        Call("KoshLauncher.MainWindow", "DefinirOcupado", window, false);
        var toggle = (CheckBox)window.FindName("NeonToggle");
        toggle.IsChecked = false;
        toggle.IsChecked = true;
        Check(((TextBlock)window.FindName("BrandText")).Foreground is LinearGradientBrush, "neon event handlers");
        string scratch = Path.Combine(Path.GetTempPath(), "Kosh-protected-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        var root = (FrameworkElement)window.Content;
        window.Content = null;
        var host = new Border { Child = root, Resources = window.Resources };
        foreach (string navigation in new[] { "HomeNav", "InstallNav", "ExploreNav", "CustomizationNav", "SettingsNav" })
        {
            ((Button)window.FindName(navigation)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            foreach (var size in new[] { new Size(1000, 680), new Size(1180, 760) })
            {
                host.Measure(size); host.Arrange(new Rect(size)); host.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(host);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(scratch, navigation + size.Width + ".png")); encoder.Save(file);
            }
        }
        Console.WriteLine("PASS protected WPF navigation and ten renders: " + scratch);
        var catalog = (Array)Call("KoshLauncher.OptifineService", "ParseCatalog", null, "OptiFine_1.20.1_HD_U_I6.jar OptiFine_1.20.1_HD_U_I6.jar")!;
        Check(catalog.Length == 1 && catalog.GetValue(0)!.ToString()!.Contains("1.20.1"), "OptiFine parsing and display after obfuscation");
        var skin = BitmapSource.Create(64, 64, 96, 96, PixelFormats.Bgra32, null, new byte[64 * 64 * 4], 256);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(skin));
        using var bytes = new MemoryStream(); png.Save(bytes);
        Check(((BitmapSource)Call("KoshLauncher.MainWindow", "ValidarSkin", null, bytes.ToArray())!).PixelWidth == 64, "skin validation after obfuscation");
        string secretFile = Path.Combine(scratch, "account.protected");
        var storage = Activator.CreateInstance(MappedType("KoshLauncher.ProtectedAccountStorage"), secretFile)!;
        var node = System.Text.Json.Nodes.JsonNode.Parse("{\"check\":\"private-test\"}")!;
        storage.GetType().GetMethod("Write")!.Invoke(storage, new object?[] { node, null });
        Check(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(secretFile)).Contains("private-test"), "account remains encrypted");
        Check(storage.GetType().GetMethod("ReadAsJsonNode")!.Invoke(storage, null)!.ToString()!.Contains("private-test"), "account decrypts after obfuscation");
        // Do not Show/Close the window: avoid account login, downloads and saving user preferences.
    }
}
