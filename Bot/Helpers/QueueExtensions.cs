using Kook;
using Kook.Commands;
using Kook.WebSocket;
using Kook.Net;
using NHSE.Core;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Concurrent;

namespace SysBot.ACNHOrders
{
    public static class QueueExtensions
    {
        const int ArriveTime = 90;
        const int SetupTime = 95;

        public static async Task AddToQueueAsync(this SocketCommandContext Context, OrderRequest<Item> itemReq, string player, SocketUser trader)
        {
            var test = await trader.SendTextAsync("我已将您添加到队列！订单准备就绪时我会在这里消息通知您").ConfigureAwait(false);

            var result = AttemptAddToQueue(itemReq, trader.Mention(), trader.Username, out var msg);

            await Context.Channel.SendTextAsync(msg).ConfigureAwait(false);
            await trader.SendTextAsync(msg).ConfigureAwait(false);

            if (result)
            {
                if (!Context.IsPrivate)
                    await Context.Message.DeleteAsync().ConfigureAwait(false);
            }
            else
            {
                try
                {
                    var actualMsg = await test.GetOrDownloadAsync().ConfigureAwait(false);
                    await actualMsg.DeleteAsync().ConfigureAwait(false);
                }
                catch { }
            }
        }

        public static bool AddToQueueSync(IACNHOrderNotifier<Item> itemReq, string playerMention, string playerNameId, out string msg)
        {
            var result = AttemptAddToQueue(itemReq, playerMention, playerNameId, out var msge);
            msg = msge;
            return result;
        }

        private static bool AttemptAddToQueue(IACNHOrderNotifier<Item> itemReq, string traderMention, string traderDispName, out string msg)
        {
            var orders = Globals.Hub.Orders;

            var existingOrder = orders.GetByUserId(itemReq.UserGuid);
            if (existingOrder != null)
            {
                msg = $"{traderMention} - 抱歉，您已经在队列中了。";
                return false;
            }

            if (Globals.Bot.CurrentUserName == traderDispName)
            {
                msg = $"{traderMention} - 无法将您的订单加入队列，因为它是当前正在处理的订单。如果您已经完成，请等待几秒钟让队列清除。";
                return false;
            }

            var position = orders.Count + 1;
            var idToken = Globals.Bot.Config.OrderConfig.ShowIDs ? $" (ID {itemReq.OrderID})" : string.Empty;
            msg = $"{traderMention} - 已将您添加到订单队列{idToken}。您的位置是: **{position}**";

            if (position > 1)
                msg += $". 您的预计等待时间是 {GetETA(position)}";
            else
                msg += ". 您的订单将在当前订单完成后开始！";

            if (itemReq.VillagerOrder != null)
                msg += $". {GameInfo.Strings.GetVillager(itemReq.VillagerOrder.GameName)} 将在岛上等您。请确保在订单时间内可以接走它们。";

            Globals.Hub.Orders.Enqueue(itemReq);

            return true;
        }

        public static int GetPosition(ulong id, out OrderRequest<Item>? order)
        {
            var orders = Globals.Hub.Orders;
            var position = orders.GetPosition(id);

            if (position > 0)
            {
                var found = orders.GetByUserId(id);
                if (found is OrderRequest<Item> oreq)
                {
                    order = oreq;
                    return position;
                }
            }

            order = null;
            return -1;
        }

        public static string GetETA(int pos)
        {
            int minSeconds = ArriveTime + SetupTime + Globals.Bot.Config.OrderConfig.UserTimeAllowed + Globals.Bot.Config.OrderConfig.WaitForArriverTime;
            int addSeconds = ArriveTime + Globals.Bot.Config.OrderConfig.UserTimeAllowed + Globals.Bot.Config.OrderConfig.WaitForArriverTime;
            var timeSpan = TimeSpan.FromSeconds(minSeconds + (addSeconds * (pos - 1)));
            if (timeSpan.Hours > 0)
                return string.Format("{0:D2}小时:{1:D2}分:{2:D2}秒", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
            else
                return string.Format("{0:D2}分:{1:D2}秒", timeSpan.Minutes, timeSpan.Seconds);
        }

        private static ulong ID = 0;
        private static object IDAccessor = new();
        public static ulong GetNextID()
        {
            lock (IDAccessor)
            {
                return ID++;
            }
        }

        public static void ClearQueue<T>(this ConcurrentQueue<T> queue)
        {
            T item;
#pragma warning disable CS8600
            while (queue.TryDequeue(out item)) { }
#pragma warning restore CS8600
        }

        public static string GetQueueString()
        {
            var orders = Globals.Hub.Orders;
            var orderArray = orders.ToArray();
            string orderString = string.Empty;
            foreach (var ord in orderArray)
                orderString += $"{ord.VillagerName} \r\n";

            return orderString;
        }
    }
}
