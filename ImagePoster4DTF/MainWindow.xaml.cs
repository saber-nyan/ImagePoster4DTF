﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Flurl.Http;
using IniParser;
using IniParser.Exceptions;
using IniParser.Model;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;
using Serilog;

namespace ImagePoster4DTF {
	public class MainWindow : Window {
		private const string SettingsFilePath = "dtf_settings.ini";
		private static readonly FileIniDataParser IniParser = new FileIniDataParser();

		private static readonly DtfClient DtfClient = new DtfClient();
		private Button _accountButton;

		//! Account info
		private TextBlock _accountText;
		private string _cookie;

		//! Selectors
		// Directory
		private TextBox _directoryField;
		private CheckBox _directoryIsRecursive;

		private Button _directorySelect;
		// Persistent data end

		private bool _directorySelectionMode = true;

		// Files
		private TextBox _filesField;
		private Button _filesSelect;

		// Other
		private CheckBox _isPostWatermark;

		//! Settings
		// Regex
		private CheckBox _isRegexEnabled;

		//! Login window
		private TextBox _loginCookie;
		private TextBox _loginEmail;
		private Grid _loginLayout;
		private Panel _loginOverlay;
		private TextBox _loginPassword;


		private Grid _mainLayout;
		private TextBox _postTitle;
		private bool _regexEnabled;
		private TextBox _regexFrom;
		private string _regexFromPersistence;
		private Grid _regexLayout;
		private TextBox _regexTo;
		private string _regexToPersistence;
		private string[] _selectedFiles;

		private int _userId;

		// Persistent data
		private string _username;
		private bool _watermarkEnabled = true;

		public MainWindow() {
			Opened += OnOpened;
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
			Log.Debug("DevTools attached");
#endif
			Log.Information("MainWindow initialized");
		}

		private async void OnOpened(object sender, EventArgs ev) {
			await ReadSettings();
			_isRegexEnabled.IsChecked = _regexEnabled;
			_regexLayout.IsEnabled = _regexEnabled;

			_regexFrom.Text = _regexFromPersistence;
			_regexTo.Text = _regexToPersistence;

			_isPostWatermark.IsChecked = _watermarkEnabled;

			if (_username == null || _cookie == null || _userId == 0) return;

			_loginOverlay.IsVisible = false;
			_loginLayout.IsEnabled = false;

			try {
				var result = await DtfClient.LoginWithCookie(_cookie);
				_userId = (int) result["id"];
				_username = (string) result["name"];
				_accountText.Text = $"Вы вошли как @{_username}";
			}
			catch (FlurlHttpException e) {
				var data = await e.GetResponseStringAsync();
				Log.Error(e, $"Network/server error! With data: {data}");
				await ShowError("Сбой сети или некорректный ответ сервера. " +
				                $"Проверьте работоспособность DTF.ru в браузере и попробуйте еще раз.\n{e}");
			}
			catch (InvalidResponseException e) {
				Log.Error(e, $"Parsing/client error! With code: {e.Code}");
				_loginOverlay.IsVisible = true;
			}
			catch (InvalidCredentialsException e) {
				Log.Error(e, "Invalid credentials");
				_loginOverlay.IsVisible = true;
			}
			catch (Exception e) {
				Log.Fatal(e, "Unknown exception!");
				await ShowError($"Неизвестная ошибка!\n{e}");
			}

			_loginLayout.IsEnabled = true;
		}

		private void InitializeComponent() {
			AvaloniaXamlLoader.Load(this);

			_mainLayout = this.Find<Grid>("MainLayout");

			//! Login window
			_loginEmail = this.Find<TextBox>("LoginEmail");
			_loginPassword = this.Find<TextBox>("LoginPassword");
			_loginCookie = this.Find<TextBox>("LoginCookie");
			_loginLayout = this.Find<Grid>("LoginLayout");
			_loginOverlay = this.Find<Panel>("LoginOverlay");

			//! Selectors
			// Directory
			_directoryField = this.Find<TextBox>("DirectoryField");
			_directorySelect = this.Find<Button>("DirectorySelect");
			_directoryIsRecursive = this.Find<CheckBox>("DirectoryIsRecursive");
			// Files
			_filesField = this.Find<TextBox>("FilesField");
			_filesSelect = this.Find<Button>("FilesSelect");

			//! Settings
			// Regex
			_isRegexEnabled = this.Find<CheckBox>("IsRegexEnabled");
			_regexLayout = this.Find<Grid>("RegexLayout");
			_regexFrom = this.Find<TextBox>("RegexFrom");
			_regexTo = this.Find<TextBox>("RegexTo");
			// Other
			_isPostWatermark = this.Find<CheckBox>("IsPostWatermark");
			_postTitle = this.Find<TextBox>("PostTitle");

			//! Account info
			_accountText = this.Find<TextBlock>("AccountText");
			_accountButton = this.Find<Button>("AccountButton");
		}

