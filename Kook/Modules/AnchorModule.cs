using System.Threading;
using System.Threading.Tasks;
using Kook.Commands;

namespace SysBot.ACNHOrders
{
    public class AnchorModule : ModuleBase<SocketCommandContext>
    {
        [Command("setAnchor")]
        [Summary("设置队列循环所需的锚点之一。")]
        [RequireSudo]
        public async Task SetAnchorAsync(int anchorId)
        {
            var bot = Globals.Bot;
            await Task.Delay(2_000, CancellationToken.None).ConfigureAwait(false);
            var success = await bot.UpdateAnchor(anchorId, CancellationToken.None).ConfigureAwait(false);
            var msg = success ? $"成功更新锚点 {anchorId}。" : $"无法更新锚点 {anchorId}。";
            await ReplyTextAsync(msg).ConfigureAwait(false);
        }

        [Command("loadAnchor")]
        [Summary("加载队列循环所需的锚点之一。仅用于测试，请确保处于正确场景，否则游戏可能崩溃。")]
        [RequireSudo]
        public async Task SendAnchorBytesAsync(int anchorId)
        {
            var bot = Globals.Bot;
            await Task.Delay(2_000, CancellationToken.None).ConfigureAwait(false);
            var success = await bot.SendAnchorBytes(anchorId, CancellationToken.None).ConfigureAwait(false);
            var msg = success ? $"成功将玩家设置到锚点 {anchorId}。" : $"无法将玩家设置到锚点 {anchorId}。";
            await ReplyTextAsync(msg).ConfigureAwait(false);
        }
    }
}
