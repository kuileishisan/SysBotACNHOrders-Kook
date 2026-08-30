using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kook;
using Kook.Commands;
using NHSE.Core;

namespace SysBot.ACNHOrders
{
    public class ItemModule : ModuleBase<SocketCommandContext>
    {
        [Command("lookupLang")]
        [Alias("ll")]
        [Summary("获取包含请求字符串的物品列表。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task SearchItemsAsync([Summary("搜索使用的语言代码")] string language, [Summary("物品名称 / 物品子串")][Remainder] string itemName)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 查询命令未被接受。");
                return;
            }
            var strings = GameInfo.GetStrings(language).ItemDataSource;
            await PrintItemsAsync(itemName, strings).ConfigureAwait(false);
        }

        [Command("lookup")]
        [Alias("li", "search")]
        [Summary("获取包含请求字符串的物品列表。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task SearchItemsAsync([Summary("物品名称 / 物品子串")][Remainder] string itemName)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 查询命令未被接受。");
                return;
            }
            var strings = GameInfo.Strings.ItemDataSource;
            await PrintItemsAsync(itemName, strings).ConfigureAwait(false);
        }

        private async Task PrintItemsAsync(string itemName, IReadOnlyList<ComboItem> strings)
        {
            const int minLength = 2;
            if (itemName.Length <= minLength)
            {
                await ReplyTextAsync($"请输入长度超过 {minLength} 个字符的搜索词。").ConfigureAwait(false);
                return;
            }

            var exact = ItemParser.GetItem(itemName, strings);
            if (!exact.IsNone)
            {
                var msg = $"{exact.ItemId:X4} {itemName}";
                if (msg == "02F8 vine")
                {
                    msg = "3107 vine";
                }
                if (msg == "02F7 glowing moss")
                {
                    msg = "3106 glowing moss";
                }
                await ReplyTextAsync($"`{msg}`").ConfigureAwait(false);
                return;
            }

            var matches = ItemParser.GetItemsMatching(itemName, strings).ToArray();
            var result = string.Join(Environment.NewLine, matches.Select(z => $"{z.Value:X4} {z.Text}"));

            if (result.Length == 0)
            {
                await ReplyTextAsync("未找到匹配项。").ConfigureAwait(false);
                return;
            }

            const int maxLength = 500;
            if (result.Length > maxLength)
            {
                var ordered = matches.OrderBy(z => LevenshteinDistance.Compute(z.Text, itemName));
                result = string.Join(Environment.NewLine, ordered.Select(z => $"{z.Value:X4} {z.Text}"));
                result = result.Substring(0, maxLength) + "...[已截断]";
            }

            await ReplyTextAsync($"`{result}`").ConfigureAwait(false);
        }

        [Command("item")]
        [Summary("获取物品信息。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetItemInfoAsync([Summary("物品ID（十六进制）")] string itemHex)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 查询命令未被接受。");
                return;
            }

            ushort itemID = ItemParser.GetID(itemHex);
            if (itemID == Item.NONE)
            {
                await ReplyTextAsync("请求的物品无效。").ConfigureAwait(false);
                return;
            }

            var name = GameInfo.Strings.GetItemName(itemID);
            var result = ItemInfo.GetItemInfo(itemID);
            if (result.Length == 0)
                await ReplyTextAsync($"请求的物品（{name}）没有可用的自定义数据。").ConfigureAwait(false);
            else
                await ReplyTextAsync($"{name}:\r\n{result}").ConfigureAwait(false);
        }

        [Command("stack")]
        [Summary("堆叠物品并打印十六进制代码。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task StackAsync([Summary("物品ID（十六进制）")] string itemHex, [Summary("堆叠中的物品数量")] int count)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 查询命令未被接受。");
                return;
            }

            ushort itemID = ItemParser.GetID(itemHex);
            if (itemID == Item.NONE || count < 1 || count > 99)
            {
                await ReplyTextAsync("请求的物品无效。").ConfigureAwait(false);
                return;
            }

            var ct = count - 1;
            var item = new Item(itemID) { Count = (ushort)ct };
            var msg = ItemParser.GetItemText(item);
            await ReplyTextAsync(msg).ConfigureAwait(false);
        }

        [Command("customize")]
        [Summary("自定义物品并打印十六进制代码。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task CustomizeAsync([Summary("物品ID（十六进制）")] string itemHex, [Summary("第一个自定义值")] int cust1, [Summary("第二个自定义值")] int cust2)
            => await CustomizeAsync(itemHex, cust1 + cust2).ConfigureAwait(false);

        [Command("customize")]
        [Summary("自定义物品并打印十六进制代码。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task CustomizeAsync([Summary("物品ID（十六进制）")] string itemHex, [Summary("自定义值总和")] int sum)
        {
            if (!Globals.Bot.Config.AllowLookup)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 查询命令未被接受。");
                return;
            }

            ushort itemID = ItemParser.GetID(itemHex);
            if (itemID == Item.NONE)
            {
                await ReplyTextAsync("请求的物品无效。").ConfigureAwait(false);
                return;
            }
            if (sum <= 0)
            {
                await ReplyTextAsync("未指定自定义数据。").ConfigureAwait(false);
                return;
            }

            var remake = ItemRemakeUtil.GetRemakeIndex(itemID);
            if (remake < 0)
            {
                await ReplyTextAsync("请求的物品没有可用的自定义数据。").ConfigureAwait(false);
                return;
            }

            int body = sum & 7;
            int fabric = sum >> 5;
            if (fabric > 7 || ((fabric << 5) | body) != sum)
            {
                await ReplyTextAsync("指定的自定义数据无效。").ConfigureAwait(false);
                return;
            }

            var info = ItemRemakeInfoData.List[remake];
            bool hasBody = body == 0 || body <= info.ReBodyPatternNum;
            bool hasFabric = fabric == 0 || info.GetFabricDescription(fabric) != "Invalid";

            if (!hasBody || !hasFabric)
                await ReplyTextAsync("请求的物品自定义似乎无效。").ConfigureAwait(false);

            var item = new Item(itemID) { BodyType = body, PatternChoice = fabric };
            var msg = ItemParser.GetItemText(item);
            await ReplyTextAsync(msg).ConfigureAwait(false);
        }
    }
}
