using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Flurl.Http;
using IniParser.Model;
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

		public async Task<JObject> Login(string username, string password) {
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

			var checkResponse = await _client.Request("https://dtf.ru/auth/check?mode=raw")
				.WithHeader("referer", "https://dtf.ru/")
				.WithHeader("sec-fetch-dest", "empty")
				.WithHeader("sec-fetch-mode", "cors")
				.WithHeader("sec-fetch-site", "same-origin")
				.GetAsync()
				.ReceiveString();
			var checkJson = JObject.Parse(checkResponse);
			Console.WriteLine($"Checked, response: {checkJson}");

			if ((int) checkJson["rc"] == 200) return checkJson["data"] as JObject;

			checkJson.TryGetValue("rm", out var checkErrorDescription);
			throw new ApplicationException(checkErrorDescription != null
				? (string) checkErrorDescription
				: "Сервер вернул некорректный ответ");
		}

		public void LoadCookies(IEnumerator<Property> cookies) {
			while (cookies.MoveNext()) {
				var cookie = cookies.Current;
				if (cookie != null) _client.WithCookie(cookie.Key, cookie.Value);
			}
		}

		public IDictionary<string, Cookie> SaveCookies() {
			return _client.Cookies;
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

		public async Task<JObject> UploadFiles(IEnumerable<string> paths) {
			_client.WithCookie("pushVisitsCount", "-49999");

			var uploadResponse = await _client.Request("https://dtf.ru/andropov/upload")
				.WithHeader("referer", "https://dtf.ru/")
				.WithHeader("sec-fetch-dest", "empty")
				.WithHeader("sec-fetch-mode", "cors")
				.WithHeader("sec-fetch-site", "same-origin")
				.WithHeader("x-this-is-csrf", "THIS IS SPARTA!")
				.PostMultipartAsync(mp => {
					var i = 0;
					foreach (var path in paths) {
						mp.AddFile($"file_{i}", path);
						++i;
					}

					mp.AddString("render", "false");
				})
				.ReceiveString();
			var uploadJson = JObject.Parse(uploadResponse);
			Console.WriteLine($"Uploaded: {uploadJson}");
			return uploadJson;
		}

		public async Task<JObject> SaveDraft(string title, int userId, JArray uploadedImages) {
			var initialData = new Dictionary<string, string> {
				{"entry[id]", "0"},
				{"entry[user_id]", userId.ToString()},
				{"entry[type]", "1"},
				{"entry[title]", title},
				{"entry[url]", ""},
				{"entry[date]", "0"},
				{"entry[date_str]", ""},
				{"entry[modification_date]", "0"},
				{"entry[modification_date_str]", ""},
				{"entry[is_published]", "false"},
				{"entry[subsite_id]", userId.ToString()},
				{"entry[subsite_name]", ""},
				{"entry[removed]", "false"},
				{"entry[custom_style]", ""},
				{"entry[path]", ""},
				{"entry[forced_to_mainpage]", "false"},
				{"entry[is_advertisement]", "false"},
				{"entry[is_enabled_instant_articles]", "false"},
				{"entry[is_enabled_amp]", "true"},
				{"entry[is_approved_for_public_rss]", "false"},
				{"entry[is_disabled_likes]", "false"},
				{"entry[is_disabled_comments]", "false"},
				{"entry[is_disabled_best_comments]", "false"},
				{"entry[is_disabled_ad]", "false"},
				{"entry[is_wide]", "false"},
				{"entry[is_still_updating]", "false"},
				{"entry[withheld]", "false"},
				{"entry[locked_by_admin]", "false"},
				{"entry[is_show_thanks]", "false"},
				{"entry[is_clean_cover]", "0"},
				{"entry[is_editorial]", "false"},
				{"entry[is_disabled_apps]", "false"},
				{"entry[is_special]", "false"},
				{"entry[is_filled_by_editors]", "false"},
				{"entry[is_holdonmain]", "false"},
				{"entry[is_holdonflash]", "0"},
				{"entry[external_access_link]", ""},
				{"entry[attaches]", ""},
				{"entry[announcement_links]", ""},
				{"autosaving", "true"},
				{"mode", "raw"}
			};

			var i = 0;
			foreach (var image in uploadedImages) {
				initialData[$"entry[entry][blocks][{i}][type]"] = "media";
				initialData[$"entry[entry][blocks][{i}][cover]"] = "false";
				initialData[$"entry[entry][blocks][{i}][data][items][0][image][type]"] = "image";
				initialData[$"entry[entry][blocks][{i}][data][items][0][image][data][uuid]"] =
					(string) image["data"]?["uuid"];
				initialData[$"entry[entry][blocks][{i}][data][items][0][image][data][width]"] =
					((int) image["data"]?["width"]).ToString();
				initialData[$"entry[entry][blocks][{i}][data][items][0][image][data][height]"] =
					((int) image["data"]?["height"]).ToString();
				initialData[$"entry[entry][blocks][{i}][data][items][0][image][data][size]"] =
					((long) image["data"]?["size"]).ToString();
				initialData[$"entry[entry][blocks][{i}][data][items][0][image][data][color]"] =
					(string) image["data"]?["color"];
				initialData[$"entry[entry][blocks][{i}][data][items][0][image][data][external_service]"] = "";
				initialData[$"entry[entry][blocks][{i}][data][items][0][image][render]"] = (string) image["render"];
				initialData[$"entry[entry][blocks][{i}][data][items][0][title]"] = ""; // TODO
				initialData[$"entry[entry][blocks][{i}][data][items][0][author]"] = "";
				initialData[$"entry[entry][blocks][{i}][data][with_border]"] = "false";
				initialData[$"entry[entry][blocks][{i}][data][with_background]"] = "false";
				++i;
			}

			var saveResponse = await _client.Request("https://dtf.ru/writing/save")
				.WithHeader("referer", "https://dtf.ru/")
				.WithHeader("sec-fetch-dest", "empty")
				.WithHeader("sec-fetch-mode", "cors")
				.WithHeader("sec-fetch-site", "same-origin")
				.WithHeader("x-js-version", "599ad322")
				.WithHeader("x-this-is-csrf", "THIS IS SPARTA!")
				.PostUrlEncodedAsync(initialData)
				.ReceiveString();
			var saveJson = JObject.Parse(saveResponse);
			Console.WriteLine($"Saved draft: {saveJson}");
			return saveJson;
		}
	}
}
