using System.CommandLine;
using Maestro.Tools.Commands;

var rootCommand = new RootCommand("Maestro CLI — generates Maestro.yaml configuration from vatSys data files.");

rootCommand.AddCommand(ExtractStarsCommand.Build());
rootCommand.AddCommand(ExtractPerformanceCommand.Build());

return await rootCommand.InvokeAsync(args);
