using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kook;
using Kook.Commands;
using Kook.WebSocket;
using NHSE.Core;
using SysBot.Base;

namespace SysBot.ACNHOrders
{
    public class DropModule : ModuleBase<SocketCommandContext>
    {
        private static int MaxRequestCount => Globals.Bot.Config.DropConfig.MaxDropCount;

        [Command("clean")]
        [Summary("捡起机器人周围的物品。")]
        public async Task RequestCleanAsync()
        {
            if (!await GetDropAvailability().ConfigureAwait(false))
                return;

            if (!Globals.Bot.Config.AllowClean)
            {
                await ReplyTextAsync("清理功能当前已禁用。").ConfigureAwait(false);
                return;
            }
            Globals.Bot.CleanRequested = true;
            await ReplyTextAsync("清理请求将很快执行。").ConfigureAwait(false);
        }

        [Command("code")]
        [Alias("dodo")]
        [Summary("打印岛屿的 Dodo 代码。")]
        [RequireSudo]
        public async Task RequestDodoCodeAsync()
        {
            var draw = Globals.Bot.DodoImageDrawer;
            var txt = $"{Globals.Bot.TownName} 的 Dodo 代码: {Globals.Bot.DodoCode}。";
            if (draw != null)
            {
                var path = draw.GetProcessedDodoImagePath();
                if (path != null)
                {
                    await Context.Channel.SendFileAsync(path, txt);
                    return;
                }
            }

            await ReplyTextAsync(txt).ConfigureAwait(false);
        }

