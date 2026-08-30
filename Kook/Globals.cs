using System;
using System.Linq;
using System.Threading.Tasks;
using Kook.Commands;
using Kook.WebSocket;

namespace SysBot.ACNHOrders
{
    public static class Globals
    {
        public static SysKook Self { get; set; } = default!;
        public static CrossBot Bot { get; set; } = default!;
        public static QueueHub Hub { get; set; } = default!;
    }

    public sealed class RequireQueueRoleAttribute : PreconditionAttribute
    {
        private readonly string _name;

        public RequireQueueRoleAttribute(string name) => _name = name;

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var mgr = Globals.Bot.Config;
            if (mgr.CanUseSudo(context.User.Id) || Globals.Self.Owner == context.User.Id || mgr.IgnoreAllPermissions)
                return Task.FromResult(PreconditionResult.FromSuccess());

            if (context.User is not SocketGuildUser gUser)
                return Task.FromResult(PreconditionResult.FromError("您必须在服务器中才能运行此命令。"));

            if (!mgr.AcceptingCommands)
                return Task.FromResult(PreconditionResult.FromError("抱歉，我当前不接受命令！"));

            bool hasRole = mgr.GetHasRole(_name, gUser.Roles.Select(z => z.Name));
            if (!hasRole)
                return Task.FromResult(PreconditionResult.FromError("您没有运行此命令所需的角色。"));

            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
