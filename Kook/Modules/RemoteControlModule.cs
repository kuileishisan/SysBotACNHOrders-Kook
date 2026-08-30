using Kook.Commands;
using SysBot.Base;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.ACNHOrders
{
    public class RemoteControlModule : ModuleBase<SocketCommandContext>
    {
        private static CrossBot Bot => Globals.Bot;

        [Command("click")]
        [Summary("点击指定按钮。")]
        [RequireSudo]
        public async Task ClickAsync(SwitchButton b)
        {
            await ClickAsyncImpl(b).ConfigureAwait(false);
        }

        [Command("setStick")]
        [Summary("将摇杆设置到指定位置。")]
        [RequireSudo]
        public async Task SetStickAsync(SwitchStick s, short x, short y, ushort ms = 1_000)
        {
            await SetStickAsyncImpl(s, x, y, ms).ConfigureAwait(false);
        }

        private async Task ClickAsyncImpl(SwitchButton button)
        {
            var b = Globals.Bot;
            await b.Connection.SendAsync(SwitchCommand.Click(button, b.UseCRLF), CancellationToken.None).ConfigureAwait(false);
            await ReplyTextAsync($"{b.Connection.Name} 已执行: {button}").ConfigureAwait(false);
        }

        private async Task SetStickAsyncImpl(SwitchStick s, short x, short y, ushort ms)
        {
            if (!Enum.IsDefined(typeof(SwitchStick), s))
            {
                await ReplyTextAsync($"未知摇杆: {s}").ConfigureAwait(false);
                return;
            }

            var b = Bot;
            await b.Connection.SendAsync(SwitchCommand.SetStick(s, x, y, b.UseCRLF), CancellationToken.None).ConfigureAwait(false);
            await ReplyTextAsync($"{b.Connection.Name} 已执行: {s}").ConfigureAwait(false);
            await Task.Delay(ms).ConfigureAwait(false);
            await b.Connection.SendAsync(SwitchCommand.ResetStick(s, b.UseCRLF), CancellationToken.None).ConfigureAwait(false);
            await ReplyTextAsync($"{b.Connection.Name} 已重置摇杆位置。").ConfigureAwait(false);
        }

        [Command("readMemory")]
        [Summary("从指定偏移读取内存并写入机器人目录。")]
        [RequireSudo]
        public async Task ReadAsync(uint offset, int length)
        {
            var b = Bot;
            var result = await b.Connection.ReadBytesAsync(offset, length, CancellationToken.None).ConfigureAwait(false);
            File.WriteAllBytes("dump.bin", result);
            await ReplyTextAsync("完成。").ConfigureAwait(false);
        }

        [Command("writeMemory")]
        [Summary("向指定偏移写入内存。")]
        [RequireSudo]
        public async Task WriteAsync(uint offset, string hex)
        {
            var b = Bot;
            var data = GetBytesFromHexString(hex.Replace(" ", ""));
            await b.Connection.WriteBytesAsync(data, offset, CancellationToken.None).ConfigureAwait(false);
            await ReplyTextAsync("完成。").ConfigureAwait(false);
        }

        [Command("readCommand")]
        [Summary("向系统模块写入指定命令并等待返回值")]
        [RequireSudo]
        public async Task ReadCommandAsync(int expectedReturnSize, [Remainder] string command)
        {
            var b = Bot;
            var data = System.Text.Encoding.UTF8.GetBytes(command + "\r\n");
            await ReplyTextAsync($"正在发送 `{command}` 并等待 {expectedReturnSize} 字节结果。").ConfigureAwait(false);
            var ret = await b.SwitchConnectedConnection.ReadRaw(data, expectedReturnSize, CancellationToken.None).ConfigureAwait(false);
            await ReplyTextAsync($"`{command}` 返回结果: {System.Text.Encoding.UTF8.GetString(ret)}").ConfigureAwait(false);
        }

        [Command("unfreezeAll")]
        [Summary("解冻所有内容")]
        [RequireSudo]
        public async Task UnfreezeAll()
        {
            var data = System.Text.Encoding.ASCII.GetBytes($"freezeClear\r\n");
            await Bot.SwitchConnectedConnection.SendRaw(data, CancellationToken.None).ConfigureAwait(false);
            await ReplyTextAsync("已解冻所有先前冻结的值").ConfigureAwait(false);
        }

        [Command("setFreezeDelay")]
        [Alias("setFreezeRate")]
        [Summary("配置冻结延迟（毫秒），范围3到10000")]
        [RequireSudo]
        public async Task SetFreezeDelay(int ms)
        {
            if (ms < 3 || ms > 10000)
            {
                await ReplyTextAsync($"错误！冻结速率必须在3到10000之间！").ConfigureAwait(false);
                return;
            }

            var data = System.Text.Encoding.ASCII.GetBytes($"configure freezeRate {ms}\r\n");
            await Bot.SwitchConnectedConnection.SendRaw(data, CancellationToken.None).ConfigureAwait(false);
            await ReplyTextAsync($"冻结速率设置为: {ms}").ConfigureAwait(false);
        }

        [Command("pauseFreeze")]
        [Alias("frzOff")]
        [Summary("暂停所有冻结值，直到调用取消暂停")]
        [RequireSudo]
        public async Task FreezePause()
        {
            await Bot.SwitchConnectedConnection.SetFreezePauseState(true, CancellationToken.None).ConfigureAwait(false);
            await ReplyTextAsync($"冻结已暂停。").ConfigureAwait(false);
        }

        [Command("pauseUnfreeze")]
        [Alias("frzOn")]
        [Summary("取消暂停所有冻结值")]
        [RequireSudo]
        public async Task FreezeUnpause()
        {
            await Bot.SwitchConnectedConnection.SetFreezePauseState(false, CancellationToken.None).ConfigureAwait(false);
            await ReplyTextAsync($"冻结已取消暂停。").ConfigureAwait(false);
        }

        private static byte[] GetBytesFromHexString(string seed)
        {
            return Enumerable.Range(0, seed.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(seed.Substring(x, 2), 16))
                .Reverse().ToArray();
        }
    }
}
