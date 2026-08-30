using System;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using SysBot.ACNHOrders.Twitch;
using SysBot.ACNHOrders.Signalr;

namespace SysBot.ACNHOrders
{
    public static class BotRunner
    {
        public static async Task RunFrom(CrossBotConfig config, CancellationToken cancel, TwitchConfig? tConfig = null)
        {
            LogUtil.Forwarders.Add(Logger);
            static void Logger(string msg, string identity) => Console.WriteLine(GetMessage(msg, identity));
            static string GetMessage(string msg, string identity) => $"> [{DateTime.Now:hh:mm:ss}] - {identity}: {msg}";

            var bot = new CrossBot(config);

            var sys = new SysKook(bot);

            Globals.Self = sys;
            Globals.Bot = bot;
            Globals.Hub = QueueHub.CurrentInstance;
            GlobalBan.UpdateConfiguration(config);

            bot.Log("正在启动 Kook。");
#pragma warning disable 4014
            Task.Run(() => sys.MainAsync(config.Token, cancel), cancel);
#pragma warning restore 4014


            if (tConfig != null && !string.IsNullOrWhiteSpace(tConfig.Token))
            {
                bot.Log("正在启动 Twitch。");
                var _ = new TwitchCrossBot(tConfig, bot);
            }

            if (!string.IsNullOrWhiteSpace(config.SignalrConfig.URIEndpoint))
            {
                bot.Log("正在启动 Web。");
                var _ = new SignalrCrossBot(config.SignalrConfig, bot);
            }

            if (config.SkipConsoleBotCreation)
            {
                await Task.Delay(-1, cancel).ConfigureAwait(false);
                return;
            }

            while (!cancel.IsCancellationRequested)
            {
                bot.Log("正在启动机器人循环。");

                var task = bot.RunAsync(cancel);
                await task.ConfigureAwait(false);

                bool attemptReconnect = false;

                if (task.IsFaulted)
                {
                    if (task.Exception == null)
                    {
                        bot.Log("机器人因未知错误终止。");
                    }
                    else
                    {
                        bot.Log("机器人因错误终止:");
                        foreach (var ex in task.Exception.InnerExceptions)
                        {
                            bot.Log(ex.Message);
                            var st = ex.StackTrace;
                            if (st != null)
                                bot.Log(st);
                        }
                    }
                    attemptReconnect = false;
                }
                else
                {
                    bot.Log("机器人已终止。");
                    attemptReconnect = true;
                    bot.Log("请稍候... 10秒后尝试重新连接。");
                }

                if (attemptReconnect)
                {
                    await Task.Delay(10_000, cancel).ConfigureAwait(false);
                    bot.Log("机器人正在尝试重启...");
                    bot = new CrossBot(config);
                    Globals.Bot = bot;

                    await sys.Disconnect();
                    sys = new SysKook(bot);
                    Globals.Self = sys;
                    bot.Log("正在重启 Kook。");
#pragma warning disable 4014
                    Task.Run(() => sys.MainAsync(config.Token, cancel), cancel);
#pragma warning restore 4014
                }
                else
                    break;
            }
        }
    }
}
