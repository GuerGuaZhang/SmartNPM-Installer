using System;
using System.Threading.Tasks;
using Spectre.Console;
using SmartNPM_Installer.Services;

namespace SmartNPM_Installer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var repl = new ReplEngine();
                await repl.StartAsync();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Program error: {EscapeMarkup(ex.Message)}[/]");
                AnsiConsole.MarkupLine($"[grey]{EscapeMarkup(ex.StackTrace ?? "")}[/]");
                AnsiConsole.MarkupLine("\n[yellow]Press Enter to exit...[/]");
                Console.ReadLine();
            }
        }

        /// <summary>
        /// 转义 Spectre.Console 标记字符
        /// </summary>
        private static string EscapeMarkup(string text)
        {
            return text.Replace("[", "[[").Replace("]", "]]");
        }
    }
}
