#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using IniParser;
using IniParser.Exceptions;
using IniParser.Model;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace ImagePoster4DTF {
	/// <summary>
	///     Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow {
		private const string SettingsFilePath = "dtf_settings.ini";

		private const string
			FilesSeparator = ", "; // TODO: Если в имени файла будут запятая и пробел, случатся плохие вещи

		private static DtfClient _dtfClient = new DtfClient();
		private static readonly FileIniDataParser IniParser = new FileIniDataParser();

		// На самом деле true, но при инициализации ToggleMode() вызывается один раз, что меняет значение этого флажка
		// ReSharper disable once RedundantDefaultMemberInitializer
		private bool _directoryMode = false;

		private IniData? _settings;
		private int _userId;

		public MainWindow() {
			InitializeComponent();
		}

		private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e) {
			await ReadSettings();

			if (_settings != null && _settings.Sections.ContainsSection("Cookies") &&
			    _settings.Global.ContainsKey("username") &&
			    _settings.Global.ContainsKey("user_id")) {
				_dtfClient.LoadCookies(_settings.Sections["Cookies"].GetEnumerator());

				var username = _settings.Global["username"];
				LoggedAs.Content = $"Вы вошли как @{username}";

				_userId = Convert.ToInt32(_settings.Global["user_id"]);

				LoginOverlay.Visibility = Visibility.Collapsed;
			}
			else {
				_settings = new IniData();
			}
		}

		private async Task ReadSettings() {
			// Вроде как так могу избежать блокирования потока при чтении файла
			await Task.Run(() => {
				try {
					Console.WriteLine("Reading persistent storage...");
					_settings = IniParser.ReadFile(SettingsFilePath, Encoding.UTF8);
					Console.WriteLine("...successful");
				}
				catch (FileNotFoundException) {
					Console.WriteLine("Error: file not found!");
				}
				catch (ParsingException) {
					Console.WriteLine("Error: file is invalid! Deleting.");
					try {
						File.Delete(SettingsFilePath);
					}
					catch (Exception) {
						// ignored
					}
				}
				catch (Exception e) {
					Console.WriteLine($"Error: unknown {e}");
				}
			});
		}

		private async Task WriteSettings() {
			await Task.Run(() => {
				try {
					Console.WriteLine("Writing persistent storage...");
					IniParser.WriteFile(SettingsFilePath, _settings, Encoding.UTF8);
					Console.WriteLine("...successful");
				}
				catch (UnauthorizedAccessException) {
					Console.WriteLine("Error: access denied");
					MessageBox.Show(this, "Невозможно сохранить настройки: текущий каталог защищен от записи.",
						"Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
				}
				catch (Exception e) {
					Console.WriteLine($"Error: unknown {e}");
					MessageBox.Show(this, $"Невозможно сохранить настройки: неизвестная ошибка.\r\n{e}",
						"Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
				}
			});
		}

		private static async Task DeleteSettings() {
			await Task.Run(() => {
				try {
					File.Delete(SettingsFilePath);
				}
				catch {
					// ignored
				}
			});
		}

		private void ToggleLoginControls(bool enabled) {
			Email.IsEnabled = enabled;
			Password.IsEnabled = enabled;
			DoLogin.IsEnabled = enabled;
		}

		/// <summary>
		///     Такая вот обертка из-за того, что нельзя делать await на void методы,
		///     а Action нельзя вешать на non-void...
		/// </summary>
		private async Task Login() {
			ToggleLoginControls(false);
			Console.WriteLine("Login called!");
			var email = Email.Text.Trim();
			var password = Password.Password;
			if (string.IsNullOrWhiteSpace(email)) {
				MessageBox.Show(this, "Почта не может быть пустой.", "Ошибка", MessageBoxButton.OK,
					MessageBoxImage.Error);

				ToggleLoginControls(true);
				return;
			}

			if (string.IsNullOrEmpty(password)) {
				MessageBox.Show(this, "Пароль не может быть пустым.", "Ошибка", MessageBoxButton.OK,
					MessageBoxImage.Error);
				ToggleLoginControls(true);
				return;
			}

			try {
				var profile = await _dtfClient.Login(email, password);
				var username = (string) profile["name"]!;
				LoggedAs.Content = $"Вы вошли как @{username}";

				_settings!.Global["username"] = username;
				_settings.Sections.AddSection("Cookies");
				foreach (var (key, value) in _dtfClient.SaveCookies())
					_settings.Sections["Cookies"].AddKey(key, value.Value);
				_settings!.Global["user_id"] = (string) profile["id"]!;
				_userId = Convert.ToInt32((string) profile["id"]!);

				await WriteSettings();
				LoginOverlay.Visibility = Visibility.Collapsed;
			}
			catch (ApplicationException e) {
				Console.WriteLine($"Failed to auth: {e}");
				MessageBox.Show(this, e.Message, "Ошибка", MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
			catch (Exception e) {
				Console.WriteLine($"Failed to auth: unknown error {e}");
			}

			ToggleLoginControls(true);
		}

		private async void ButtonDoLogin_OnClick(object sender, RoutedEventArgs ev) {
			await Login();
		}

		private async void Password_OnKeyDown(object sender, KeyEventArgs ev) {
			if (ev.Key == Key.Return || ev.Key == Key.Enter) await Login();
		}

		private async void LogoutButton_OnClick(object sender, RoutedEventArgs ev) {
			_dtfClient = new DtfClient();
			await DeleteSettings();
			LoggedAs.Content = "Вы не вошли";
			LoginOverlay.Visibility = Visibility.Visible;
		}

		private void ToggleMode() {
			try {
				Console.WriteLine($"Was _directoryMode={_directoryMode}");
				_directoryMode = !_directoryMode;

				if (_directoryMode) {
					DirectorySelectField.IsEnabled = true;
					DirectorySelect.IsEnabled = true;
					FilesSelectField.IsEnabled = false;
					FilesSelect.IsEnabled = false;
				}
				else {
					DirectorySelectField.IsEnabled = false;
					DirectorySelect.IsEnabled = false;
					FilesSelectField.IsEnabled = true;
					FilesSelect.IsEnabled = true;
				}
			}
			catch {
				// ignored
			}
		}

		private void ToggleAllControls(bool enabled) {
			DirectoryRadio.IsEnabled = enabled;
			DirectorySelectField.IsEnabled = enabled;
			DirectorySelect.IsEnabled = enabled;
			FilesRadio.IsEnabled = enabled;
			FilesSelectField.IsEnabled = enabled;
			FilesSelect.IsEnabled = enabled;
			DraftTitle.IsEnabled = enabled;
			FireButton.IsEnabled = enabled;
			LogoutButton.IsEnabled = enabled;
		}

		private void DirectoryRadio_OnChecked(object sender, RoutedEventArgs ev) {
			ToggleMode();
		}

		private void FilesRadio_OnChecked(object sender, RoutedEventArgs ev) {
			ToggleMode();
		}

		private void DirectorySelect_OnClick(object sender, RoutedEventArgs ev) {
			Console.WriteLine("Selecting directory...");
			// TODO
			MessageBox.Show(this, "Поскольку WPF -- говнище, здесь нет встроенного диалога выбора директории. " +
			                      "Пожалуйста, укажите путь вручную.", "# TODO", MessageBoxButton.OK,
				MessageBoxImage.Error);
		}

		private void FilesSelect_OnClick(object sender, RoutedEventArgs ev) {
			Console.WriteLine("Selecting files...");
			var fileDialog = new OpenFileDialog {
				Filter =
					"Изображения (*.png;*.jpg;*.jpeg;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.gif;*.webp|Все файлы (*.*)|*.*",
				Multiselect = true,
				Title = "Выбор изображений",
				CheckFileExists = true
			};
			if (fileDialog.ShowDialog() != true) return;
			Console.WriteLine("Selected!");
			FilesSelectField.Text = string.Join(FilesSeparator, fileDialog.FileNames);
		}

		private async void FireButton_OnClick(object sender, RoutedEventArgs ev) {
			try {
				Console.WriteLine("!!! F I R E !!!");
				IEnumerable<string>? filePaths;
				if (_directoryMode)
					try {
						var path = DirectorySelectField.Text;
						// https://stackoverflow.com/a/163220/10018051
						filePaths = await Task.Run(() => Directory
							.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
							.Where(s => s.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
										|| s.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
										|| s.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
										|| s.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
							.ToImmutableList()
						);
					}
					catch (Exception e) {
						MessageBox.Show(this, $"Указан некорректный путь.\r\n{e}", "Ошибка", MessageBoxButton.OK,
							MessageBoxImage.Error);
						return;
					}
				else
					filePaths = FilesSelectField.Text.Split(FilesSeparator);

				ToggleAllControls(false);
				try {
					await _dtfClient.CreatePost();
					var file = await _dtfClient.UploadFiles(filePaths);
					var draft = await _dtfClient.SaveDraft(DraftTitle.Text, _userId, file["result"] as JArray);

					DirectorySelectField.Text = "";
					FilesSelectField.Text = "";
					DraftTitle.Text = "";

					// Открываем браузер с черновиком! https://stackoverflow.com/a/58439029/10018051
					Console.WriteLine($"DATA IS {draft}");
					var psi = new ProcessStartInfo {
						FileName = (string) draft["data"]?["entry"]?["url"]!,
						UseShellExecute = true
					};
					Process.Start(psi);
				}
				catch (Exception e) {
					Console.WriteLine($"Error: {e}");
					MessageBox.Show(this, "Возникла ошибка при отправке черновика." +
										  " Проверьте ваше интернет-соединение и работоспособность dtf.ru," +
										  $" а также указанные пути.\r\n{e}", "Ошибка", MessageBoxButton.OK,
						MessageBoxImage.Error);
				}

				ToggleAllControls(true);
				// Возвращаем в исходное состояние, два раза переключив...
				ToggleMode();
				ToggleMode();

				Console.WriteLine("!!! D O N E !!!");
			} catch (Exception e) {
				MessageBox.Show(this, $"FUCK: {e}", "Ошибка", MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}
	}
}
