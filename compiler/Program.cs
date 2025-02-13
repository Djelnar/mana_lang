using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using insomnia;
using mana.cmd;
using static System.Console;
using static GitVersionInformation;
using static Spectre.Console.AnsiConsole;
using Color = System.Drawing.Color;

[assembly: InternalsVisibleTo("wc_test")]


if (Environment.GetEnvironmentVariable("WT_SESSION") == null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    Environment.SetEnvironmentVariable($"RUNE_EMOJI_USE", "0");
    Environment.SetEnvironmentVariable($"RUNE_COLOR_USE", "0");
    Environment.SetEnvironmentVariable($"RUNE_NIER_USE", "0");
    //Environment.SetEnvironmentVariable($"NO_COLOR", "true");
    //ForegroundColor = ConsoleColor.Gray;
    //Console.WriteLine($"\t@\tno windows-terminal: coloring, emoji and nier has disabled.");
    //ForegroundColor = ConsoleColor.White;
}


if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
{
    MarkupLine("Platform is not supported.");
    return -1;
}
var watch = Stopwatch.StartNew();

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    OutputEncoding = Encoding.Unicode;
JsonConvert.DefaultSettings = () => new JsonSerializerSettings()
{
    NullValueHandling = NullValueHandling.Ignore,
    Formatting = Formatting.Indented,
    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    Culture = CultureInfo.InvariantCulture
};



MarkupLine($"[grey]Mana compiler[/] [red]{AssemblySemFileVer}-{BranchName}+{ShortSha}[/]");
MarkupLine($"[grey]Copyright (C)[/] [cyan3]2021[/] [bold]Yuuki Wesp[/].\n\n");


ColorShim.Apply();

AppFlags.RegisterArgs(ref args);

var app = new CommandApp();



app.Configure(config =>
{
    config.AddCommand<CompileCommand>("compile");
});

var result = app.Run(args);

watch.Stop();

MarkupLine($":sparkles: Done in [lime]{watch.Elapsed.TotalSeconds:00.000}s[/].");

return result;
