using Kook;
using Kook.WebSocket;

namespace SysBot.ACNHOrders
{
    public static class KookExtensions
    {
        public static string Mention(this IUser user) => $"(met){user.Id}(met)";
        public static string Mention(this SocketUser user) => $"(met){user.Id}(met)";
    }
}
