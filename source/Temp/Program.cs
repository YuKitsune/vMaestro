// See https://aka.ms/new-console-template for more information

using Newtonsoft.Json;
using Temp;

Console.WriteLine("Where is Maestro.json?");

var path = Console.ReadLine();
if (string.IsNullOrWhiteSpace(path))
{
    Console.WriteLine("No path provided");
    return;
}

var json = File.ReadAllText(path);
var v1 = JsonConvert.DeserializeObject<PluginConfigurationV1>(
    json,
    new Newtonsoft.Json.Converters.StringEnumConverter());

if (v1 == null)
{
    Console.WriteLine("Failed to deserialize V1 configuration");
    return;
}

var v2 = ConfigurationConverter.ConvertPluginConfiguration(v1);

// Write V2 JSON
var v2Json = JsonConvert.SerializeObject(
    v2,
    Formatting.Indented,
    new Newtonsoft.Json.Converters.StringEnumConverter());

var v2JsonPath = Path.Combine(Path.GetDirectoryName(path)!, "Maestro-v2.json");
File.WriteAllText(v2JsonPath, v2Json);
Console.WriteLine($"V2 JSON config written to {v2JsonPath}");

// Write V2 YAML
var v2Yaml = YamlWriter.SaveToYaml(v2);
var v2YamlPath = Path.Combine(Path.GetDirectoryName(path)!, "Maestro.yaml");
File.WriteAllText(v2YamlPath, v2Yaml);
Console.WriteLine($"V2 YAML config written to {v2YamlPath}");
