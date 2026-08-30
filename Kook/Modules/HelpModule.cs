using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kook;
using Kook.Commands;

namespace SysBot.ACNHOrders
{
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _service;

        public HelpModule(CommandService service)
        {
            _service = service;
        }

        [Command("help")]
        [Summary("列出可用命令。")]
        [RequireSudo]
        public async Task HelpAsync()
        {
            var uid = Context.User.Id;

            var output = "**以下是您可以使用的命令:**\n\n";

            foreach (var module in _service.Modules)
            {
                string? description = null;
                HashSet<string> mentioned = new();
                foreach (var cmd in module.Commands)
                {
                    var name = cmd.Name;
                    if (mentioned.Contains(name))
                        continue;
                    if (cmd.Attributes.Any(z => z is RequireSudoAttribute) && !Globals.Bot.Config.CanUseSudo(uid))
                        continue;

                    mentioned.Add(name);
                    var result = await cmd.CheckPreconditionsAsync(Context).ConfigureAwait(false);
                    if (result.IsSuccess)
                        description += $"{cmd.Aliases[0]}\n";
                }
                if (string.IsNullOrWhiteSpace(description))
                    continue;

                output += $"**{module.Name}**\n{description}\n";
            }

            await ReplyTextAsync(output).ConfigureAwait(false);
        }

        [Command("help")]
        [Summary("列出有关特定命令的信息。")]
        public async Task HelpAsync([Summary("您需要帮助的命令")] string command)
        {
            var result = _service.Search(Context, command);

            if (!result.IsSuccess)
            {
                await ReplyTextAsync($"抱歉，我找不到类似 **{command}** 的命令。").ConfigureAwait(false);
                return;
            }

            var output = $"**以下是一些类似 {command} 的命令:**\n\n";

            foreach (var match in result.Commands)
            {
                var cmd = match.Command;
                output += $"**{string.Join(", ", cmd.Aliases)}**\n{GetCommandSummary(cmd)}\n\n";
            }

            await ReplyTextAsync(output).ConfigureAwait(false);
        }

        private static string GetCommandSummary(CommandInfo cmd)
        {
            return $"说明: {cmd.Summary}\n参数: {GetParameterSummary(cmd.Parameters)}";
        }

        private static string GetParameterSummary(IReadOnlyList<ParameterInfo> p)
        {
            if (p.Count == 0)
                return "无";
            return $"{p.Count}\n- " + string.Join("\n- ", p.Select(GetParameterSummary));
        }

        private static string GetParameterSummary(ParameterInfo z)
        {
            var result = z.Name;
            if (!string.IsNullOrWhiteSpace(z.Summary))
                result += $" ({z.Summary})";
            return result;
        }
    }
}
