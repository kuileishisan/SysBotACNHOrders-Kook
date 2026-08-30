using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ACNHMobileSpawner;
using Kook.Commands;
using SysBot.Base;

namespace SysBot.ACNHOrders
{
    public class ControlModule : ModuleBase<SocketCommandContext>
    {
        [Command("detach")]
        [Summary("分离虚拟控制器，以便操作员可以临时使用自己的手柄。")]
        [RequireSudo]
        public async Task DetachAsync()
        {
            await ReplyTextAsync("控制器分离请求将很快执行。").ConfigureAwait(false);
            var bot = Globals.Bot;
            await bot.Connection.SendAsync(SwitchCommand.DetachController(), CancellationToken.None).ConfigureAwait(false);
        }

        [Command("toggleRequests")]
        [Summary("切换是否接受丢弃请求。")]
        [RequireSudo]
        public async Task ToggleRequestsAsync()
        {
            bool value = (Globals.Bot.Config.AcceptingCommands ^= true);
            await ReplyTextAsync($"接受丢弃请求: {value}。").ConfigureAwait(false);
        }

        [Command("toggleMashB")]
        [Summary("切换机器人是否应连按B键以确保所有对话都被处理。仅在 dodo 恢复模式下有效。")]
        [RequireSudo]
        public async Task ToggleMashB()
        {
            Globals.Bot.Config.DodoModeConfig.MashB = !Globals.Bot.Config.DodoModeConfig.MashB;
            await ReplyTextAsync($"连按B设置为: {Globals.Bot.Config.DodoModeConfig.MashB}。").ConfigureAwait(false);
        }

        [Command("toggleRefresh")]
        [Summary("切换机器人是否应刷新地图。仅在 dodo 恢复模式下有效。")]
        public async Task ToggleRefresh()
        {
            Globals.Bot.Config.DodoModeConfig.RefreshMap = !Globals.Bot.Config.DodoModeConfig.RefreshMap;
            await ReplyTextAsync($"刷新地图设置为: {Globals.Bot.Config.DodoModeConfig.RefreshMap}。").ConfigureAwait(false);
        }

        [Command("newDodo")]
        [Alias("restartGame", "restart")]
        [Summary("让机器人重启游戏并获取新的 dodo 代码。仅在 dodo 恢复模式下有效。")]
        [RequireSudo]
        public async Task FetchNewDodo()
        {
            Globals.Bot.RestoreRestartRequested = true;
            await ReplyTextAsync($"正在发送获取新 dodo 代码的请求。").ConfigureAwait(false);
        }

        [Command("timer")]
        [Alias("timedDodo", "delayDodo")]
        [Summary("让机器人在延迟后重启游戏并获取新的 dodo 代码。仅在 dodo 恢复模式下有效。")]
        [RequireSudo]
        public async Task DelayFetchNewDodo(int timeDelayMinutes)
        {
            _ = Task.Run(async () =>
              {
                  await Task.Delay(timeDelayMinutes * 60_000, CancellationToken.None).ConfigureAwait(false);
                  Globals.Bot.RestoreRestartRequested = true;
                  await ReplyTextAsync($"即将获取新的 dodo 代码。").ConfigureAwait(false);
              }, CancellationToken.None).ConfigureAwait(false);
            await ReplyTextAsync($"将在 {timeDelayMinutes} 分钟后发送获取新 dodo 代码的请求。").ConfigureAwait(false);
        }

        [Command("speak")]
        [Alias("talk", "say")]
        [Summary("让机器人在有人在岛上时说话。")]
        [RequireSudo]
        public async Task SpeakAsync([Remainder] string request)
        {
            var saneString = request.Length > (int)OffsetHelper.ChatBufferSize ? request.Substring(0, (int)OffsetHelper.ChatBufferSize) : request;
            Globals.Bot.Speaks.Enqueue(new SpeakRequest(Context.User.Username, saneString));
            await ReplyTextAsync($"我很快会说 `{saneString}`。").ConfigureAwait(false);
        }

        [Command("setScreenOn")]
        [Alias("screenOn", "scrOn")]
        [Summary("打开屏幕")]
        [RequireSudo]
        public async Task SetScreenOnAsync()
        {
            await SetScreen(true).ConfigureAwait(false);
        }

        [Command("setScreenOff")]
        [Alias("screenOff", "scrOff")]
        [Summary("关闭屏幕")]
        [RequireSudo]
        public async Task SetScreenOffAsync()
        {
            await SetScreen(false).ConfigureAwait(false);
        }

        [Command("charge")]
        [Alias("getCharge", "chg")]
        [Summary("打印主机当前电池百分比")]
        [RequireSudo]
        public async Task GetChargeAsync()
        {
            await ReplyTextAsync($"上次记录的电量: {Globals.Bot.ChargePercent}%");
        }

        [Command("kill")]
        [Alias("sudoku", "exit")]
        [Summary("关闭机器人")]
        [RequireSudo]
        public async Task KillBotAsync()
        {
            await ReplyTextAsync($"再见 {Context.User.Mention()}，请记住我。").ConfigureAwait(false);
            Environment.Exit(0);
        }

        [Command("ping")]
        [Summary("如果存活则回复 pong")]
        public async Task PingAsync()
        {
            await ReplyTextAsync($"你好 {Context.User.Mention()}，Pong！").ConfigureAwait(false);
        }

        [Command("makebat")]
        [Alias("makebatch")]
        [RequireSudo]
        public async Task MakeBatAsync()
        {
            var botloc = AppDomain.CurrentDomain.BaseDirectory;
            var botapp = AppDomain.CurrentDomain.FriendlyName;
            var botname = "";
            if (Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                botname = "宝藏岛";
            }
            else
            {
                botname = "订单机器人";
            }
            var batinfo = $"TITLE {botname}\n@echo off\n:Start\ncd {botloc}\n{botapp}\n:: 等待20秒后重启。\nTIMEOUT / T 20\nGOTO: Start";
            using (StreamWriter writer = new StreamWriter("Restart.bat"))
            {
                writer.WriteLine($"{batinfo}");
            }
            await Context.Message.DeleteAsync().ConfigureAwait(false);
            await ReplyTextAsync("我已创建 `Restart.bat`。请在机器人文件夹中查看该文件。").ConfigureAwait(false);
        }

        private async Task SetScreen(bool on)
        {
            var bot = Globals.Bot;

            await bot.SetScreenCheck(on, CancellationToken.None, true).ConfigureAwait(false);
            await ReplyTextAsync("屏幕状态设置为: " + (on ? "开" : "关")).ConfigureAwait(false);
        }
    }
}
