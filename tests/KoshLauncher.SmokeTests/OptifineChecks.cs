using System.Reflection;
using System.IO;
using System.Threading;
using KoshLauncher;

internal static class OptifineChecks
{
    private static readonly Type Service = typeof(MainWindow).Assembly.GetType("KoshLauncher.OptifineService")!;
    private static object Invoke(string method, params object[] args) => Service.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!;
    public static void Run()
    {
        var releases = (Array)Invoke("ParseCatalog", "f=OptiFine_1.20.1_HD_U_I6.jar f=OptiFine_1.20.1_HD_U_I6.jar f=preview_OptiFine_1.21_HD_U_J1_pre1.jar");
        if (releases.Length != 2) throw new Exception("Catalog deduplication failed");
        foreach (string invalid in new[] { "../OptiFine_1.20.1_HD_U_I6.jar", "OptiFine_1.20.1_HD_U_I6.jar.exe", "OptiFine_1.20.1_../x.jar" })
        {
            try { Invoke("ParseFileName", invalid); throw new Exception("Unsafe filename accepted"); }
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { }
        }
        Console.WriteLine("PASS OptiFine catalog parsing, deduplication and path validation");
        string? root = Environment.GetEnvironmentVariable("KOSH_OPTIFINE_TEST_ROOT");
        if (root == null) return;
        string java = Environment.GetEnvironmentVariable("KOSH_OPTIFINE_TEST_JAVA")!;
        var catalogTask = (Task)Invoke("GetCatalogAsync", CancellationToken.None);
        catalogTask.GetAwaiter().GetResult();
        var catalog = (Array)catalogTask.GetType().GetProperty("Result")!.GetValue(catalogTask)!;
        if (catalog.Length == 0) throw new Exception("Empty live catalog");
        Console.WriteLine("PASS live official catalog: " + catalog.Length);
        var release = Invoke("ParseFileName", "OptiFine_1.20.1_HD_U_I6.jar");
        string staging = Path.Combine(root, "staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        string jar = Path.Combine(staging, "OptiFine_1.20.1_HD_U_I6.jar");
        ((Task)Invoke("DownloadAsync", release, jar, CancellationToken.None)).GetAwaiter().GetResult();
        var install = (Task<string>)Invoke("InstallAsync", root, staging, jar, java, release, CancellationToken.None);
        string profile = install.GetAwaiter().GetResult();
        var mc = new CmlLib.Core.MinecraftLauncher(new CmlLib.Core.MinecraftPath(root));
        var metadata = mc.GetVersionAsync(profile).AsTask().GetAwaiter().GetResult();
        if (metadata == null || !File.Exists(Path.Combine(root, "versions", profile, profile + ".json"))) throw new Exception("Missing profile");
        Console.WriteLine("PASS official OptiFine install and CmlLib profile resolution: " + profile);
    }
}
