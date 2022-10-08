#pragma warning disable CS8600
#pragma warning disable CS8602
#pragma warning disable CS8604

using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Net.Http;

namespace RitMapBot
{
	public class Pin
	{
		public static Dictionary<string, Pin> allPins = new Dictionary<string, Pin>();	// Key is pin ID, value is associated pin

		public string Id { get; }
		public string Title { get; }
		public double Latitude { get; }
		public double Longitude { get; }
		public string Text { get; }
		public CategoryType Category { get; }
		public DateTime UploadDate { get; }

		public enum CategoryType { Review, Cool, Shitpost }

		public Pin(string id, string title, double latitude, double longitude, string text, string category, string rawDateTime)
		{
			Id = id;
			Title = title;
			Latitude = latitude;
			Longitude = longitude;
			Text = text;

			Category = category switch
			{
				"review" => CategoryType.Review,
				"cool" => CategoryType.Cool,
				_ => CategoryType.Shitpost
			};

			UploadDate = DateTime.Parse(rawDateTime);
		}
	
		public static async Task UpdatePinDict(bool postInFeed = false)
		{
			List<Pin> newPinsAdded = new List<Pin>();

			// Get up-to-date list of pins from ritmaps
			HttpClient client = new HttpClient();
			HttpResponseMessage responseMessage = await client.GetAsync("https://ritmap.com/api/getpins");

			// Get JSON
			responseMessage.EnsureSuccessStatusCode();
			string responseBody = await responseMessage.Content.ReadAsStringAsync();

			// Parse JSON and add any news pins that weren't there already
			JArray responsePins = JArray.Parse(responseBody);

			foreach (JObject pinJObject in responsePins)
			{
				if (allPins.ContainsKey(pinJObject["id"].Value<string>()))
					continue;

				Pin newPin = new Pin
				(
					pinJObject["id"].Value<string>(),
					pinJObject["title"].Value<string>(),
					pinJObject["latitude"].Value<double>(),
					pinJObject["longitude"].Value<double>(),
					pinJObject["text"].Value<string>(),
					pinJObject["category"].Value<string>(),
					pinJObject["date"].Value<string>()
				);

				allPins.Add(pinJObject["id"].Value<string>(), newPin);
				newPinsAdded.Add(newPin);
			}

			Console.WriteLine($"Local pin record updated, added {newPinsAdded.Count} new pin(s)");

			// Send a feed message about any new pins (if specified that this should happen)
			if (!postInFeed) return;

			foreach (Pin pin in newPinsAdded)
			{
				EmbedBuilder pinEmbedBuilder = new EmbedBuilder()
				{
					Color = pin.Category switch
					{
						Pin.CategoryType.Review => Color.Red,
						Pin.CategoryType.Cool => Color.Green,
						_ => Color.Blue
					},
					Description = pin.Text,
					Footer = new EmbedFooterBuilder() { Text = $"Submitted {pin.UploadDate}" },
					Title = pin.Title,
					Url = $"https://ritmap.com/pin?lat={pin.Latitude}&lng={pin.Longitude}"
				};

				foreach (ulong id in Program.subscribedChannelIds)
				{
					if (Program.client.GetChannel(id) == null) return;
					await ((SocketTextChannel)Program.client.GetChannel(id)).SendMessageAsync("New pin!", false, pinEmbedBuilder.Build());
				}
			}
		}
	}
}