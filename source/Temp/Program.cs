// See https://aka.ms/new-console-template for more information

using Newtonsoft.Json;
using Temp;

Console.WriteLine("Where is Maestro.json?");

var path = Console.ReadLine();

var json = File.ReadAllText(path);
var v1 = JsonConvert.DeserializeObject<PluginConfigurationV1>(
    json,
    new Newtonsoft.Json.Converters.StringEnumConverter());

var v2 = ConfigurationConverter.ConvertPluginConfiguration(v1);
var v2Json = JsonConvert.SerializeObject(
    v2,
    Formatting.Indented,
    new Newtonsoft.Json.Converters.StringEnumConverter());

var v2Path = Path.Combine(Path.GetDirectoryName(path), "Maestro-v2.json");
File.WriteAllText(v2Path, v2Json);

Console.WriteLine($"V2 config written to {v2Path}");