        [Command("sendDodo")]
        [Alias("sd", "send")]
        [Summary("打印岛屿的 Dodo 代码。仅在 dodo 恢复模式下有效。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestRestoreLoopDodoAsync()
        {
            var cfg = Globals.Bot.Config;
            var MesUser = Context.User.Username;
            Globals.Bot.DisUserID = ($"{Context.User.Id}");
            if (!Globals.Bot.Config.DodoModeConfig.AllowSendDodo && !Globals.Bot.Config.CanUseSudo(Context.User.Id) && Globals.Self.Owner != Context.User.Id)
                return;
            if (!Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
                return;

            string[] Checklist = File.ReadAllLines("banlist.txt", Encoding.UTF8);
            int indexS = Array.FindIndex(Checklist, row => row.Contains(Context.User.Id.ToString()));
            if (indexS != -1)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 您当前被禁止使用机器人。将不会发送 Dodo 代码。");
                return;
            }
            try
            {
                if (cfg.FieldLayerName != "name")
                {
                    var MapFile = ($"{Globals.Bot.Config.FieldLayerNHLDirectory}/{Globals.Bot.CLayer}.png");
                    await Context.User.SendTextAsync($"{Globals.Bot.TownName} 的 Dodo 代码: {Globals.Bot.DodoCode}。\n{Globals.Bot.TownName} 当前设置为以下层: {Globals.Bot.CLayer}。").ConfigureAwait(false);
                    if (File.Exists($"{MapFile}"))
                    {
                        await Context.User.SendFileAsync($"{MapFile}");
                    }
                    await ReplyTextAsync($"`{MesUser}`: 已通过私信发送 Dodo 代码");
                    await Globals.Self.TrySpeakMessage(Globals.Bot.Config.DodoModeConfig.SentDodoChannels, $"[{DateTime.Now:MM-dd hh:mm:ss tt}] Dodo 代码已发送给 (met){Context.User.Id}(met) - {Context.User.Id} 来自 `{Context.Guild.Name}` 服务器。").ConfigureAwait(false);
                }
                else
                {
                    await Context.User.SendTextAsync($"{Globals.Bot.TownName} 的 Dodo 代码: {Globals.Bot.DodoCode}。").ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await ReplyTextAsync($"{ex.Message}: 必须开启私信才能使用此命令。我不会在此频道泄露 Dodo 代码！");
                return;
            }
        }

        private const string DropItemSummary =
            "请求机器人丢弃用户提供的物品。" +
            "十六进制模式: 物品ID（十六进制）; 多个物品用空格分隔。" +
            "文本模式: 物品名称; 多个物品用逗号分隔。如需使用其他语言解析，请先输入语言代码和逗号，然后是物品。";

        [Command("drop")]
        [Alias("dropItem")]
        [Summary("丢弃自定义物品（或多个物品）。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestDropAsync([Summary(DropItemSummary)][Remainder] string request)
        {
            var cfg = Globals.Bot.Config;
            var items = ItemParser.GetItemsFromUserInput(request, cfg.DropConfig, cfg.DropConfig.UseLegacyDrop ? ItemDestination.PlayerDropped : ItemDestination.HeldItem);

            MultiItem.StackToMax(items);
            await DropItems(items).ConfigureAwait(false);
        }

        private const string DropDIYSummary =
            "请求机器人丢弃用户提供的 DIY 配方。" +
            "十六进制模式: DIY 配方ID（十六进制）; 多个用空格分隔。" +
            "文本模式: DIY 配方物品名称; 多个用逗号分隔。如需使用其他语言解析，请先输入语言代码和逗号，然后是配方。";

        [Command("dropDIY")]
        [Alias("diy")]
        [Summary("丢弃请求的配方ID的 DIY 配方。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestDropDIYAsync([Summary(DropDIYSummary)][Remainder] string recipeIDs)
        {
            var items = ItemParser.GetDIYsFromUserInput(recipeIDs);
            await DropItems(items).ConfigureAwait(false);
        }

        [Command("setTurnips")]
        [Alias("turnips")]
        [Summary("将本周所有大头菜价格（周日除外）设置为指定值。")]
        [RequireSudo]
        public async Task RequestTurnipSetAsync(int value)
        {
            var bot = Globals.Bot;
            bot.StonkRequests.Enqueue(new TurnipRequest(Context.User.Username, value)
            {
                OnFinish = success =>
                {
                    var reply = success
                        ? $"所有大头菜价格已成功设置为 {value}！"
                        : "灾难性失败。";
                    Task.Run(async () => await ReplyTextAsync($"{Context.User.Mention()}: {reply}").ConfigureAwait(false));
                }
            });
            await ReplyTextAsync($"已将所有大头菜价格排队设置为 {value}。");
        }

        [Command("setTurnipsMax")]
        [Alias("turnipsMax", "stonks")]
        [Summary("将本周所有大头菜价格（周日除外）设置为 999,999,999")]
        [RequireSudo]
        public async Task RequestTurnipMaxSetAsync() => await RequestTurnipSetAsync(999999999);

        private async Task DropItems(IReadOnlyCollection<Item> items)
        {
            if (!await GetDropAvailability().ConfigureAwait(false))
                return;

            if (!InternalItemTool.CurrentInstance.IsSaneAfterCorrection(items, Globals.Bot.Config.DropConfig))
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 您试图丢弃会损坏存档的物品。丢弃请求未被接受。");
                return;
            }

            if (items.Count > MaxRequestCount)
            {
                var clamped = $"用户每条命令最多 {MaxRequestCount} 个物品。请负责任地使用此机器人。";
                await ReplyTextAsync(clamped).ConfigureAwait(false);
                items = items.Take(MaxRequestCount).ToArray();
            }

            var requestInfo = new ItemRequest(Context.User.Username, items);
            Globals.Bot.Injections.Enqueue(requestInfo);

            var msg = $"物品丢弃请求{(requestInfo.Item.Count > 1 ? "s" : string.Empty)}将很快执行。";
            await ReplyTextAsync(msg).ConfigureAwait(false);
        }

        private async Task<bool> GetDropAvailability()
        {
            var cfg = Globals.Bot.Config;

            if (cfg.CanUseSudo(Context.User.Id) || Globals.Self.Owner == Context.User.Id)
                return true;

            if (Globals.Bot.CurrentUserId == Context.User.Id)
                return true;

            if (!cfg.AllowDrop)
            {
                await ReplyTextAsync($"AllowDrop 当前设置为 false。");
                return false;
            }
            else if (!cfg.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 仅在您的订单期间在岛上时，且仅当您在订单中遗漏了某些物品时，才允许使用此命令。");
                return false;
            }

            return true;
        }
    }
}
