using System;
using System.Threading.Tasks;
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"程序异常退出: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
