using System.Collections.Generic;
using System.Linq;
using System.IO;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Net;
using AngleSharp;
using Telegram.Bot;
using Telegram.Bot.Types;
using System.Text;
//using Telegram.Bot.Types.InputFiles;

namespace parser_html
{
	class Message
	{
		public enum ContentType
		{
			Photo,
			Video,
			Gif
		}

		public readonly string text;
		public readonly List<string> links;
		public ContentType contentType { get; private set; }

		public Message(AngleSharp.Dom.IElement element)
		{
			links = new List<string>();
			if (element.TextContent.Contains("Фотография")) contentType = ContentType.Photo;
			if (element.TextContent.Contains("Файл")) contentType = ContentType.Gif;
			if (element.TextContent.Contains("Видеозапись")) contentType = ContentType.Video;

			//разбиение на строки и удаление пустых строк
			var content = element.TextContent
				.Replace("  ", "")
				.Replace("Фотография", "")
				.Replace("Видеозапись", "")
				.Replace("Файл", "")
				.Split('\n')
				.Where(x => !string.IsNullOrWhiteSpace(x)).
				ToArray();
			text = content[1];

			if (content.Length > 2)
			{
				for (int i = 2; i < content.Length; i++) 
					links.Add(content[i]);
			}
		}

		private string LinkToGif(string link)
		{
			string gifLink = "";
			string html = new WebClient().DownloadString(link);
			var doc = new HtmlParser().ParseDocument(html);
			string className = "ViewerImage__image--1zqMP";
			var elements = doc.QuerySelectorAll("img").Where(item => item.ClassName == className).ToArray();
			int len = elements.Length;
			gifLink = elements[0].SourceReference.ToString();
			return gifLink;
		}

		public void Download(string path)
		{
			var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

			if (links == null)
			{
				Console.BackgroundColor = ConsoleColor.Yellow;
				Console.ForegroundColor = ConsoleColor.Black;

				Console.WriteLine("--- Нечего скачивать ---");

				Console.ResetColor();

				Console.WriteLine($"Сообщение: {text}");
				return;
			}

			if (!Directory.Exists(path)) Directory.CreateDirectory(path);
			foreach (var link in links)
			{
				string file = link;
				string fileType = "";
				contentType = ContentType.Photo;
				
				if (file.Contains(".jpg")) fileType = ".jpg";
				if (file.Contains(".gif")) fileType = ".gif";
				if (file.Contains(".png")) fileType = ".png";
				if (file.Contains("https://vk.com/video")) contentType = ContentType.Video;
				if (file.Contains("https://vk.com/doc")) contentType = ContentType.Gif;

				using (WebClient webClient = new WebClient())
				{
					Directory.CreateDirectory(path);
					System.IO.File.WriteAllText((path + "\\mem.txt"), text);
					try
					{
						Console.WriteLine("=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=");

						Console.ForegroundColor = ConsoleColor.Blue;
						Console.Write("Сообщение: "); Console.ForegroundColor = ConsoleColor.White;
						Console.WriteLine(text);

						Console.ForegroundColor = ConsoleColor.DarkYellow;
						Console.Write("Путь сохранения: "); Console.ForegroundColor = ConsoleColor.White;
						Console.WriteLine(path);

						Console.Write($"Идет скачивание файла из ");
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.WriteLine(file);

						Console.ResetColor();

						if (contentType == ContentType.Gif)
						{
							file = LinkToGif(file);
							fileType = ".gif";
						}
						if (contentType == ContentType.Photo || contentType == ContentType.Gif)
						{
							webClient.DownloadFile(file, $"{path}\\mem{fileType}");
							Console.ForegroundColor = ConsoleColor.Green;
							Console.WriteLine("Успех!");
							Console.ResetColor();
						}
						else if (contentType == ContentType.Video)
						{
							Console.BackgroundColor = ConsoleColor.Yellow;
							Console.ForegroundColor = ConsoleColor.Black;
							Console.WriteLine("!!! Скачивание видео пропущено !!!");
							throw new Exception("Скачивание видео из вк недоступно!");
						}
					}
					catch (Exception ex)
					{
						string errorMsg = $"{text} - {file}";
						Console.ForegroundColor = ConsoleColor.Black;
						Console.BackgroundColor = ConsoleColor.Red;
						Console.WriteLine(errorMsg);
						Console.ResetColor();

						System.IO.File.AppendAllText($"{desktop}\\logs.txt", $"{errorMsg}\n");
					}
				}
			}
		}

