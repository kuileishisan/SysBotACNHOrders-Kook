using System;
using System.Threading.Tasks;
using Kook.Commands;
using Kook.WebSocket;

namespace SysBot.ACNHOrders
{
    public sealed class RequireSudoAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var mgr = Globals.Bot.Config;
            if (mgr.CanUseSudo(context.User.Id) || context.User.Id == Globals.Self.Owner || mgr.IgnoreAllPermissions)
                return Task.FromResult(PreconditionResult.FromSuccess());

            if (context.User is not SocketGuildUser gUser)
                return Task.FromResult(PreconditionResult.FromError("您必须在服务器中才能运行此命令。"));

            if (mgr.CanUseSudo(gUser.Id))
                return Task.FromResult(PreconditionResult.FromSuccess());

            return Task.FromResult(PreconditionResult.FromError("您没有权限运行此命令。"));
        }
    }
}