		protected override async void OnClosed(EventArgs e) {
			Log.Information("Exiting now...");
			_regexEnabled = _isRegexEnabled.IsChecked ?? false;
			_regexFromPersistence = _regexFrom.Text;
			_regexToPersistence = _regexTo.Text;
			_watermarkEnabled = _isPostWatermark.IsChecked ?? false;
			await WriteSettings();
			Log.CloseAndFlush();
			base.OnClosed(e);
		}

		private async Task ReadSettings() {
			IniData settings = null;
			try {
				Log.Information("Reading persistent storage...");
				await Task.Run(() => { settings = IniParser.ReadFile(SettingsFilePath, Encoding.UTF8); });
				Log.Information("...successful");
			}
			catch (ParsingException e) when (e.InnerException is FileNotFoundException) {
				Log.Information(e, "Error: file not found");
			}
			catch (ParsingException e) {
				Log.Error(e, "Error: invalid file content, deleting");
				try {
					File.Delete(SettingsFilePath);
				}
				catch {
					// ignored
				}
			}
			catch (Exception e) {
				Log.Error(e, "Error: unknown");
			}

			if (settings == null) return;

			try {
				if (settings.Global.ContainsKey("regexEnabled")
				    && settings.Global.ContainsKey("watermarkEnabled")) {
					_username = settings.Global["username"];
					_cookie = settings.Global["cookie"];
					_regexEnabled = Convert.ToBoolean(settings.Global["regexEnabled"]);
					_regexFromPersistence = settings.Global["regexFrom"];
					_regexToPersistence = settings.Global["regexTo"];
					_watermarkEnabled = Convert.ToBoolean(settings.Global["watermarkEnabled"]);
					_userId = Convert.ToInt32(settings.Global["userId"]);
				}
				else {
					Log.Error("Invalid ini, deleting...");
					try {
						File.Delete(SettingsFilePath);
					}
					catch {
						// ignored
					}
				}
			}
			catch (Exception e) {
				Log.Error(e, "Failed to parse file, deleting...");
				try {
					File.Delete(SettingsFilePath);
				}
				catch {
					// ignored
				}
			}
		}

		private async Task WriteSettings() {
			var settings = new IniData {
				Global = {
					["username"] = _username,
					["cookie"] = _cookie,
					["regexEnabled"] = _regexEnabled.ToString(),
					["regexFrom"] = _regexFromPersistence,
					["regexTo"] = _regexToPersistence,
					["watermarkEnabled"] = _watermarkEnabled.ToString(),
					["userId"] = _userId.ToString()
				}
			};

			try {
				Log.Information("Writing settings...");
				await Task.Run(() => IniParser.WriteFile(SettingsFilePath, settings, Encoding.UTF8));
				Log.Information("...successful");
			}
			catch (UnauthorizedAccessException e) {
				Log.Error(e, "Error: access denied");
				await ShowError("Невозможно сохранить настройки: текущий каталог защищен от записи.");
			}
			catch (Exception e) {
				Log.Error(e, "Error: unknown");
				await ShowError($"Невозможно сохранить настройки: неизвестная ошибка.\n{e}");
			}
		}

		private async Task ShowError(string error) {
			await MessageBoxManager.GetMessageBoxCustomWindow(new MessageBoxCustomParams {
				ContentTitle = "Ошибка",
				ContentMessage = error,
				Icon = MessageBox.Avalonia.Enums.Icon.Error,
				ButtonDefinitions = new[] {new ButtonDefinition {Name = "ОК", Type = ButtonType.Default}},
				CanResize = false
			}).ShowDialog(this);
		}

		private async Task LoginByEmail() {
			var email = _loginEmail.Text;
			var password = _loginPassword.Text;
			Log.Information($"Logging in via email = {email}, password len = {password?.Length}");

			if (string.IsNullOrWhiteSpace(email)) {
				await ShowError("Поле почты не может быть пустым.");
				return;
			}

			if (string.IsNullOrEmpty(password)) {
				await ShowError("Поле пароля не может быть пустым.");
				return;
			}

			_loginLayout.IsEnabled = false;

			try {
				var result = await DtfClient.LoginWithMail(email, password);
				_userId = (int) result["id"];
				_username = (string) result["name"];
				_accountText.Text = $"Вы вошли как @{_username}";
				_cookie = DtfClient.GetCookie();
				await WriteSettings();
				_loginOverlay.IsVisible = false;
			}
			catch (FlurlHttpException e) {
				var data = await e.GetResponseStringAsync();
				Log.Error(e, $"Network/server error! With data: {data}");
				await ShowError("Сбой сети или некорректный ответ сервера. " +
				                $"Проверьте работоспособность DTF.ru в браузере и попробуйте еще раз.\n{e}");
			}
			catch (InvalidResponseException e) {
				Log.Error(e, $"Parsing/client error! With code: {e.Code}");
				await ShowError($"{e.Message}, код {e.Code}.");
			}
			catch (InvalidCredentialsException e) {
				Log.Error(e, "Invalid credentials");
				await ShowError("Неправильный логин или пароль.");
			}
			catch (Exception e) {
				Log.Fatal(e, "Unknown exception!");
				await ShowError($"Неизвестная ошибка!\n{e}");
			}

			_loginLayout.IsEnabled = true;
		}

