using Discord;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;
namespace SwedishBOT.modules
{
    public class Games : ModuleBase<SocketCommandContext>
    {
        [Command("meme")]
        public async Task reddit(string subreddit = null)
        {
            var client = new HttpClient();
            var result = await client.GetStringAsync($"https://reddit.com/r/{subreddit ?? "DankMemes"}/random.json?limit=1");
            if (!result.StartsWith("["))
            {
                await Context.Channel.SendMessageAsync("This subreddit doesnt exist");
                return;
            }
            JArray arr = JArray.Parse(result);
            JObject post = JObject.Parse(arr[0]["data"]["children"][0]["data"].ToString());
            var builder = new EmbedBuilder()
                .WithImageUrl(post["url"].ToString())
                .WithColor(new Color(169, 0, 169))
                .WithTitle(post["title"].ToString())
                .WithUrl("https://reddit.com" + post["permalink"].ToString())
                .WithFooter($"🗨 {post["num_comments"]} Comments | ⬆️ {post["ups"]} Up-Votes");
            var embed = builder.Build();
            var message = Context.Message;
            await Context.Channel.SendMessageAsync(null, false, embed);
        }
    }
}