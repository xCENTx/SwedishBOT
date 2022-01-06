using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace SwedishBOT.Services
{
    public class CommandHandler : InitializedService
    {
        private readonly IServiceProvider _provider;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _service;
        private readonly IConfiguration _config;

        public CommandHandler(IServiceProvider provider, DiscordSocketClient client, CommandService service, IConfiguration config)
        {
            _provider = provider;
            _client = client;
            _service = service;
            _config = config;
        }

        //Event Handler
        public override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            //MODERATION
            _client.MessageReceived += MessageSpamFilter;       //Spam URL LINK Handler
            _client.MessageDeleted += MessageDeleted;           //Deleted Messages
            _client.MessageUpdated += MessageEdited;            //Edited Messages
            _client.UserJoined += MemberJoinedServer;           //Member Joined Server
            _client.UserLeft += MemberLeftServer;               //Member Left Server
            _client.UserUpdated += OnUserUpdated;               //User Profile Updated
            _client.UserBanned += OnUserBanned;                 //User Banned
            _client.UserUnbanned += OnUserUnBanned;             //User Unbaned
            
            //Command Handler
            _client.MessageReceived += OnMessageReceived;       //Message Sent in chat
            
            //Execution Handler
            _service.CommandExecuted += OnCommandExecuted;
            
            await _service.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
        }

        //DO NOT ADJUST
        #region COMMAND HANDLER 
        private async Task OnMessageReceived(SocketMessage arg)
        {
            if (!(arg is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;

            var argPos = 0;
            var context = new SocketCommandContext(_client, message);

            //Attempt to respond to "good bot"
            if (message.Content.Contains("good bot", StringComparison.CurrentCultureIgnoreCase))
            {
                var umessage = $"Thank You {arg.Author.Mention}, you're too kind <:swedL:771673043269320754>";
                await context.Channel.SendMessageAsync(umessage);
                return;
            }

            //Bot listening for prefix / command
            if (!message.HasStringPrefix(_config["prefix"], ref argPos) && !message.HasMentionPrefix(_client.CurrentUser, ref argPos)) return;
            await _service.ExecuteAsync(context, argPos, _provider);
        }

        private async Task OnCommandExecuted(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (command.IsSpecified && !result.IsSuccess) await context.Channel.SendMessageAsync($"Error: {result}");
        }
        #endregion

        #region MODERATION

        /*SUMMARY
            //      Filters Bad Words               | Yellow Embed
            //      - Responds with edited message in same channel
            ///     Logs and Deletes Spam Links     | Red Embed
            ///     - #audit-log
        */
        private async Task MessageSpamFilter(SocketMessage arg)
        {
            //Establish some variables
            var msg = arg as SocketUserMessage;
            var context = new SocketCommandContext(_client, msg);
            var user = context.User.Id;

            if (arg.Author.Id == _client.CurrentUser.Id) return;

            if (arg.Content == "") return;

            //ADMIN / MOD FILTER 
            if (user == Admins.Swedish || user == Admins.TheFrieber || user == Admins.Prince) return;

            //Bad Word Filter
            //- Adjustments need to be made
            //for instance , once a bad word is detected the bot will copy the entirety of the message,
            //spit it out and filter the first bad word it encounters in the list.
            //foreach should be changed to
            List<string> badWords = new List<string>
            {
               "hack",
               "crack",
               "cheat",
               "aimbot",
               "injector",
            };
            string FILTERMESSAGE = arg.Content;
            foreach (string word in badWords)
            {
                int index = arg.Content.IndexOf(word, StringComparison.CurrentCultureIgnoreCase);
                if (index != -1)
                {
                    FILTERMESSAGE = arg.Content.Replace(word, "*beep*");
                }
            }
            var userrub = context.User as SocketGuildUser;
            var role = (userrub as IGuildUser).Guild.Roles.FirstOrDefault(x => x.Id == Roles.Trusted);
            if (!userrub.Roles.Contains(role))
            {

                //URL SPAM FILTER
                var myRegex = new Regex(@"\b(?:https?://|www\.)\S+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                HttpClient httpclient = new HttpClient();
                var postReqvalues = new Dictionary<string, string>
                {
                    {"strictness","0" },
                    {"fast","true" }
                };
                var Content = new FormUrlEncodedContent(postReqvalues);

                // You will need to use your own API
                // IpQualityScore is good
                foreach (Match match in myRegex.Matches(arg.Content))
                {
                    string url = match.Value.Replace(":", "%3A").Replace("/", "%2F");
                    HttpResponseMessage response = await httpclient.PostAsync("USE YOUR OWN API" + url, Content);
                    string responseString = await response.Content.ReadAsStringAsync();
                    Root parsedResponse = JsonConvert.DeserializeObject<Root>(responseString);
                    if (parsedResponse.@unsafe == true || parsedResponse.risk_score > 80 || parsedResponse.adult == true)
                    {
                        //Website Filter
                        if (url.Contains("tenor"))
                            return;

                        //Build our embedded post
                        var builder = new EmbedBuilder()
                                .WithThumbnailUrl(context.User.GetAvatarUrl() ?? context.User.GetDefaultAvatarUrl())
                                .WithDescription("")
                                .WithColor(new Color(169, 0, 0))
                                .WithTitle("SPAM MESSAGE DELETED")
                                .AddField("USER", arg.Author.Mention, false)
                                .AddField("CHANNEL", $"<#{arg.Channel.Id}>", false)
                                .AddField($"MESSAGE CONTENT", arg.Content, false)
                                .WithFooter($"USER ID: {arg.Author.Id}")
                                .WithCurrentTimestamp();
                        var embed = builder.Build();

                        //Extra Actions
                        await arg.Channel.DeleteMessageAsync(arg);                      //Delete Bad Message
                        var logChannel = _client.GetChannel(Channels.Spam_Log) as IMessageChannel;    //Further Defining the actions for the log
                        await logChannel.SendMessageAsync(null, false, embed);          //Finally we make a log of the event
                        break;
                    }
                    Console.WriteLine(responseString);
                }
            }
        }
        
        /*SUMMARY
            //      Logs Edited Messages            | Yellow Embed
            //      - #audit-log
        */
        private async Task MessageEdited(Cacheable<IMessage, ulong> log, SocketMessage message, ISocketMessageChannel channel)
        {
            // check if the message exists in cache; if not, we cannot report what was removed
            if (!log.HasValue) return;
            var _oldMSG = log.Value;
            var logChannel = _client.GetChannel(Channels.Audit_Log) as SocketTextChannel;
            var builder = new EmbedBuilder()
                .WithAuthor(message.Author)
                .WithColor(new Color(169, 169, 0))
                .WithTitle("MESSAGE EDITED")
                .AddField("CHANNEL", $"<#{channel.Id}>", false)
                .AddField("OLD CONTENT", $"{_oldMSG.Content}", false)
                .AddField("NEW CONTENT", $"{message.Content}", false)
                .WithFooter($"USER ID: {message.Author.Id}")
                .WithCurrentTimestamp();
            var embed = builder.Build();
            await logChannel.SendMessageAsync(null, false, embed);
        }
        
        /*SUMMARY
            //      Logs Deleted Messages           | Yellow Embed
            //      - #audit-log
        */
        private async Task MessageDeleted(Cacheable<IMessage, ulong> msg, ISocketMessageChannel channel)
        {
            // check if the message exists in cache; if not, we cannot report what was removed
            if (!msg.HasValue) return;
            var message = msg.Value;
            string a = message.Content;
            if (message.Author.IsBot) return;
            if (message.Content.Contains("nf.")) return;
            int index = a.IndexOf("hack", StringComparison.CurrentCultureIgnoreCase);
            if (index != -1) return;
            var logChannel = _client.GetChannel(Channels.Audit_Log) as SocketTextChannel;
            var builder = new EmbedBuilder()
                .WithAuthor(message.Author)
                .WithColor(new Color(169, 169, 0))
                .WithTitle("MESSAGE DELETED")
                .AddField("CHANNEL", $"<#{channel.Id}>", false)
                .AddField("CONTENT", $"{message.Content}", false)
                .WithFooter($"USER ID: {message.Author.Id}")
                .WithCurrentTimestamp();
            var embed = builder.Build();
            await logChannel.SendMessageAsync(null, false, embed);
        }
        
        /*SUMMARY
            //      Logs Member Left                | Red Embed
        */
        private async Task MemberLeftServer(SocketGuildUser arg)
        {
            var channel = _client.GetChannel(Channels.goodbye) as SocketTextChannel;
            var builder = new EmbedBuilder()
                .WithThumbnailUrl(arg.GetAvatarUrl() ?? arg.GetDefaultAvatarUrl())
                .WithDescription($"{arg.Mention} has left **{channel.Guild.Name}**")
                .WithColor(new Color(169, 0, 0))
                .AddField("User Name", arg, true)
                .AddField("Created", arg.CreatedAt.ToString("MM/dd/yyyy"))
                .AddField($"Joined {arg.Guild}", arg.JoinedAt.Value.ToString("MM/dd/yyyy"))
                .AddField($"Roles", String.Join(" ", arg.Roles.Select(x => x.Mention)))
                .WithFooter($"USER ID: {arg.Id}")
                .WithCurrentTimestamp();
            var embed = builder.Build();
            await channel.SendMessageAsync(null, false, embed);

            //Remove roles
            //Note , we will want to retain the role "warning" if the member has it. This can be done later
            var roles = arg.Roles.Select(x => x.Id);
            await arg.RemoveRolesAsync(roles);
        }
        
        /*SUMMARY
            //      Kicks new account (<36hrs)      | Red Embed
            //      - #alt-log
            ///     Flags Fresh accounts (<7days)   | Yellow Embed
            ///     - #alt-log
            ////    Logs Member Joined              | Green Embed
            ////    - #welcome
        */
        private async Task MemberJoinedServer(SocketGuildUser arg)
        {
            //New Account Detection (ANTI-ALT)
            var timeCreated = arg.CreatedAt.UtcDateTime;
            var kickTimer = arg.JoinedAt.Value.UtcDateTime;

            //New Account ~36hrs = Kick
            if ((kickTimer - timeCreated).TotalHours <= 36)
            {
                var LOGEVENT = _client.GetChannel(Channels.Alt_Log) as SocketTextChannel;

                //LOG EVENT
                var builder1 = new EmbedBuilder()
                    .WithThumbnailUrl(arg.GetAvatarUrl() ?? arg.GetDefaultAvatarUrl())
                    .WithDescription($"{arg.Mention} has joined **{LOGEVENT.Guild.Name}**")
                    .WithTitle("KICKED NEW ACCOUNT!")
                    .WithColor(new Color(169, 0, 0))
                    .AddField("Username", arg, true)
                    .AddField("Created", arg.CreatedAt.ToString("MM/dd/yyyy"))
                    .AddField($"Joined {arg.Guild}", arg.JoinedAt.Value.ToString("MM/dd/yyyy"))
                    .AddField($"Roles", String.Join(" ", arg.Roles.Select(x => x.Mention)))
                    .WithFooter($"USER ID: {arg.Id}")
                    .WithCurrentTimestamp();
                var LOG = builder1.Build();

                //Log Event
                await LOGEVENT.SendMessageAsync(null, false, LOG);

                //Remove any roles 
                var roles = arg.Roles.Select(x => x.Id);
                await arg.RemoveRolesAsync(roles);

                //Kick User
                await arg.KickAsync("Account is less than 24hrs old");
                return;
            }

            //Baby Account ~168hrs
            if ((kickTimer - timeCreated).TotalHours <= 168)
            {
                var LOGEVENT = _client.GetChannel(Channels.Alt_Log) as SocketTextChannel;
                var channel1 = _client.GetChannel(Channels.welcome) as SocketTextChannel;

                //LOG EVENT
                var builder1 = new EmbedBuilder()
                    .WithThumbnailUrl(arg.GetAvatarUrl() ?? arg.GetDefaultAvatarUrl())
                    .WithDescription($"{arg.Mention} has joined **{LOGEVENT.Guild.Name}**")
                    .WithTitle("NEW ACCOUNT!")
                    .WithColor(new Color(169, 169, 0))
                    .AddField("Username", arg, true)
                    .AddField("Created", arg.CreatedAt.ToString("MM/dd/yyyy"))
                    .AddField($"Joined {arg.Guild}", arg.JoinedAt.Value.ToString("MM/dd/yyyy"))
                    .AddField($"Roles", String.Join(" ", arg.Roles.Select(x => x.Mention)))
                    .WithFooter($"USER ID: {arg.Id}")
                    .WithCurrentTimestamp();
                var LOG = builder1.Build();
                
                //Log Event
                await LOGEVENT.SendMessageAsync(null, false, LOG);
                await channel1.SendMessageAsync(null, false, LOG);
                return;
            }
            
            //Log Event of Member Joining
            var channel = _client.GetChannel(Channels.welcome) as SocketTextChannel;
            var builder = new EmbedBuilder()
                .WithThumbnailUrl(arg.GetAvatarUrl() ?? arg.GetDefaultAvatarUrl())
                .WithDescription($"{arg.Mention} has joined **{channel.Guild.Name}**")
                .WithColor(new Color(0, 169, 0))
                .AddField("Username", arg, true)
                .AddField("Created", arg.CreatedAt.ToString("MM/dd/yyyy"))
                .AddField($"Joined {arg.Guild}", arg.JoinedAt.Value.ToString("MM/dd/yyyy"))
                .AddField($"Roles", String.Join(" ", arg.Roles.Select(x => x.Mention)))
                .WithFooter($"USER ID: {arg.Id}")
                .WithCurrentTimestamp();
            var embed = builder.Build();
            await channel.SendMessageAsync(null, false, embed);
        }

        /*SUMMARY
            //      Logs Avatar and Name Changes  | Purple Embed
            //      - #audit-log
        */
        private async Task OnUserUpdated(SocketUser arg1, SocketUser arg2)
        {
            var channel = _client.GetChannel(Channels.Audit_Log) as SocketTextChannel;

            //Avatar Updated
            if (arg1.GetAvatarUrl() != arg2.GetAvatarUrl())
            {
                var avabuilder = new EmbedBuilder()
                .WithAuthor(arg2)
                .WithThumbnailUrl(arg2.GetAvatarUrl() ?? arg2.GetDefaultAvatarUrl())
                .WithTitle("AVATAR UPDATE")
                .WithDescription($"{arg2.Mention}")
                .WithColor(new Color(169, 0, 169))
                .WithFooter($"USER ID: {arg2.Id}")
                .WithCurrentTimestamp();
                var avaembed = avabuilder.Build();
                await channel.SendMessageAsync(null, false, avaembed);
            }

            //Name Changed
            if (arg1.Username.ToString() != arg2.Username.ToString())
            {
                var builder = new EmbedBuilder()
                .WithAuthor(arg2)
                .WithThumbnailUrl(arg2.GetAvatarUrl() ?? arg2.GetDefaultAvatarUrl())
                .WithTitle("NAME CHANGE")
                .WithDescription($"NEW: {arg2.Mention}")
                .WithDescription($"OLD: {arg1}")
                .WithColor(new Color(169, 0, 169))
                .WithFooter($"USER ID: {arg2.Id}")
                .WithCurrentTimestamp();
                var embed = builder.Build();
                await channel.SendMessageAsync(null, false, embed);
            }

            //Roles changed?? cant remember
            if ((arg1 as SocketGuildUser).Roles.Count != (arg1 as SocketGuildUser).Roles.Count)
            {
                var getRoles = (arg1 as SocketGuildUser).Roles.Select(x => x.Mention);
                var getRoles2 = (arg2 as SocketGuildUser).Roles.Select(x => x.Mention);
                var builder = new EmbedBuilder()
                .WithAuthor(arg2)
                .WithThumbnailUrl(arg2.GetAvatarUrl() ?? arg2.GetDefaultAvatarUrl())
                .WithTitle("ROLE CHANGE")
                .WithDescription($"NEW: {String.Join(" ", getRoles2)}")
                .WithDescription($"OLD: {String.Join(" ", getRoles)}")
                .WithColor(new Color(169, 0, 169))
                .WithFooter($"USER ID: {arg2.Id}")
                .WithCurrentTimestamp();
                var embed = builder.Build();
                await channel.SendMessageAsync(null, false, embed);
            }
        }

        /*SUMMARY
            //      Logs banned users             | Red Embed
            //      - #ban-log
        */
        private async Task OnUserBanned(SocketUser arg1, SocketGuild arg2)
        {
            var channel = _client.GetChannel(Channels.Ban_Log) as SocketTextChannel;
            var builder = new EmbedBuilder()
            .WithAuthor(arg1)
            .WithThumbnailUrl(arg1.GetAvatarUrl() ?? arg1.GetDefaultAvatarUrl())
            .WithTitle("MEMBER BANNED")
            .WithDescription($"{arg1.Mention}")
            .WithColor(new Color(169, 0, 0))
            .WithFooter($"USER ID: {arg1.Id}")
            .WithCurrentTimestamp();
            var embed = builder.Build();
            await channel.SendMessageAsync(null, false, embed);
        }

        /*SUMMARY
            //      Logs unbanned users             | Red Embed
            //      #ban-log
        */
        private async Task OnUserUnBanned(SocketUser arg1, SocketGuild arg2)
        {
            var channel = _client.GetChannel(Channels.Ban_Log) as SocketTextChannel;
            var builder = new EmbedBuilder()
            .WithAuthor(arg1)
            .WithThumbnailUrl(arg1.GetAvatarUrl() ?? arg1.GetDefaultAvatarUrl())
            .WithTitle("MEMBER UN-BANNED")
            .WithDescription($"{arg1.Mention}")
            .WithColor(new Color(0, 169, 0))
            .WithFooter($"USER ID: {arg1.Id}")
            .WithCurrentTimestamp();
            var embed = builder.Build();
            await channel.SendMessageAsync(null, false, embed);
        }
        #endregion
    }
}