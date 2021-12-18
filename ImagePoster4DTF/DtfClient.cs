using System;
using System.Collections.Generic;
using System.Linq;
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
		private readonly FlurlClient _client = new FlurlClient();
		private readonly CookieJar _jar = new CookieJar();


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
				.WithHeader("x-js-version", "01aba50c")
				.WithHeader("x-this-is-csrf", "THIS IS SPARTA!");
			_jar
				.AddOrReplace("pushVisitsCount", "1", "https://dtf.ru")
				.AddOrReplace("adblock-state", "1", "https://dtf.ru")
				.AddOrReplace("audio_player_volume", "0.75", "https://dtf.ru")
				.AddOrReplace("is_news_widget_closed", "false", "https://dtf.ru");
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

			var code = (int)codeRaw;
			if (code == 200) return parsedJson; //! Successful path

			Log.Error($"Server returned code {code}, aborting");

			if (parsedJson.ContainsKey("rm")) { // response message
				var reason = (string)parsedJson["rm"];
				Log.Error($"Reason: {reason}");
				throw new InvalidResponseException(reason, code);
			}

			Log.Error($"Unknown reason... Full JSON: {parsedJson}");
			throw new InvalidResponseException("Сервер вернул некорректный ответ.", code);
		}

		private async Task<JObject> CheckAccount() {
			var checkResponse = await _client.Request("https://dtf.ru/auth/check?mode=raw")
				.WithCookies(_jar)
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
			return _jar.FirstOrDefault(c => c.Name == "osnova-remember")?.Value;
		}

		public async Task HitRandomPost() {
			var success = false;
			while (!success) {
				var randomId = new Random().Next(100000, 165971);
				Log.Debug($"Fetching {randomId}...");
				try {
					await _client.Request($"https://dtf.ru/hit/{randomId}")
						.WithCookies(_jar)
						.PostUrlEncodedAsync(new Dictionary<string, string> {
							{ "mode", "raw" }
						})
						.ReceiveString();
					success = true;
				}
				catch (Exception e) {
					Log.Warning(e, $"Failed to hit post {randomId}, retrying: ");
				}

				await Task.Delay(334);
			}
		}

		public async Task<JObject> LoginWithCookie(string cookie) {
			Log.Debug($"Logging in w/ cookie len = {cookie.Length}");
			_jar.AddOrReplace("osnova-remember", cookie, "https://dtf.ru");
			JObject result;
			try {
				result = await CheckAccount();
			}
			catch (InvalidCredentialsException) {
				Log.Warning("Failed to log in: invalid cookie");
				_jar.Remove(flurlCookie => flurlCookie.Name == "osnova-remember");
				throw;
			}

			if (!result.ContainsKey("data"))
				throw new InvalidResponseException("Сервер вернул некорректный ответ.", -1);

			return result["data"] as JObject;
		}

		public async Task<JObject> LoginWithMail(string username, string password) {
			Log.Debug($"Logging in w/ username = {username}, password len = {password.Length}");
			var loginResponse = await _client.Request("https://dtf.ru/auth/simple/login")
				.WithCookies(_jar)
				.PostUrlEncodedAsync(new Dictionary<string, string> {
					{ "values[login]", username },
					{ "values[password]", password },
					{ "mode", "raw" }
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
				.WithCookies(_jar)
				.GetAsync()
				.ReceiveString();
			var writingJson = ParseAndCheckJson(writingResponse);
			// Log.Debug($"Writing sent (post created): {writingJson}");
			if (writingJson.ContainsKey("module.auth")) return writingJson["module.auth"] as JObject;

			Log.Error("module.auth is not found in response jsons");
			throw new InvalidResponseException("Сервер вернул некорректный ответ.", -2);
		}

		public async Task<JObject> UploadFile(string path, string mimetype) {
			Log.Verbose("Uploading new file...");
			var uploadResponse = await _client.Request("https://dtf.ru/andropov/upload")
				.WithCookies(_jar)
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
			var basicData = new Dictionary<string, string> {
				{ "autosaving", "true" },
				{ "mode", "raw" },
				{ "additionalData[editorType]", "web full" },
				{ "additionalData[entryPoint]", "Header Create Button" }
			};

			var entryData = new Dictionary<string, object> {
				{ "id", 0 },
				{ "user_id", userId },
				{ "type", 1 },
				{ "title", title },
				{ "url", "" },
				{ "date", 0 },
				{ "date_str", "" },
				{ "modification_date", 0 },
				{ "modification_date_str", "" },
				{ "is_published", false },
				{ "subsite_id", userId },
				{ "subsite_name", "" },
				{ "removed", false },
				{ "custom_style", "" },
				{ "path", "" },
				{ "forced_to_mainpage", false },
				{ "is_advertisement", false },
				{ "is_enabled_instant_articles", false },
				{ "is_enabled_amp", true },
				{ "is_approved_for_public_rss", false },
				{ "is_disabled_likes", false },
				{ "is_disabled_comments", false },
				{ "is_disabled_best_comments", false },
				{ "is_disabled_ad", false },
				{ "is_wide", false },
				{ "is_still_updating", false },
				{ "withheld", false },
				{ "locked_by_admin", false },
				{ "is_show_thanks", false },
				{ "is_clean_cover", 0 },
				{ "is_editorial", false },
				{ "is_disabled_apps", false },
				{ "is_special", false },
				{ "is_filled_by_editors", false },
				{ "is_holdonmain", false },
				{ "is_holdonflash", 0 },
				{ "external_access_link", "" },
				{ "attaches", "" },
				{ "announcement_links", "" },
				// Content
				{
					"entry", new Dictionary<string, object> {
						{ "blocks", new List<object>() }
					}
				}
			};

			foreach (var imageInfo in uploadedFiles) {
				if (!imageInfo.Success) continue;
				var image = imageInfo.ResultJson;
				((List<object>)((Dictionary<string, object>)entryData["entry"])["blocks"])
					.Add(new Dictionary<string, object> {
						{ "type", "media" },
						{ "cover", false },
						{ "hidden", false },
						{ "anchor", null }, {
							"data", new Dictionary<string, object> {
								{ "with_border", false },
								{ "with_background", false },
								// BRUH
								{
									"items", new List<object> {
										new Dictionary<string, object> {
											{ "title", imageInfo.Title ?? "" },
											{ "author", "" }, {
												"image", new Dictionary<string, object> {
													{ "type", "image" },
													{ "render", (string)image["render"] }, {
														"data", new Dictionary<string, object> {
															{ "uuid", (string)image["data"]?["uuid"] },
															{ "width", (int)image["data"]?["width"] },
															{ "height", (int)image["data"]?["height"] },
															{ "size", (long)image["data"]?["size"] },
															{ "type", image["type"].ToString() },
															{ "color", (string)image["data"]?["color"] },
															{ "hash", "" },
															{ "external_service", new List<object>() }
														}
													}
												}
											}
										}
									}
								}
							}
						}
					});
			}

			if (watermark) {
				((List<object>)((Dictionary<string, object>)entryData["entry"])["blocks"])
					.AddRange(new List<object> {
						new Dictionary<string, object> {
							{ "type", "text" },
							{ "cover", false },
							{ "hidden", false },
							{ "anchor", null }, {
								"data", new Dictionary<string, string> {
									{
										"text",
										"<p>Пост сделан через <a href=\"https://github.com/saber-nyan/ImagePoster4DTF/releases?ref=dtf.ru\"" +
										" target=\"_blank\">ImagePoster4DTF</a> при поддержке <a href=\"https://dtf.ru/u/69160-saber-nyan\"" +
										" target=\"_blank\">saber-nyan</a> и <a href=\"https://dtf.ru/u/335947\" target=\"_blank\">Deku</a>.</p>"
									},
									{ "format", "html" },
									{ "text_truncated", "<<<same>>>" }
								}
							}
						},
						new Dictionary<string, object> {
							{ "type", "text" },
							{ "cover", false },
							{ "hidden", false },
							{ "anchor", null }, {
								"data", new Dictionary<string, string> {
									{
										"text",
										"<p>Банда хейтеров очобы создана <a href=\"https://dtf.ru/u/203649\" target=\"_blank\">Данилом Спарковым</a>.</p>"
									},
									{ "format", "html" },
									{ "text_truncated", "<<<same>>>" }
								}
							}
						},
						new Dictionary<string, object> {
							{ "type", "text" },
							{ "cover", false },
							{ "hidden", false },
							{ "anchor", null }, {
								"data", new Dictionary<string, string> {
									{
										"text",
										"<p><a href=\"https://dtf.ru/tag/thisPostWasMadeByOchobaHatersGang\">#thisPostWasMadeByOchobaHatersGang</a></p>"
									},
									{ "format", "html" },
									{ "text_truncated", "<<<same>>>" }
								}
							}
						}
					});
			}

			var serializedEntry = JsonConvert.SerializeObject(entryData);
			basicData["entry"] = serializedEntry;

			var saveResponse = await _client.Request("https://dtf.ru/writing/save")
				.WithCookies(_jar)
				.PostUrlEncodedAsync(basicData)
				.ReceiveString();
			var saveJson = ParseAndCheckJson(saveResponse);
			Console.WriteLine($"Saved draft: {saveJson}");
			return saveJson;
		}
	}
}
