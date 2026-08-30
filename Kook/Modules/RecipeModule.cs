using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kook;
using Kook.Commands;
using NHSE.Core;

namespace SysBot.ACNHOrders
{
    public class RecipeModule : ModuleBase<SocketCommandContext>
    {
        [Command("recipeLang")]
        [Alias("rl")]
        [Summary("获取包含请求物品名称字符串的 DIY 配方ID列表。")]
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

        [Command("recipe")]
        [Alias("ri", "searchDIY")]
        [Summary("获取包含请求物品名称字符串的 DIY 配方ID列表。")]
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

            foreach (var item in strings)
            {
                if (!string.Equals(item.Text, itemName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!ItemParser.InvertedRecipeDictionary.TryGetValue((ushort)item.Value, out var recipeID))
                {
                    await ReplyTextAsync("请求的物品不是 DIY 配方。").ConfigureAwait(false);
                    return;
                }

                var msg = $"{item.Value:X4} {item.Text}: 配方订单代码: {recipeID:X3}000016A2";
                await ReplyTextAsync($"`{msg}`").ConfigureAwait(false);
                return;
            }

            var items = ItemParser.GetItemsMatching(itemName, strings).ToArray();
            var matches = new List<string>();
            foreach (var item in items)
            {
                if (!ItemParser.InvertedRecipeDictionary.TryGetValue((ushort)item.Value, out var recipeID))
                    continue;

                var msg = $"{item.Value:X4} {item.Text}: 配方订单代码: {recipeID:X3}000016A2";
                matches.Add(msg);
            }

            var result = string.Join(Environment.NewLine, matches);
            if (result.Length == 0)
            {
                await ReplyTextAsync("未找到匹配项。").ConfigureAwait(false);
                return;
            }

            const int maxLength = 500;
            if (result.Length > maxLength)
                result = result.Substring(0, maxLength) + "...[已截断]";

            await ReplyTextAsync($"`{result}`").ConfigureAwait(false);
        }
    }
}
