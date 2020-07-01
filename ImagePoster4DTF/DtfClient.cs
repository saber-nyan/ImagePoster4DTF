using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Flurl.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace ImagePoster4DTF {
	public class InvalidResponseException : ApplicationException {
		public InvalidResponseException(string description, int code) : base(description) {
			Code = code;
		}

		public InvalidResponseException(string description, Exception e) : base(description, e) { }
		public int Code { get; }
	}

	public class InvalidCredentialsException : ApplicationException { }

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
				.WithHeader("user-agent",
					"Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36")
				.WithHeader("referer", "https://dtf.ru/")
				.WithHeader("sec-fetch-dest", "empty")
				.WithHeader("sec-fetch-mode", "cors")
				.WithHeader("sec-fetch-site", "same-origin")
				.WithHeader("x-js-version", "599ad322")
				.WithHeader("x-this-is-csrf", "THIS IS SPARTA!")
				.WithCookie(new Cookie("pushVisitsCount", "1", "/", ".dtf.ru"))
				.WithCookie(new Cookie("adblock-state", "1", "/", ".dtf.ru"))
				.WithCookie(new Cookie("audio_player_volume", "0.75", "/", ".dtf.ru"))
				.WithCookie(new Cookie("is_news_widget_closed", "false", "/", ".dtf.ru"));
			Log.Information("DtfClient initialized");
		}

		private static JObject ParseAndCheckJson(string responseBody) {
			JObject parsedJson;
			try {
				parsedJson = JObject.Parse(responseBody);
			}
			catch (JsonReaderException e) {
				var errorDescription = $"Failed to parse JSON (source string: {responseBody})";
				Log.Error(e, errorDescription);
				throw new InvalidResponseException(errorDescription, e);
			}

			if (!parsedJson.ContainsKey("rc")) // Log.Information($"Response does not contains code: {parsedJson}");
				return parsedJson;

			// response code
			var codeRaw = parsedJson["rc"];

			if (codeRaw.Type != JTokenType.Integer) {
				Log.Warning($"Unknown response code type ({codeRaw.Type})");
				return parsedJson;
			}

			var code = (int) codeRaw;
			if (code == 200) return parsedJson; //! Successful path

			Log.Error($"Server returned code {code}, aborting");

			if (parsedJson.ContainsKey("rm")) { // response message
				var reason = (string) parsedJson["rm"];
				Log.Error($"Reason: {reason}");
				throw new InvalidResponseException(reason, code);
			}

			Log.Error($"Unknown reason... Full JSON: {parsedJson}");
			throw new InvalidResponseException("Сервер вернул некорректный ответ.", code);
		}

		private async Task<JObject> CheckAccount() {
			var checkResponse = await _client.Request("https://dtf.ru/auth/check?mode=raw")
				.GetAsync()
				.ReceiveString();
			JObject checkJson;
			try {
				checkJson = ParseAndCheckJson(checkResponse);
			}
			catch (InvalidResponseException e) {
				if (e.Code == 403) { // returns 403 on incorrect cookie
					Log.Warning("Failed to check account: invalid cookie");
					throw new InvalidCredentialsException();
				}

				Log.Warning($"Failed to check account: unknown error {e}");
				throw;
			}

			Log.Debug("Successfully checked account");
			return checkJson;
		}

		public string GetCookie() {
			_client.Cookies.TryGetValue("osnova-remember", out var cookie);
			return cookie?.Value;
		}

		public async Task<JObject> LoginWithCookie(string cookie) {
			Log.Debug($"Logging in w/ cookie len = {cookie.Length}");
			_client.WithCookie("osnova-remember", cookie);
			JObject result;
			try {
				result = await CheckAccount();
			}
			catch (InvalidCredentialsException) {
				Log.Warning("Failed to log in: invalid cookie");
				_client.Cookies.Remove("osnova-remember");
				throw;
			}

			if (!result.ContainsKey("data"))
				throw new InvalidResponseException("Сервер вернул некорректный ответ.", -1);

			return result["data"] as JObject;
		}

		public async Task<JObject> LoginWithMail(string username, string password) {
			Log.Debug($"Logging in w/ username = {username}, password len = {password.Length}");
			var loginResponse = await _client.Request("https://dtf.ru/auth/simple/login")
				.PostUrlEncodedAsync(new Dictionary<string, string> {
					{"values[login]", username},
					{"values[password]", password},
					{"mode", "raw"}
				})
				.ReceiveString();
			var loginJson = ParseAndCheckJson(loginResponse);
			Log.Verbose($"Logged in: {loginJson}");

			var result = await CheckAccount();

			if (!result.ContainsKey("data"))
				throw new InvalidResponseException("Сервер вернул некорректный ответ.", -1);

			return result["data"] as JObject;
		}

		public async Task<JObject> CreatePost() {
			var writingResponse = await _client.Request("https://dtf.ru/writing?to=u&mode=ajax")
				.GetAsync()
				.ReceiveString();
			var writingJson = ParseAndCheckJson(writingResponse);
			Log.Debug($"Writing sent (post created): {writingJson}");
			if (writingJson.ContainsKey("module.auth")) return writingJson["module.auth"] as JObject;

			Log.Error("module.auth is not found in response jsons");
			throw new InvalidResponseException("Сервер вернул некорректный ответ.", -2);
		}

		public async Task<JObject> UploadFile(string path, string mimetype) {
			Log.Verbose("Uploading new file...");
			var uploadResponse = await _client.Request("https://dtf.ru/andropov/upload")
				.PostMultipartAsync(mp => {
					mp.AddFile("file_0", path, mimetype);
					mp.AddString("render", "false");
				})
				.ReceiveString();
			var uploadJson = ParseAndCheckJson(uploadResponse);
			Log.Debug($"Done: {uploadJson}");
			return uploadJson;
		}

		public async Task<JObject> SaveDraft(string title, int userId, bool watermark,
			IEnumerable<UploadingFileInfo> uploadedFiles) {
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
			foreach (var imageInfo in uploadedFiles) {
				if (!imageInfo.Success) continue;
				var image = imageInfo.ResultJson;
				initialData[$"entry[entry][blocks][{i}][type]"] = "media";
				initialData[$"entry[entry][blocks][{i}][cover]"] = "false";
				initialData[$"entry[entry][blocks][{i}][data][items][0][image][type]"] = image["type"].ToString();
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
				initialData[$"entry[entry][blocks][{i}][data][items][0][title]"] = imageInfo.Title ?? "";
				initialData[$"entry[entry][blocks][{i}][data][items][0][author]"] = "";
				initialData[$"entry[entry][blocks][{i}][data][with_border]"] = "false";
				initialData[$"entry[entry][blocks][{i}][data][with_background]"] = "false";
				++i;
			}

			if (watermark) {
				initialData[$"entry[entry][blocks][{i}][type]"] = "text";
				initialData[$"entry[entry][blocks][{i}][cover]"] = "false";
				initialData[$"entry[entry][blocks][{i}][data][text]"] =
					"<p>Пост сделан через <a href=\"https://github.com/saber-nyan/ImagePoster4DTF/releases?ref=dtf.ru\"" +
					" target=\"_blank\">ImagePoster4DTF</a> при поддержке <a href=\"https://dtf.ru/u/69160-saber-nyan\"" +
					" target=\"_blank\">saber-nyan</a> и <a href=\"https://dtf.ru/u/132253-knightmare\" target=\"_blank\">Knightmare</a>.</p>";
				initialData[$"entry[entry][blocks][{i}][data][format]"] = "html";
				initialData[$"entry[entry][blocks][{i}][data][text_truncated]"] = "<<<same>>>";
				++i;
				initialData[$"entry[entry][blocks][{i}][type]"] = "text";
				initialData[$"entry[entry][blocks][{i}][cover]"] = "false";
				initialData[$"entry[entry][blocks][{i}][data][text]"] =
					"<p>Банда хейтеров очобы создана <a href=\"https://dtf.ru/u/203649-danil-sparkov\" target=\"_blank\">Данилом Спарковым</a>.</p>";
				initialData[$"entry[entry][blocks][{i}][data][format]"] = "html";
				initialData[$"entry[entry][blocks][{i}][data][text_truncated]"] = "<<<same>>>";
				++i;
				initialData[$"entry[entry][blocks][{i}][type]"] = "text";
				initialData[$"entry[entry][blocks][{i}][cover]"] = "false";
				initialData[$"entry[entry][blocks][{i}][data][text]"] =
					"<p><a href=\"https://dtf.ru/tag/thisPostWasMadeByOchobaHatersGang\">#thisPostWasMadeByOchobaHatersGang</a></p>";
				initialData[$"entry[entry][blocks][{i}][data][format]"] = "html";
				initialData[$"entry[entry][blocks][{i}][data][text_truncated]"] = "<<<same>>>";
			}

			var saveResponse = await _client.Request("https://dtf.ru/writing/save")
				.PostUrlEncodedAsync(initialData)
				.ReceiveString();
			var saveJson = ParseAndCheckJson(saveResponse);
			Console.WriteLine($"Saved draft: {saveJson}");
			return saveJson;
		}
	}
}
