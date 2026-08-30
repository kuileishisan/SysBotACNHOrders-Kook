using System.IO;
using System.Threading.Tasks;
using Kook.Commands;

namespace SysBot.ACNHOrders
{
    public class MapModule : ModuleBase<SocketCommandContext>
    {
        [Command("loadLayer")]
        [Summary("将当前刷新层更改为新的 .nhl 地面物品层")]
        [RequireSudo]
        public async Task SetFieldLayerAsync(string filename)
        {
            var bot = Globals.Bot;

            if (!bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await ReplyTextAsync($"此命令仅在 dodo 恢复模式且刷新地图开启时使用。").ConfigureAwait(false);
                return;
            }

            var bytes = bot.ExternalMap.GetNHL(filename);

            if (bytes == null)
            {
                await ReplyTextAsync($"文件 {filename} 不存在或没有正确的 .nhl 扩展名。").ConfigureAwait(false);
                return;
            }

            var req = new MapOverrideRequest(Context.User.Username, bytes, filename);
            bot.MapOverrides.Enqueue(req);

            await ReplyTextAsync($"地图刷新层设置为: {Path.GetFileNameWithoutExtension(filename)}。").ConfigureAwait(false);
            Globals.Bot.CLayer = ($"{Path.GetFileNameWithoutExtension(filename)}");
        }
    }
}