		private async Task LoginByCookie() {
			var cookie = _loginCookie.Text;
			Log.Information($"Logging in via cookie! Len: {cookie?.Length}");

			if (string.IsNullOrWhiteSpace(cookie)) {
				await ShowError("Поле cookie не может быть пустым.");
				return;
			}

			_loginLayout.IsEnabled = false;

			try {
				var result = await DtfClient.LoginWithCookie(cookie);
				_userId = (int) result["id"];
				_username = (string) result["name"];
				_accountText.Text = $"Вы вошли как @{_username}";
				_cookie = cookie;
				await WriteSettings();
				_loginOverlay.IsVisible = false;
			}
			catch (FlurlHttpException e) {
				var data = await e.GetResponseStringAsync();
				Log.Error(e, $"Network/server error! With data: {data}");
				await ShowError("Сбой сети или некорректный ответ сервера. " +
				                $"Проверьте работоспособность DTF.ru в браузере и попробуйте еще раз.\n{e}");
			}
			catch (InvalidResponseException e) {
				Log.Error(e, $"Parsing/client error! With code: {e.Code}");
				await ShowError($"{e.Message}, код {e.Code}.");
			}
			catch (InvalidCredentialsException e) {
				Log.Error(e, "Invalid credentials");
				await ShowError("Неверная или устаревшая кука.");
			}
			catch (Exception e) {
				Log.Fatal(e, "Unknown exception!");
				await ShowError($"Неизвестная ошибка!\n{e}");
			}

			_loginLayout.IsEnabled = true;
		}

		// ReSharper disable UnusedMember.Local
		// ReSharper disable UnusedParameter.Local
		private void ToggleMode() {
			Log.Debug($"Was _directorySelectionMode={_directorySelectionMode}");
			_filesField.IsEnabled = _directorySelectionMode;
			_filesSelect.IsEnabled = _directorySelectionMode;

			_directorySelectionMode = !_directorySelectionMode;

			_directoryField.IsEnabled = _directorySelectionMode;
			_directorySelect.IsEnabled = _directorySelectionMode;
			_directoryIsRecursive.IsEnabled = _directorySelectionMode;
		}

		private async void LoginByEmailExecute_OnClick(object sender, RoutedEventArgs ev) {
			await LoginByEmail();
		}

		private async void LoginByEmail_OnKeyDown(object sender, KeyEventArgs ev) {
			if (ev.Key == Key.Return || ev.Key == Key.Enter) await LoginByEmail();
		}

		private async void LoginByCookieExecute_OnClick(object sender, RoutedEventArgs ev) {
			await LoginByCookie();
		}

		private async void LoginByCookie_OnKeyDown(object sender, KeyEventArgs ev) {
			if (ev.Key == Key.Return || ev.Key == Key.Enter) await LoginByCookie();
		}

		private void LoginCookieHelp_OnClick(object sender, RoutedEventArgs ev) {
			// TODO
			Utils.OpenBrowser("https://github.com/saber-nyan/ImagePoster4DTF/");
		}

		private void DirectoryRadio_OnChecked(object sender, RoutedEventArgs ev) {
			ToggleMode();
		}

		private void FilesRadio_OnChecked(object sender, RoutedEventArgs ev) {
			ToggleMode();
		}

		private void IsRegexEnabled_OnClick(object sender, RoutedEventArgs ev) {
			_regexLayout.IsEnabled = _isRegexEnabled.IsChecked ?? false;
		}

		private async void DirectorySelect_OnClick(object sender, RoutedEventArgs ev) {
			var dialog = new OpenFolderDialog {Title = "Выберите директорию для загрузки"};
			var result = await dialog.ShowAsync(this);
			if (result == null) return;

			Log.Information($"Selected {result}");
			_directoryField.Text = result;
		}

		private async void FilesSelect_OnClick(object sender, RoutedEventArgs ev) {
			var dialog = new OpenFileDialog {
				Title = "Выберите файлы для загрузки",
				AllowMultiple = true,
				Filters = {
					new FileDialogFilter {
						Name = "Медиафайлы",
						Extensions = {
							"png", "jfif", "pjpeg", "jpeg", "pjp",
							"jpg", "gif", "m4v", "mp4", "webp"
						}
					},
					new FileDialogFilter {
						Name = "Все файлы",
						Extensions = {"*"}
					}
				}
			};
			string[] result = await dialog.ShowAsync(this);
			if (result == null) return;

			var filesCount = result.Length;
			var pluralized = Utils.PluralizeRussian(filesCount, new List<string> {
				"Выбран", "Выбрано", "Выбрано"
			});
			var pluralizedFile = Utils.PluralizeRussian(filesCount, new List<string> {
				"файл", "файла", "файлов"
			});
			_filesField.Text = $"{pluralized} {filesCount} {pluralizedFile}";
			_selectedFiles = result;
		}
		// ReSharper restore UnusedMember.Local
		// ReSharper restore UnusedParameter.Local
	}
}