		public void Send(TelegramBotClient client, string chatID, uint cooldownMilliseconds)
		{
			if (links.Count > 0 && contentType == ContentType.Photo)
			{
				List<IAlbumInputMedia> photos = new List<IAlbumInputMedia>();

				string description = text;
				int count = 0;

				foreach (var file in links)
				{
					InputFile photo;
					try
					{
						photo = InputFile.FromUri(file);
					}
					catch { return; }

					InputMediaPhoto photo1 = new InputMediaPhoto(photo);
					if (count == 0) photo1.Caption = description;
					photos.Add(photo1);
					count++;
				}
				Console.WriteLine(description);
				DebugInfo(description);

				#region СООБЩЕНИЕ С НЕСКОЛЬКИМИ КАРТИНКАМИ
				if (links.Count > 1)
				{
					client.SendMediaGroupAsync(
						chatId: chatID,
						media: photos
						);
					Console.ForegroundColor = ConsoleColor.Green;

					foreach (var path in links)
					{
						Console.WriteLine(path);
						DebugInfo(path);
					}
					Console.ResetColor();
				}
				#endregion
				#region СООБЩЕНИЕ С ОДНОЙ КАРТИНКОЙ
				else
				{
					string msg = links[0];
					try
					{
						client.SendPhotoAsync(
							chatId: chatID,
							photo: InputFile.FromUri(links[0]),
							caption: description
							);
						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine(msg);
						Console.ResetColor();
						DebugInfo(msg);
					}
					catch
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine(msg);
						Console.ResetColor();
						DebugInfo(msg);
					}
				}
				#endregion
				Thread.Sleep((int)cooldownMilliseconds);
			}
		}

		static void DebugInfo(string info)
		{
			System.IO.File.AppendAllText($"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\tg_debug.txt", info + "\n");
		}
	}

	internal class Program
	{
		static void Main(string[] args)
		{
			#region СКАЧИВАНИЕ СООБЩЕНИЙ
			string backUpMessagesPath = @"C:\\Users\\admin\\Downloads\\Messages\";
			var backedUpMessages = Directory.GetFiles(backUpMessagesPath);
			string token = "tgToken";
			string channelId = "channelID";

			List<Message> messages = new List<Message>();
			TelegramBotClient client = new TelegramBotClient(token);

			foreach (var htmlFile in backedUpMessages)
			{
				Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
				string htmlCode = System.IO.File.ReadAllText(htmlFile, Encoding.GetEncoding(1251));
				var doc = new HtmlParser().ParseDocument(htmlCode);

				//поиск тека div класса message
				var elements = doc
					.QuerySelectorAll("div")
					.Where(item => item.ClassName == "message" && !item.TextContent.Contains("Сообщение удалено"));

				foreach (var element in elements)
				{
					var msg = new Message(element);
					messages.Add(msg);
				}
			}

			messages.Reverse();

			for (int i = 0; i < messages.Count; i++)
			{
				var message = messages[i];

				Console.ForegroundColor = ConsoleColor.Cyan;
				var percent = Math.Round(100 * (float)(i + 1) / messages.Count, 2);
				Console.WriteLine($"{i + 1} из {messages.Count} ({percent}%)");
				Console.ResetColor();

				message.Send(client, channelId, cooldownMilliseconds: 2000);
			}

			Console.WriteLine("Скачивание завершено!");
			Console.Beep();
			#endregion
		}
	}
}