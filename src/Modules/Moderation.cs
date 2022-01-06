using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NightFyreBOT.modules
{
    public class Moderation : ModuleBase<SocketCommandContext>
    {
        /// <summary>
        /// Deletes a specified amount of messages
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        [Command("purge")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireOwner]
        public async Task Purge(int amount)
        {
            var messages = await Context.Channel.GetMessagesAsync(amount + 1).FlattenAsync();
            await (Context.Channel as SocketTextChannel).DeleteMessagesAsync(messages);
            var message = await Context.Channel.SendMessageAsync($"{messages.Count()} messages deleted succesfully");
            await Task.Delay(2500);
            await message.DeleteAsync();
        }

        /// <summary>
        /// Sends a message to the specified channel
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <returns>nf.sendc channel message</returns>
        [Command("sendc")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task SendMessageC(IMessageChannel channel, [Remainder] string message)
        {
            //await Context.Message.DeleteAsync();        //Delete the message that summoned the bot
            await channel.SendMessageAsync(message);    //Send message to specified channel

            //Making an embed
            var builder = new EmbedBuilder()
                .WithColor(new Color(169, 0, 169))
                .AddField($"{Context.User}", $"Message sent to **{channel}**", true);
            var embed = builder.Build();
            await Context.Channel.SendMessageAsync(null, false, embed);
        }

        /// <summary>
        /// Sends a message to specified user
        /// </summary>
        /// <param name="user"></param>
        /// <param name="message"></param>
        /// <returns>nf.sendu user message</returns>
        [Command("sendu")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireOwner]
        public async Task SendMessageU(SocketGuildUser user, [Remainder] string message)
        {
            //await Context.Message.DeleteAsync();                                    //Delete message that summoned the bot
            await user.SendMessageAsync(message);                                   //Send message to specified user

            //Now we will make a very small embed
            var builder = new EmbedBuilder()
                .WithColor(new Color(169, 0, 169))
                .AddField($"{Context.User}", $"Message Sent to {user}", true);
            var embed = builder.Build();
            await Context.Channel.SendMessageAsync(null, false, embed);      //Confirmation on message sent
        }
    }
}
