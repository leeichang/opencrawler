using System.CommandLine;
using OpenCrawler.Cli.Commands;

var root = new RootCommand("openCrawler CLI");
root.AddCommand(DownloadCommand.Build());

return await root.InvokeAsync(args);
