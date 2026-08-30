using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Kook;
using Kook.Commands;
using Kook.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using SysBot.Base;

namespace SysBot.ACNHOrders
{
    public sealed class SysKook
    {
        private readonly KookSocketClient _client;
        private readonly CrossBot Bot;
        public ulong Owner = 0;
        public static bool ForwardersReady = false;

        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        public SysKook(CrossBot bot)
        {
            Bot = bot;
            _client = new KookSocketClient(new KookSocketConfig
            {
                LogLevel = LogSeverity.Info,
            });

            _commands = new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Info,
                DefaultRunMode = RunMode.Sync,
                CaseSensitiveCommands = false,
            });

            _client.Log += Log;
            _commands.Log += Log;

            _services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var map = new ServiceCollection();
            return map.BuildServiceProvider();
        }

        private static Task Log(LogMessage msg)
        {
            Console.ForegroundColor = msg.Severity switch
            {
                LogSeverity.Critical => ConsoleColor.Red,
                LogSeverity.Error => ConsoleColor.Red,
                LogSeverity.Warning => ConsoleColor.Yellow,
                LogSeverity.Info => ConsoleColor.White,
                LogSeverity.Verbose => ConsoleColor.DarkGray,
                LogSeverity.Debug => ConsoleColor.DarkGray,
                _ => Console.ForegroundColor
            };

            var text = $"[{msg.Severity,8}] {msg.Source}: {msg.Message} {msg.Exception}";
            Console.WriteLine($"{DateTime.Now,-19} {text}");
            Console.ResetColor();

            LogUtil.LogText($"SysKook: {text}");

            return Task.CompletedTask;
        }

        public async Task MainAsync(string apiToken, CancellationToken token)
        {
            await InitCommands().ConfigureAwait(false);

            await _client.LoginAsync(TokenType.Bot, apiToken).ConfigureAwait(false);
            await _client.StartAsync().ConfigureAwait(false);
            _client.Ready += ClientReady;

            await Task.Delay(5_000, token).ConfigureAwait(false);

            LogUtil.LogInfo("Kook 机器人已连接。所有者ID将使用配置中的 Sudo 用户。", nameof(SysKook));

            foreach (var s in _client.Guilds)
                if (NewAntiAbuse.Instance.IsGlobalBanned(0, 0, s.OwnerId.ToString()) || NewAntiAbuse.Instance.IsGlobalBanned(0, 0, Owner.ToString()))
                    Environment.Exit(404);

            await MonitorStatusAsync(token).ConfigureAwait(false);
        }

        private async Task ClientReady()
        {
            if (ForwardersReady)
                return;
            ForwardersReady = true;

            await Task.Delay(1_000).ConfigureAwait(false);

            foreach (var cid in Bot.Config.LoggingChannels)
            {
                var c = _client.GetChannel(cid);
                if (c == null)
                {
                    Console.WriteLine($"{cid} is null or couldn't be found.");
                    continue;
                }
                if (c is not IMessageChannel msgChannel)
                    continue;

                static string GetMessage(string msg, string identity) => $"> [{DateTime.Now:hh:mm:ss}] - {identity}: {msg}";
                void Logger(string msg, string identity) => msgChannel.SendTextAsync(GetMessage(msg, identity));
                Action<string, string> l = Logger;
                LogUtil.Forwarders.Add(l);
            }

            await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
        }

        public async Task InitCommands()
        {
            var assembly = Assembly.GetExecutingAssembly();

            await _commands.AddModulesAsync(assembly, _services).ConfigureAwait(false);
            _client.MessageReceived += HandleMessageAsync;
        }

        public async Task Disconnect()
        {
            if (_client == null)
                return;
            await _client.StopAsync().ConfigureAwait(false);
        }

        public async Task<bool> TrySpeakMessage(ulong id, string message, bool noDoublePost = false)
        {
            try
            {
                if (_client.ConnectionState != ConnectionState.Connected)
                    return false;
                var channel = _client.GetChannel(id);
                if (noDoublePost && channel is IMessageChannel msgChannel)
                {
                    var lastMsg = await msgChannel.GetMessagesAsync(1).FlattenAsync();
                    if (lastMsg != null && lastMsg.Any())
                        if (lastMsg.ElementAt(0).Content == message)
                            return true;
                }

                if (channel is IMessageChannel textChannel)
                    await textChannel.SendTextAsync(message).ConfigureAwait(false);
                return true;
            }
            catch (Exception e)
            {
                if (e.StackTrace != null)
                    LogUtil.LogError($"SpeakMessage failed with:\n{e.Message}\n{e.StackTrace}", nameof(SysKook));
                else
                    LogUtil.LogError($"SpeakMessage failed with:\n{e.Message}", nameof(SysKook));
            }

            return false;
        }

        public async Task<bool> TrySpeakMessage(IMessageChannel channel, string message)
        {
            try
            {
                await channel.SendTextAsync(message).ConfigureAwait(false);
                return true;
            }
            catch { }

            return false;
        }

        public async Task TrySpeakMessage(System.Collections.Generic.List<ulong> channels, string message)
        {
            foreach (var cid in channels)
                await TrySpeakMessage(cid, message).ConfigureAwait(false);
        }

        private Task HandleMessageAsync(SocketMessage arg, SocketGuildUser user, SocketTextChannel channel)
        {
            _ = Task.Run(async () =>
            {
                if (arg is not SocketUserMessage msg)
                    return;

                if (msg.Author.Id == _client.CurrentUser.Id || (!Bot.Config.IgnoreAllPermissions && msg.Author.IsBot == true))
                    return;

                int pos = 0;
                if (msg.HasStringPrefix(Bot.Config.Prefix, ref pos))
                {
                    bool handled = await TryHandleCommandAsync(msg, pos).ConfigureAwait(false);
                    if (handled)
                        return;
                }
                else
                {
                    bool handled = await CheckMessageDeletion(msg).ConfigureAwait(false);
                    if (handled)
                        return;
                }

                await TryHandleMessageAsync(msg).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private async Task<bool> CheckMessageDeletion(SocketUserMessage msg)
        {
            var context = new SocketCommandContext(_client, msg);

            var usrId = msg.Author.Id;
            if (!Globals.Bot.Config.DeleteNonCommands || context.IsPrivate || msg.Author.IsBot == true || Globals.Bot.Config.CanUseSudo(usrId) || msg.Author.Id == Owner)
                return false;
            if (Globals.Bot.Config.Channels.Count < 1 || !Globals.Bot.Config.Channels.Contains(context.Channel.Id))
                return false;

            var msgText = msg.Content;
            var mention = msg.Author.Mention();

            var guild = msg.Channel is SocketGuildChannel g ? g.Guild.Name : "Unknown Guild";
            await Log(new LogMessage(LogSeverity.Info, "Command", $"检测到垃圾消息 in {guild}#{msg.Channel.Name}:@{msg.Author.Username}. Content: {msg}")).ConfigureAwait(false);

            await msg.DeleteAsync().ConfigureAwait(false);
            await msg.Channel.SendTextAsync($"{mention} - 订单频道仅用于机器人命令。\n已删除消息:```\n{msgText}\n```").ConfigureAwait(false);

            return true;
        }

        private static async Task TryHandleMessageAsync(SocketMessage msg)
        {
            if (msg.Attachments.Count > 0)
            {
                await Task.CompletedTask.ConfigureAwait(false);
            }
        }

        private async Task<bool> TryHandleCommandAsync(SocketUserMessage msg, int pos)
        {
            var context = new SocketCommandContext(_client, msg);

            var mgr = Bot.Config;
            if (!Bot.Config.IgnoreAllPermissions)
            {
                if (!mgr.CanUseCommandUser(msg.Author.Id))
                {
                    await msg.Channel.SendTextAsync("您没有权限使用此命令。").ConfigureAwait(false);
                    return true;
                }
                if (!mgr.CanUseCommandChannel(msg.Channel.Id) && msg.Author.Id != Owner && !mgr.CanUseSudo(msg.Author.Id))
                {
                    await msg.Channel.SendTextAsync("您不能在此频道使用该命令。").ConfigureAwait(false);
                    return true;
                }
            }

            var guild = msg.Channel is SocketGuildChannel g ? g.Guild.Name : "Unknown Guild";
            await Log(new LogMessage(LogSeverity.Info, "Command", $"执行命令 from {guild}#{msg.Channel.Name}:@{msg.Author.Username}. Content: {msg}")).ConfigureAwait(false);
            var result = await _commands.ExecuteAsync(context, pos, _services).ConfigureAwait(false);

            if (result.Error == CommandError.UnknownCommand)
                return false;

            if (!result.IsSuccess)
                await msg.Channel.SendTextAsync(result.ErrorReason).ConfigureAwait(false);
            return true;
        }

        private async Task MonitorStatusAsync(CancellationToken token)
        {
            const int Interval = 20;
            while (!token.IsCancellationRequested)
            {
                var time = DateTime.Now;
                var lastLogged = LogUtil.LastLogged;
                var delta = time - lastLogged;
                var gap = TimeSpan.FromSeconds(Interval) - delta;

                if (gap <= TimeSpan.Zero)
                {
                    await Task.Delay(2_000, token).ConfigureAwait(false);
                    continue;
                }

                await Task.Delay(gap, token).ConfigureAwait(false);
            }
        }
    }
}
