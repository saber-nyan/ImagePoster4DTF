using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Flurl.Http;
using Newtonsoft.Json.Linq;

namespace ImagePoster4DTF {
	public class DtfClient {
		private readonly FlurlClient _client = new FlurlClient().EnableCookies();

		public DtfClient() {
			// Пиздец. Помогите.
			_client
				.WithHeader("accept",
					"text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9")
				.WithHeader("accept-encoding", "gzip, deflate, br")
				.WithHeader("accept-language", "en-US,en;q=0.9")
				.WithHeader("cache-control", "no-cache")
				.WithHeader("pragma", "no-cache")
				.WithHeader("sec-fetch-dest", "document")
				.WithHeader("sec-fetch-mode", "navigate")
				.WithHeader("sec-fetch-site", "none")
				.WithHeader("sec-fetch-user", "?1")
				.WithHeader("upgrade-insecure-requests", "1")
				.WithHeader("user-agent",
					"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36");
		}

		public async Task Login(string username, string password) {
			var firstVisitResponse = await _client.Request("https://dtf.ru/").GetAsync();
			Console.WriteLine($"Done first visit: {firstVisitResponse}");

			_client
				.WithCookie(new Cookie("pushVisitsCount", "1", "/", ".dtf.ru"))
				.WithCookie(new Cookie("adblock-state", "1", "/", ".dtf.ru"))
				.WithCookie(new Cookie("audio_player_volume", "0.75", "/", ".dtf.ru"))
				.WithCookie(new Cookie("is_news_widget_closed", "false", "/", ".dtf.ru"));

			var loginResponse = await _client.Request("https://dtf.ru/auth/simple/login")
				.WithHeader("referer", "https://dtf.ru/")
				.WithHeader("sec-fetch-dest", "empty")
				.WithHeader("sec-fetch-mode", "cors")
				.WithHeader("sec-fetch-site", "same-origin")
				.WithHeader("x-js-version", "599ad322")
				.WithHeader("x-this-is-csrf", "THIS IS SPARTA!")
				.PostUrlEncodedAsync(new Dictionary<string, string> {
					{"values[login]", username},
					{"values[password]", password},
					{"mode", "raw"}
				})
				.ReceiveString();
			var loginJson = JObject.Parse(loginResponse);
			Console.WriteLine($"Logged in, response: {loginJson}");

			// Да, поля rc может не оказаться, но мы просто прокидываем все исключения наверх.
			// Мне слишком лень городить простыню детальной обработки ошибок.
			if ((int) loginJson["rc"] != 200) {
				loginJson.TryGetValue("rm", out var errorDescription);
				throw new ApplicationException(errorDescription != null
					? (string) errorDescription
					: "Сервер вернул некорректный ответ");
			}
		}

		public async Task<JObject> CreatePost() {
			var writingResponse = await _client.Request("https://dtf.ru/writing?to=u&mode=ajax")
				.WithHeader("referer", "https://dtf.ru/")
				.WithHeader("sec-fetch-dest", "empty")
				.WithHeader("sec-fetch-mode", "cors")
				.WithHeader("sec-fetch-site", "same-origin")
				.GetAsync()
				.ReceiveString();
			var writingJson = JObject.Parse(writingResponse);
			Console.WriteLine($"Writing sent (post created): {writingJson}");
			return writingJson["module.auth"] as JObject;
		}
	}
}
