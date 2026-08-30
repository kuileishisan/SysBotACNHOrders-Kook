using Kook;
using Kook.WebSocket;
using NHSE.Core;
using SysBot.Base;
using System;
using System.Diagnostics;
using System.Linq;

namespace SysBot.ACNHOrders
{
    public class OrderRequest<T> : IACNHOrderNotifier<T> where T : Item, new()
    {
        public MultiItem ItemOrderData { get; }
        public ulong UserGuid { get; }
        public ulong OrderID { get; }
        public string VillagerName { get; }
        private SocketUser Trader { get; }
        private IMessageChannel CommandSentChannel { get; }
        public Action<CrossBot>? OnFinish { private get; set; }
        public T[] Order { get; }
        public VillagerRequest? VillagerOrder { get; }

        public OrderRequest(MultiItem data, T[] order, ulong user, ulong orderId, SocketUser trader, IMessageChannel commandSentChannel, VillagerRequest? vil)
        {
            ItemOrderData = data;
            UserGuid = user;
            OrderID = orderId;
            Trader = trader;
            CommandSentChannel = commandSentChannel;
            Order = order;
            VillagerName = trader.Username;
            VillagerOrder = vil;
        }

        public void OrderCancelled(CrossBot routine, string msg, bool faulted)
        {
            OnFinish?.Invoke(routine);
            Trader.SendTextAsync($"糟糕！您的订单发生了问题: {msg}");
            if (!faulted)
                CommandSentChannel.SendTextAsync($"{Trader.Mention()} - 您的订单已取消: {msg}");
        }

        public void OrderInitializing(CrossBot routine, string msg)
        {
            Trader.SendTextAsync($"您的订单即将开始，请**确保您的背包是__空的__**，然后去找奥维尔并停留在 Dodo 代码输入界面。我很快会发送 Dodo 代码给您。{msg}");
        }

        public void OrderReady(CrossBot routine, string msg, string dodo)
        {
            try
            {
                Trader.SendTextAsync($"我在等您 {Trader.Mention()}！{msg}。您的 Dodo 代码是 **{dodo}**");
            }
            catch (Exception e)
            {
                LogUtil.LogError("发送 dodo 代码失败: " + e.Message + "\n" + e.StackTrace, "Kook");
            }
        }

        public void OrderFinished(CrossBot routine, string msg)
        {
            OnFinish?.Invoke(routine);
            Trader.SendTextAsync($"您的订单已完成，感谢您的订购！{msg}");
        }

        public void SendNotification(CrossBot routine, string msg)
        {
            Trader.SendTextAsync(msg);
        }
    }
}
