using System.Reflection;

Console.WriteLine("Maestro Plugin Loader");
Console.WriteLine("====================");

while (true)
{
    // C:\Users\days_\Documents\vatSys Files\Profiles\Australia\Plugins\MaestroPlugin\Maestro.Plugin.dll
    Console.Write("Enter path to plugin DLL (or 'exit' to quit): ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    try
    {
        Console.WriteLine($"Attempting to load: {input}");

        if (!File.Exists(input))
        {
            Console.WriteLine("❌ File not found!");
            continue;
        }

        // Set working directory to the plugin's directory so it can find dependencies
        var pluginDir = Path.GetDirectoryName(input)!;
        Directory.SetCurrentDirectory(pluginDir);
        Console.WriteLine($"Set working directory to: {pluginDir}");

        // Load the assembly
        Console.WriteLine("Loading assembly...");
        var assembly = Assembly.LoadFrom(input);
        Console.WriteLine($"✅ Assembly loaded: {assembly.FullName}");

        // Find the Plugin type
        Console.WriteLine("Searching for Plugin type...");
        var pluginType = assembly.GetTypes().FirstOrDefault(t => t.Name == "Plugin");

        if (pluginType == null)
        {
            Console.WriteLine("❌ Plugin type not found!");
            continue;
        }
        Console.WriteLine($"✅ Found Plugin type: {pluginType.FullName}");

        // Check for MEF export attribute
        var exportAttr = pluginType.GetCustomAttributes().FirstOrDefault(a => a.GetType().Name == "ExportAttribute");
        if (exportAttr != null)
        {
            Console.WriteLine($"✅ Export attribute found: {exportAttr}");
        }
        else
        {
            Console.WriteLine("⚠️ No Export attribute found");
        }

        // Try to instantiate the plugin
        Console.WriteLine("Creating plugin instance...");
        var plugin = Activator.CreateInstance(pluginType);
        Console.WriteLine($"✅ Plugin instance created successfully: {plugin?.GetType().Name}");

        // Get the plugin name if it has one
        var nameProperty = pluginType.GetProperty("Name");
        if (nameProperty != null)
        {
            var name = nameProperty.GetValue(plugin)?.ToString();
            Console.WriteLine($"Plugin Name: {name}");
        }

    }
    catch (ReflectionTypeLoadException ex)
    {
        Console.WriteLine($"❌ ReflectionTypeLoadException: {ex.Message}");
        Console.WriteLine("Loader exceptions:");
        foreach (var loaderEx in ex.LoaderExceptions.Where(e => e != null))
        {
            Console.WriteLine($"  - {loaderEx!.Message}");
        }
    }
    catch (FileLoadException ex)
    {
        Console.WriteLine($"❌ FileLoadException: {ex.Message}");
        Console.WriteLine($"File name: {ex.FileName}");
        Console.WriteLine($"Fusion log: {ex.FusionLog}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Exception: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
    }

    Console.WriteLine(new string('-', 50));
}
