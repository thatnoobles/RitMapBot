#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8604

using Discord;
using Discord.WebSocket;
using System;
using System.IO;
using System.Threading;

namespace RitMapBot
{
	internal class Program
	{
		public static DiscordSocketClient client = new DiscordSocketClient();
		public static List<ulong> subscribedChannelIds = new List<ulong>();

		private const string CHANNELS_PATH = "channels.txt";

		private static Task Main(string[] args) => new Program().MainAsync(args);

		private async Task MainAsync(string[] args)
		{
			// Get all pins currently on website, but don't post them to feed
			// (server would get like 500 messages!)
			await Pin.UpdatePinDict();

			client.Log += Log;
			client.MessageReceived += OnMessageCreated;

			// Start Discord bot
			await client.LoginAsync(Discord.TokenType.Bot, File.ReadAllText("token.txt"));
			await client.StartAsync();

			// Load subscribed channels into memory from file
			// (saving IDs in a file stops them from being unsubscribed if bot goes down)
			if (File.Exists(CHANNELS_PATH)) foreach (string line in File.ReadAllLines(CHANNELS_PATH))
			{
				if (ulong.TryParse(line, out ulong id))
				{
					subscribedChannelIds.Add(id);
				}
			}

			while (true)
			{
				await Pin.UpdatePinDict(true);
				Thread.Sleep(5000);
			}

			// Don't close window automatically
			// await Task.Delay(-1);
		}

		private async Task OnMessageCreated(SocketMessage message)
		{
			// Listen for user commands. All commands start with "!map", followed by the desired action:
			// subscribe	Subscribes the current channel to the map pin feed
			// unsubscribe	Stops current channel from receiving map pin feed messages
			// help			Shows list of commands
			// source		Shows link to GitHub page
			
			// Ignore bot messages and messages that don't start with the "!map" prefix
			if (message.Author.IsBot || !message.Content.StartsWith("!map")) return;

			// If no argument is provided, notify user
			if (message.Content.Split(' ').Length == 1)
			{
				await message.Channel.SendMessageAsync("Usage: `!map [argument]` (try `!map help`)");
				return;
			}

			// Get command argument (if valid)
			string arg = message.Content.Trim().ToLower().Split(' ')[1];

			switch (arg)
			{
				case "subscribe":

					// User must be admin for this command
					if (!((SocketGuildUser)message.Author).GuildPermissions.Administrator)
					{
						await message.Channel.SendMessageAsync("You must be an administrator to run this command.");
						return;
					}

					File.AppendAllText(CHANNELS_PATH, $"{message.Channel.Id.ToString()}\n");
					subscribedChannelIds.Add(message.Channel.Id);

					await message.Channel.SendMessageAsync($"Subscribed #{message.Channel.Name} to the RITMap feed.");
					break;

				case "unsubscribe":

					// User must be admin for this command
					if (!((SocketGuildUser)message.Author).GuildPermissions.Administrator)
					{
						await message.Channel.SendMessageAsync("You must be an administrator to run this command.");
						return;
					}

					await message.Channel.SendMessageAsync($"Unsubscribed #{message.Channel.Name} from the RITMap feed.");
					break;

				case "source":
					await message.Channel.SendMessageAsync("**GitHub:** https://github.com/thatnoobles/ritmap-bot \n(Created by thatnoobles#4418)");
					break;

				case "help":
					await message.Channel.SendMessageAsync
					(
						"Usage: `!map [argument]` - list of arguments:\n" +
						"`subscribe` - *Subscribes the current channel to the RITMap feed (admin only)*\n" +
						"`unsubscribe` - *Stops sending feed messages to the current channel (admin only)*\n" +
						"`source` - *Sends a link to this bot's GitHub page*\n" +
						"`help` - *Sends this message*"
					);
					break;

				default:
					await message.Channel.SendMessageAsync("Invalid argument, (try `!map help`)");
					break;
			}
		}

		private Task Log(LogMessage message)
		{
			Console.WriteLine(message);
			return Task.CompletedTask;
		}
	}
}