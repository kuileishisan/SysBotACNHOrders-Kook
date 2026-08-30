using Kook;
using Kook.Commands;
using Kook.WebSocket;
using NHSE.Core;
using NHSE.Villagers;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.ACNHOrders
{
    public class OrderModule : ModuleBase<SocketCommandContext>
    {
        public const string LastOrderDirectory = "UserOrder";
        public const string OrderMarker = "ORDER";
        public const string OrderCatMarker = "ORDERCAT";

        private static int MaxOrderCount => Globals.Bot.Config.OrderConfig.MaxQueueCount;
        private static Dictionary<ulong, DateTime> UserLastCommand = new();
        private static object commandSync = new();

        private const string OrderItemSummary =
            "请求机器人将物品订单添加到队列，使用用户提供的输入。" +
            "十六进制模式: 物品ID（十六进制）; 多个物品用空格分隔。" +
            "文本模式: 物品名称; 多个物品用逗号分隔。如需使用其他语言解析，请先输入语言代码和逗号，然后是物品。";

        [Command("order")]
        [Summary(OrderItemSummary)]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestOrderAsync([Summary(OrderItemSummary)][Remainder] string request)
        {
            var cfg = Globals.Bot.Config;
            VillagerRequest? vr = null;

            LogUtil.LogInfo($"收到订单 by {Context.User.Username} - {request}", nameof(OrderModule));

            var result = VillagerOrderParser.ExtractVillagerName(request, out var res, out var san);
            if (result == VillagerOrderParser.VillagerRequestResult.InvalidVillagerRequested)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - {res} 订单未被接受。");
                return;
            }

            if (result == VillagerOrderParser.VillagerRequestResult.Success)
            {
                if (!cfg.AllowVillagerInjection)
                {
                    await ReplyTextAsync($"{Context.User.Mention()} - 村民注入当前已禁用。");
                    return;
                }

                request = san;
                var replace = VillagerResources.GetVillager(res);
                vr = new VillagerRequest(Context.User.Username, replace, 0, GameInfo.Strings.GetVillager(res));
            }

            Item[]? items = null;

            var attachment = Context.Message.Attachments.FirstOrDefault();
            if (attachment != default)
            {
                var att = await NetUtil.DownloadNHIAsync(attachment).ConfigureAwait(false);
                if (!att.Success || !(att.Data is Item[] itemData))
                {
                    await ReplyTextAsync("未提供 NHI 附件！").ConfigureAwait(false);
                    return;
                }
                else
                {
                    items = itemData;

                    string path = Path.Combine(LastOrderDirectory, $"{Context.User.Id}");
                    var itemArray = new ItemArrayEditor<Item>(att.Data);
                    File.WriteAllBytes(path, itemArray.Write());
                }
            }

            if (items == null)
                items = string.IsNullOrWhiteSpace(request) ? new Item[1] { new Item(Item.NONE) } : ItemParser.GetItemsFromUserInput(request, cfg.DropConfig, ItemDestination.FieldItemDropped).ToArray();

            if (attachment == default)
            {
                string path = Path.Combine(LastOrderDirectory, $"{Context.User.Id}");
                File.WriteAllText(path, OrderMarker + request);
            }

            await AttemptToQueueRequest(items, Context.User, Context.Channel, vr).ConfigureAwait(false);
        }

        [Command("ordercat")]
        [Summary("订购由订单工具（如 ACNHMobileSpawner）创建的物品目录，不重复任何物品。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestCatalogueOrderAsync([Summary(OrderItemSummary)][Remainder] string request)
        {
            var cfg = Globals.Bot.Config;
            VillagerRequest? vr = null;

            LogUtil.LogInfo($"收到目录订单 by {Context.User.Username} - {request}", nameof(OrderModule));

            var result = VillagerOrderParser.ExtractVillagerName(request, out var res, out var san);
            if (result == VillagerOrderParser.VillagerRequestResult.InvalidVillagerRequested)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - {res} 订单未被接受。");
                return;
            }

            if (result == VillagerOrderParser.VillagerRequestResult.Success)
            {
                if (!cfg.AllowVillagerInjection)
                {
                    await ReplyTextAsync($"{Context.User.Mention()} - 村民注入当前已禁用。");
                    return;
                }

                request = san;
                var replace = VillagerResources.GetVillager(res);
                vr = new VillagerRequest(Context.User.Username, replace, 0, GameInfo.Strings.GetVillager(res));
            }

            var items = string.IsNullOrWhiteSpace(request) ? new Item[1] { new Item(Item.NONE) } : ItemParser.GetItemsFromUserInput(request, cfg.DropConfig, ItemDestination.FieldItemDropped);

            string path = Path.Combine(LastOrderDirectory, $"{Context.User.Id}");
            File.WriteAllText(path, OrderCatMarker + request);

            await AttemptToQueueRequest(items, Context.User, Context.Channel, vr, true).ConfigureAwait(false);
        }

        [Command("order")]
        [Summary("请求 NHI 格式的物品订单。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestNHIOrderAsync()
        {
            var attachment = Context.Message.Attachments.FirstOrDefault();
            if (attachment == default)
            {
                await ReplyTextAsync("未提供附件！").ConfigureAwait(false);
                return;
            }

            var att = await NetUtil.DownloadNHIAsync(attachment).ConfigureAwait(false);
            if (!att.Success || !(att.Data is Item[] items))
            {
                await ReplyTextAsync("未提供 NHI 附件！").ConfigureAwait(false);
                return;
            }

            string path = Path.Combine(LastOrderDirectory, $"{Context.User.Id}");
            var itemArray = new ItemArrayEditor<Item>(att.Data);
            File.WriteAllBytes(path, itemArray.Write());

            await AttemptToQueueRequest(items, Context.User, Context.Channel, null, true).ConfigureAwait(false);
        }


        [Command("lastorder")]
        [Alias("lo", "lasto", "lorder")]
        [Summary("向用户提供其上次订单数据。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestLastOrderAsync()
        {
            var cfg = Globals.Bot.Config;
            string path = Path.Combine(LastOrderDirectory, $"{Context.User.Id}");
            if (File.Exists(path))
            {
                string request = File.ReadAllText(path);

                if (request.StartsWith(OrderMarker))
                {
                    var stringWithoutMarker = request[OrderMarker.Length..];
                    var orderString = Globals.Bot.Config.Prefix + "order " + stringWithoutMarker;
                    await ReplyTextAsync($"{Context.User.Mention()}, 您上次的订单命令是:\n`{orderString}`").ConfigureAwait(false);
                    return;
                }
                else if (request.StartsWith(OrderCatMarker))
                {
                    var stringWithoutMarker = request[OrderCatMarker.Length..];
                    var orderString = Globals.Bot.Config.Prefix + "ordercat " + stringWithoutMarker;
                    await ReplyTextAsync($"{Context.User.Mention()}, 您上次的目录订单命令是:\n`{orderString}`").ConfigureAwait(false);
                    return;
                }
                else
                {
                    var bytes = File.ReadAllBytes(path);
                    var tempFileName = $"{Context.User.Id}.nhi";
                    var tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);
                    File.WriteAllBytes(tempFilePath, bytes);

                    await Context.Channel.SendFileAsync(tempFilePath, $"{Context.User.Mention()}, 这是您上次订购的 nhi 文件！").ConfigureAwait(false);

                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch (Exception e)
                    {
                        LogUtil.LogError($"删除临时 NHI 文件失败 {tempFilePath}: {e.Message}", nameof(OrderModule));
                    }
                }
            }
            else
            {
                await ReplyTextAsync($"(met){Context.User.Id}(met), 我们没有记录您的上次订单，请先下订单，然后可以使用此命令。").ConfigureAwait(false);
                return;
            }
        }

        [Command("checkitems")]
        [Alias("checkitem")]
        [Summary("检查物品ID，找出会导致订单无法进行的物品ID。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task CheckItemAsync([Summary(OrderItemSummary)][Remainder] string request)
        {
            var cfg = Globals.Bot.Config;
            var BadItemsList = "";
            var CheckItemN = "";
            Item[]? items = null;
            items = string.IsNullOrWhiteSpace(request) ? new Item[1] { new Item(Item.NONE) } : ItemParser.GetItemsFromUserInput(request, cfg.DropConfig, ItemDestination.FieldItemDropped).ToArray();
            {
                var Bitems = FileUtil.GetEmbeddedResource("SysBot.ACNHOrders.Resources", "InternalHexList.txt");
                string[] CheckItems = request.Split(' ');

                foreach (var CheckItem in CheckItems)
                    if (Bitems.Contains(CheckItem))
                    {
                        ushort itemID = ItemParser.GetID(CheckItem);
                        if (itemID != Item.NONE)
                        {
                            var name = GameInfo.Strings.GetItemName(itemID);
                            CheckItemN = name + ": " + CheckItem;
                        }
                        BadItemsList = BadItemsList + CheckItemN + "\n";
                    }

                if (BadItemsList == "")
                {
                    await ReplyTextAsync($"所有物品都可以安全订购。");
                }
                else
                {
                    await ReplyTextAsync($"以下物品不能安全订购:\n`{BadItemsList}`");
                }
            }
        }

        [Command("preset")]
        [Summary("请求机器人订购由机器人主机创建的预设。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RequestPresetOrderAsync([Remainder] string presetName)
        {
            var cfg = Globals.Bot.Config;
            VillagerRequest? vr = null;

            var result = VillagerOrderParser.ExtractVillagerName(presetName, out var res, out var san);
            if (result == VillagerOrderParser.VillagerRequestResult.InvalidVillagerRequested)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - {res} 订单未被接受。");
                return;
            }

            if (result == VillagerOrderParser.VillagerRequestResult.Success)
            {
                if (!cfg.AllowVillagerInjection)
                {
                    await ReplyTextAsync($"{Context.User.Mention()} - 村民注入当前已禁用。");
                    return;
                }

                presetName = san;
                var replace = VillagerResources.GetVillager(res);
                vr = new VillagerRequest(Context.User.Username, replace, 0, GameInfo.Strings.GetVillager(res));
            }

            presetName = presetName.Trim();
            var preset = PresetLoader.GetPreset(cfg.OrderConfig, presetName);
            if (preset == null)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - {presetName} 不是有效的预设。");
                return;
            }

            await AttemptToQueueRequest(preset, Context.User, Context.Channel, vr, true).ConfigureAwait(false);
        }

        [Command("ListPresets")]
        [Alias("LP")]
        [Summary("列出所有预设。")]
        public async Task RequestListPresetsAsync()
        {
            var bot = Globals.Bot;

            DirectoryInfo dir = new DirectoryInfo(bot.Config.OrderConfig.NHIPresetsDirectory);
            FileInfo[] files = dir.GetFiles("*.nhi");
            string listnhi = "";
            foreach (FileInfo file in files)
            {
                listnhi = listnhi + "\n " + Path.GetFileNameWithoutExtension(file.Name);
            }
            await ReplyTextAsync($"**可用预设如下:** {listnhi}。").ConfigureAwait(false);
        }

        [Command("uploadpreset")]
        [Alias("UpPre", "UP")]
        [Summary("上传文件以添加到预设文件夹。")]
        [RequireSudo]
        public async Task RequestUploadPresetAsync()
        {
            var cfg = Globals.Bot.Config;
            var attachments = Context.Message.Attachments;

            string file = attachments.ElementAt(0).Filename;
            string url = attachments.ElementAt(0).Url;

            var file1 = cfg.OrderConfig.NHIPresetsDirectory + "/" + file;
            await NetUtil.DownloadFileAsync(url, file1).ConfigureAwait(false);

            await ReplyTextAsync("收到附件！\n\n" + "以下文件已添加到预设文件夹: " + file);
        }

        [Command("queue")]
        [Alias("qs", "qp", "position")]
        [Summary("查看您在队列中的位置。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task ViewQueuePositionAsync()
        {
            var cooldown = Globals.Bot.Config.OrderConfig.PositionCommandCooldown;
            if (!CanCommand(Context.User.Id, cooldown, true))
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 此命令有 {cooldown} 秒冷却时间。请负责任地使用此机器人。").ConfigureAwait(false);
                return;
            }

            var position = QueueExtensions.GetPosition(Context.User.Id, out _);
            if (position < 0)
            {
                await ReplyTextAsync("抱歉，您不在队列中，或者您的订单正在进行中。").ConfigureAwait(false);
                return;
            }

            var message = $"{Context.User.Mention()} - 您在订单队列中。位置: {position}。";
            if (position > 1)
                message += $" 您的预计等待时间是 {QueueExtensions.GetETA(position)}。";
            else
                message += " 您的订单将在当前订单完成后开始！";

            await ReplyTextAsync(message).ConfigureAwait(false);
            await Context.Message.DeleteAsync().ConfigureAwait(false);
        }

        [Command("remove")]
        [Alias("qc", "delete", "removeMe", "cancel")]
        [Summary("将自己从队列中移除。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task RemoveFromQueueAsync()
        {
            QueueExtensions.GetPosition(Context.User.Id, out var order);
            if (order == null)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 抱歉，您不在队列中，或者您的订单正在进行中。").ConfigureAwait(false);
                return;
            }

            Globals.Hub.Orders.RemoveByUserId(Context.User.Id);
            await ReplyTextAsync($"{Context.User.Mention()} - 您的订单已移除。您可以随时重新加入队列。").ConfigureAwait(false);
        }

        [Command("removeUser")]
        [Alias("rmu", "removeOther", "rmo")]
        [Summary("将某人从队列中移除。")]
        [RequireSudo]
        public async Task RemoveOtherFromQueueAsync(string identity)
        {
            if (ulong.TryParse(identity, out var res))
            {
                QueueExtensions.GetPosition(res, out var order);
                if (order == null)
                {
                    await ReplyTextAsync($"{identity} 不是队列中的有效 ulong。").ConfigureAwait(false);
                    return;
                }

                Globals.Hub.Orders.RemoveByUserId(res);
                await ReplyTextAsync($"{identity} ({order.VillagerName}) 已从队列中移除。").ConfigureAwait(false);
            }
            else
                await ReplyTextAsync($"{identity} 不是有效的 u64。").ConfigureAwait(false);
        }

        [Command("removeAlt")]
        [Alias("removeLog", "rmAlt")]
        [Summary("从本地用户到村民反滥用数据库中移除身份（名称-id）")]
        [RequireSudo]
        public async Task RemoveAltAsync([Remainder] string identity)
        {
            if (NewAntiAbuse.Instance.Remove(identity))
                await ReplyTextAsync($"{identity} 已从数据库中移除。").ConfigureAwait(false);
            else
                await ReplyTextAsync($"{identity} 不是有效身份。").ConfigureAwait(false);
        }

        [Command("removeAltLegacy")]
        [Alias("removeLogLegacy", "rmAltLegacy")]
        [Summary("（使用旧数据库）从本地用户到村民反滥用数据库中移除身份（名称-id）")]
        [RequireSudo]
        public async Task RemoveLegacyAltAsync([Remainder] string identity)
        {
            if (LegacyAntiAbuse.CurrentInstance.Remove(identity))
                await ReplyTextAsync($"{identity} 已从数据库中移除。").ConfigureAwait(false);
            else
                await ReplyTextAsync($"{identity} 不是有效身份。").ConfigureAwait(false);
        }

        [Command("visitorList")]
        [Alias("visitors")]
        [Summary("打印岛上的访客列表（仅 dodo 恢复模式）。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task ShowVisitorList()
        {
            if (!Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode && Globals.Self.Owner != Context.User.Id)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 仅在 dodo 恢复模式下可以查看访客。请尊重其他订购者的隐私。");
                return;
            }

            await ReplyTextAsync(Globals.Bot.VisitorList.VisitorFormattedString);
        }

        [Command("checkState")]
        [Alias("checkDirtyState")]
        [Summary("打印机器人是否会为下一个订单重启游戏。")]
        [RequireSudo]
        public async Task ShowDirtyStateAsync()
        {
            if (Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await ReplyTextAsync("dodo 恢复模式下没有订单状态。");
                return;
            }

            await ReplyTextAsync($"状态: {(Globals.Bot.GameIsDirty ? "不良" : "良好")}").ConfigureAwait(false);
        }

        [Command("queueList")]
        [Alias("ql")]
        [Summary("通过私信向用户发送当前队列中的名称列表。")]
        [RequireSudo]
        public async Task ShowQueueListAsync()
        {
            if (Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                await ReplyTextAsync("dodo 恢复模式下没有队列。").ConfigureAwait(false);
                return;
            }

            try
            {
                await Context.User.SendTextAsync($"以下用户在 {Globals.Bot.TownName} 的队列中: \r\n{QueueExtensions.GetQueueString()}").ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await ReplyTextAsync($"{e.Message}: 您的私信是否开启？").ConfigureAwait(false);
            }
        }

        [Command("gameTime")]
        [Alias("gt")]
        [Summary("打印上次检查的（当前）游戏内时间。")]
        [RequireQueueRole(nameof(Globals.Bot.Config.RoleUseBot))]
        public async Task GetGameTime()
        {
            var bot = Globals.Bot;
            var cooldown = bot.Config.OrderConfig.PositionCommandCooldown;
            if (!CanCommand(Context.User.Id, cooldown, true))
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 此命令有 {cooldown} 秒冷却时间。请负责任地使用此机器人。").ConfigureAwait(false);
                return;
            }

            if (Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode)
            {
                var nooksMessage = (bot.LastTimeState.Hour >= 22 || bot.LastTimeState.Hour < 8) ? "Nook's Cranny 已关闭" : "Nook's Cranny 应该正在营业。";
                await ReplyTextAsync($"当前游戏内时间是: {bot.LastTimeState} \r\n{nooksMessage}").ConfigureAwait(false);
                return;
            }

            await ReplyTextAsync($"上次订单开始于: {bot.LastTimeState}").ConfigureAwait(false);
            return;
        }

        private async Task AttemptToQueueRequest(IReadOnlyCollection<Item> items, SocketUser orderer, IMessageChannel msgChannel, VillagerRequest? vr, bool catalogue = false)
        {
            if (!Globals.Bot.Config.AllowKnownAbusers && LegacyAntiAbuse.CurrentInstance.IsGlobalBanned(orderer.Id))
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 您没有权限使用此机器人。");
                return;
            }

            if (Globals.Bot.Config.DodoModeConfig.LimitedDodoRestoreOnlyMode || Globals.Bot.Config.SkipConsoleBotCreation)
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 当前不接受订单。");
                return;
            }

            if (GlobalBan.IsBanned(orderer.Id.ToString()))
            {
                await ReplyTextAsync($"{Context.User.Mention()} - 您因滥用被封禁。订单未被接受。");
                return;
            }

            var currentOrderCount = Globals.Hub.Orders.Count;
            if (currentOrderCount >= MaxOrderCount)
            {
                var requestLimit = $"队列已满，当前有 {currentOrderCount} 名玩家在队列中。请稍后再试。";
                await ReplyTextAsync(requestLimit).ConfigureAwait(false);
                return;
            }

            if (!InternalItemTool.CurrentInstance.IsSaneAfterCorrection(items, Globals.Bot.Config.DropConfig))
            {
                var unsafeItems = InternalItemTool.CurrentInstance.GetUnsafeItemNames(items);
                var unsafeList = string.Join(", ", unsafeItems);
                await ReplyTextAsync($"{Context.User.Mention()} - 您试图订购会损坏存档的物品。订单未被接受。\r\n以下物品不安全: {unsafeList}");
                return;
            }

            if (items.Count > MultiItem.MaxOrder)
            {
                var clamped = $"用户每条命令最多 {MultiItem.MaxOrder} 个物品，您请求了 {items.Count} 个。超出限制的物品已被移除。";
                await ReplyTextAsync(clamped).ConfigureAwait(false);
                items = items.Take(40).ToArray();
            }

            var multiOrder = new MultiItem(items.ToArray(), catalogue, true, true);
            var requestInfo = new OrderRequest<Item>(multiOrder, multiOrder.ItemArray.Items.ToArray(), orderer.Id, QueueExtensions.GetNextID(), orderer, msgChannel, vr);
            await Context.AddToQueueAsync(requestInfo, orderer.Username, orderer);
        }

        public static bool CanCommand(ulong id, int secondsCooldown, bool addIfNotAdded)
        {
            if (secondsCooldown < 0)
                return true;
            lock (commandSync)
            {
                if (UserLastCommand.ContainsKey(id))
                {
                    bool inCooldownPeriod = Math.Abs((DateTime.Now - UserLastCommand[id]).TotalSeconds) < secondsCooldown;
                    if (addIfNotAdded && !inCooldownPeriod)
                    {
                        UserLastCommand.Remove(id);
                        UserLastCommand.Add(id, DateTime.Now);
                    }
                    return !inCooldownPeriod;
                }
                else if (addIfNotAdded)
                {
                    UserLastCommand.Add(id, DateTime.Now);
                }
                return true;
            }
        }
    }

    public static class VillagerOrderParser
    {
        public enum VillagerRequestResult
        {
            NoVillagerRequested,
            InvalidVillagerRequested,
            Success
        }

        public static VillagerRequestResult ExtractVillagerName(string order, out string result, out string sanitizedOrder, string villagerFormat = "Villager:")
        {
            result = string.Empty;
            sanitizedOrder = string.Empty;
            var index = order.IndexOf(villagerFormat, StringComparison.InvariantCultureIgnoreCase);
            if (index < 0)
                return VillagerRequestResult.NoVillagerRequested;

            var internalName = order.Substring(index + villagerFormat.Length);
            var nameSearched = internalName;
            internalName = internalName.Trim();

            if (!VillagerResources.IsVillagerDataKnown(internalName))
                internalName = GameInfo.Strings.VillagerMap.FirstOrDefault(z => string.Equals(z.Value, internalName, StringComparison.InvariantCultureIgnoreCase)).Key;

            if (IsUnadoptable(nameSearched) || IsUnadoptable(internalName))
            {
                result = $"{nameSearched} 不可领养。此村民无需订单设置。";
                return VillagerRequestResult.InvalidVillagerRequested;
            }

            if (internalName == default)
            {
                result = $"{nameSearched} 不是有效的村民内部名称。";
                return VillagerRequestResult.InvalidVillagerRequested;
            }

            sanitizedOrder = order.Substring(0, index);
            sanitizedOrder = sanitizedOrder.Trim().TrimEnd(',');
            result = internalName;
            return VillagerRequestResult.Success;
        }

        private static readonly List<string> UnadoptableVillagers = new()
        {
            "cbr18", "der10", "elp11", "gor11", "rbt20", "shp14",
            "alp", "alw", "bev", "bey", "boa", "boc", "bpt", "chm", "chy",
            "cml", "cmlb", "dga", "dgb", "doc", "dod", "fox", "fsl", "grf",
            "gsta", "gstb", "gul", "hgc", "hgh", "hgs", "kpg", "kpm", "kpp",
            "kps", "lom", "man", "mka", "mnc", "mnk", "mob", "mol", "otg",
            "otgb", "ott", "owl", "ows", "pck", "pge", "pgeb", "pkn", "plk",
            "plm", "plo", "poo", "poob", "pyn", "rcm", "rco", "rct", "rei",
            "seo", "skk", "slo", "spn", "sza", "szo", "tap", "tkka", "tkkb",
            "ttla", "ttlb", "tuk", "upa", "wrl", "xct", "brd20", "der12",
        };

        public static bool IsUnadoptable(string? internalName) => UnadoptableVillagers.Contains(internalName == null ? string.Empty : internalName.Trim().ToLower());
    }
}
