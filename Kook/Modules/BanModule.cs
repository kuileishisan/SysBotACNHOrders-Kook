using Kook.Commands;
using System;
using System.Threading.Tasks;

namespace SysBot.ACNHOrders
{
    public class BanModule : ModuleBase<SocketCommandContext>
    {
        [Command("unBan")]
        [Summary("通过用户数字ID解除滥用封禁。")]
        [RequireSudo]
        public async Task UnBanAsync(string id)
        {
            if (GlobalBan.IsBanned(id))
            {
                GlobalBan.UnBan(id);
                await ReplyTextAsync($"{id} 已解除滥用封禁。").ConfigureAwait(false);
            }
            else
            {
                await ReplyTextAsync($"{id} 未在封禁列表中找到。").ConfigureAwait(false);
            }
        }

        [Command("ban")]
        [Summary("通过用户数字ID进行滥用封禁。")]
        [RequireSudo]
        public async Task BanAsync(string id)
        {
            if (GlobalBan.IsBanned(id))
            {
                await ReplyTextAsync($"{id} 已被滥用封禁").ConfigureAwait(false);
            }
            else
            {
                GlobalBan.Ban(id);
                await ReplyTextAsync($"{id} 已被滥用封禁。").ConfigureAwait(false);
            }
        }

        [Command("checkBan")]
        [Summary("通过用户数字ID检查封禁状态。")]
        [RequireSudo]
        public async Task CheckBanAsync(string id) => await ReplyTextAsync(GlobalBan.IsBanned(id) ? $"{id} 已被滥用封禁" : $"{id} 未被滥用封禁").ConfigureAwait(false);

        [Command("restrict")]
        [Summary("通过用户数字账户ID临时限制用户。")]
        [RequireSudo]
        public async Task RestrictAsync(ulong id)
        {
            if (GlobalBan.IsTempRestricted(id))
            {
                await ReplyTextAsync($"{id} 已被临时限制").ConfigureAwait(false);
            }
            else
            {
                GlobalBan.TempRestrict(id);
                await ReplyTextAsync($"{id} 已被临时限制。").ConfigureAwait(false);
            }
        }

        [Command("unRestrict")]
        [Summary("通过用户数字账户ID解除临时限制。")]
        [RequireSudo]
        public async Task UnRestrictAsync(ulong id)
        {
            if (GlobalBan.IsTempRestricted(id))
            {
                GlobalBan.RemoveTempRestrict(id);
                await ReplyTextAsync($"{id} 已解除临时限制。").ConfigureAwait(false);
            }
            else
            {
                await ReplyTextAsync($"{id} 未被临时限制").ConfigureAwait(false);
            }
        }

        [Command("checkRestrict")]
        [Summary("通过用户数字账户ID检查临时限制状态。")]
        [RequireSudo]
        public async Task CheckRestrictAsync(ulong id) => await ReplyTextAsync(GlobalBan.IsTempRestricted(id) ? $"{id} 已被临时限制" : $"{id} 未被临时限制").ConfigureAwait(false);
    }
}
