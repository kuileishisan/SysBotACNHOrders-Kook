using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Kook;
using Kook.Commands;

namespace SysBot.ACNHOrders
{
    public class InfoModule : ModuleBase<SocketCommandContext>
    {
        private const string detail = "我是一个基于 SysBot.NET、NHSE、ACNHMS 和其他开源软件的开源 Kook 机器人。";
        private const string repo = "https://github.com/berichan/SysBot.ACNHOrders";

        [Command("info")]
        [Alias("about", "whoami", "owner")]
        [RequireSudo]
        public async Task InfoAsync()
        {
            var info = $"**关于我**\n{detail}\n\n" +
                $"- 源代码: {repo}\n" +
                $"- 库: Kook.Net\n" +
                $"- 运行时间: {GetUptime()}\n" +
                $"- 运行环境: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture} " +
                $"({RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture})\n" +
                $"- 构建时间: {GetBuildTime()}\n\n" +
                $"**统计**\n" +
                $"- 堆内存: {GetHeapSize()}MiB\n" +
                $"- 服务器: {Context.Client.Guilds.Count}\n" +
                $"- 频道: {Context.Client.Guilds.Sum(g => g.Channels.Count)}\n" +
                $"- 用户: {Context.Client.Guilds.Sum(g => g.Users.Count)}\n";

            await ReplyTextAsync(info).ConfigureAwait(false);
        }

        private static string GetUptime() => (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss");
        private static string GetHeapSize() => Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2).ToString(CultureInfo.CurrentCulture);

        private static string GetBuildTime()
        {
            var assembly = Assembly.GetEntryAssembly();
            if (assembly == null)
                return DateTime.Now.ToString(@"yy-MM-dd\.hh\:mm");
            return File.GetLastWriteTime(assembly.Location).ToString(@"yy-MM-dd\.hh\:mm");
        }
    }
}
