using System;
using System.Linq;
using System.Threading.Tasks;
using Kook.Commands;
using Kook.WebSocket;
using NHSE.Core;
using NHSE.Villagers;

namespace SysBot.ACNHOrders
{
    public class VillagerModule : ModuleBase<SocketCommandContext>
    {

        [Command("injectVillager"), Alias("iv")]
        [Summary("根据内部名称注入村民。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task InjectVillagerAsync(int index, string internalName) => await InjectVillagers(index, new string[1] { internalName });


        [Command("injectVillager"), Alias("iv")]
        [Summary("根据内部名称注入村民。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task InjectVillagerAsync(string internalName) => await InjectVillagerAsync(0, internalName).ConfigureAwait(false);

        [Command("multiVillager"), Alias("mvi", "injectVillagerMulti", "superUltraInjectionGiveMeMoreVillagers")]
        [Summary("根据内部名称注入多个村民。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task InjectVillagerMultiAsync([Remainder] string names) => await InjectVillagers(0, names.Split(new string[2] { ",", " ", }, StringSplitOptions.RemoveEmptyEntries));

        private async Task InjectVillagers(int startIndex, string[] villagerNames)
        {
            if (!Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 订单模式下无法注入村民。").ConfigureAwait(false);
                return;
            }

            if (!Globals.Bot.Config.AllowVillagerInjection)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 村民注入当前已禁用。").ConfigureAwait(false);
                return;
            }

            var bot = Globals.Bot;
            int index = startIndex;
            int count = villagerNames.Length;

            if (count < 1)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 命令中没有村民名称").ConfigureAwait(false);
                return;
            }

            foreach (var nameLookup in villagerNames)
            {
                var internalName = nameLookup;
                var nameSearched = internalName;

                if (!VillagerResources.IsVillagerDataKnown(internalName))
                    internalName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, internalName, StringComparison.InvariantCultureIgnoreCase)).Key;

                if (internalName == default)
                {
                    await ReplyTextAsync($"{Context.User.Mention()} - {nameSearched} 不是有效的村民内部名称。");
                    return;
                }

                if (index > byte.MaxValue || index < 0)
                {
                    await ReplyTextAsync($"{Context.User.Mention()} - {index} 不是有效的索引");
                    return;
                }

                int slot = index;

                var replace = VillagerResources.GetVillager(internalName);
                var user = Context.User;
                var mention = Context.User.Mention();

                var extraMsg = string.Empty;
                if (VillagerOrderParser.IsUnadoptable(internalName))
                    extraMsg += " 请注意，您将无法领养此村民。";

                var request = new VillagerRequest(Context.User.Username, replace, (byte)index, GameInfo.Strings.GetVillager(internalName))
                {
                    OnFinish = success =>
                    {
                        var reply = success
                            ? $"{nameSearched} 已由机器人在索引 {slot} 处注入。请去和他们对话！{extraMsg}"
                            : "注入村民失败。请告诉机器人所有者查看日志！";
                        Task.Run(async () => await ReplyTextAsync($"{reply}").ConfigureAwait(false));
                    }
                };

                bot.VillagerInjections.Enqueue(request);

                index = (index + 1) % 10;
            }

            var addMsg = count > 1 ? $"已为 {count} 个村民提交注入请求" : "村民注入请求已";
            var msg = $"{addMsg}添加到队列，将很快注入。完成后我会回复您。";
            await ReplyTextAsync(msg).ConfigureAwait(false);
        }

        [Command("villagers"), Alias("vl", "villagerList")]
        [Summary("打印当前岛上的村民列表。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetVillagerListAsync()
        {
            if (!Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 可以通过将村民添加到您的订单命令中来替换岛上的村民。");
                return;
            }

            await ReplyTextAsync($"以下村民在 {Globals.Bot.TownName}: {Globals.Bot.Villagers.LastVillagers}。").ConfigureAwait(false);
        }


        [Command("villagerName")]
        [Alias("vn", "nv", "name")]
        [Summary("获取村民的内部名称。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetVillagerInternalNameAsync([Summary("搜索使用的语言代码")] string language, [Summary("村民名称")][Remainder] string villagerName)
        {
            var strings = GameInfo.GetStrings(language);
            await ReplyVillagerName(strings, villagerName).ConfigureAwait(false);
        }

        [Command("villagerName")]
        [Alias("vn", "nv", "name")]
        [Summary("获取村民的内部名称。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetVillagerInternalNameAsync([Summary("村民名称")][Remainder] string villagerName)
        {
            var strings = GameInfo.Strings;
            await ReplyVillagerName(strings, villagerName).ConfigureAwait(false);
        }

        private async Task ReplyVillagerName(GameStrings strings, string villagerName)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 查询命令未被接受。");
                return;
            }

            var map = strings.VillagerMap;
            var result = map.FirstOrDefault(z => string.Equals(villagerName, z.Value.Replace(" ", string.Empty), StringComparison.InvariantCultureIgnoreCase));
            if (string.IsNullOrWhiteSpace(result.Key))
            {
                await ReplyTextAsync($"未找到名为 {villagerName} 的村民。").ConfigureAwait(false);
                return;
            }
            await ReplyTextAsync($"{villagerName}={result.Key}").ConfigureAwait(false);
        }
    }
}
