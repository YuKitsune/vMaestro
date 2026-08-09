using System.CommandLine;
using Maestro.Tools.Commands;

var rootCommand = new RootCommand("Maestro CLI — generates Maestro.yaml configuration from vatSys data files.");

rootCommand.Subcommands.Add(ExtractStarsCommand.Build());
rootCommand.Subcommands.Add(VisualizeCommand.Build());

return await rootCommand.Parse(args).InvokeAsync();
