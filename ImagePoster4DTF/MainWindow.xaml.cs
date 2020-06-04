#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using IniParser;
using IniParser.Exceptions;
using IniParser.Model;

namespace ImagePoster4DTF {
	/// <summary>
	///     Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow {
		private static readonly DtfClient DtfClient = new DtfClient();
		private static readonly FileIniDataParser IniParser = new FileIniDataParser();
		private IniData? _settings;

		public MainWindow() {
			InitializeComponent();
		}

		private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e) {
			await ReadSettings();

			if (_settings != null && _settings.Sections.ContainsSection("Cookies") &&
			    _settings.Global.ContainsKey("username")) {
				var username = _settings.Global["username"];
				DtfClient.LoadCookies(_settings.Sections["Cookies"].GetEnumerator());
				LoggedAs.Content = $"Вы вошли как @{username}";
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
					_settings = IniParser.ReadFile("dtf_settings.ini", Encoding.UTF8);
					Console.WriteLine("...successful");
				}
				catch (FileNotFoundException) {
					Console.WriteLine("Error: file not found!");
				}
				catch (ParsingException) {
					Console.WriteLine("Error: file is invalid! Deleting.");
					try {
						File.Delete("dtf_settings.ini");
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
					IniParser.WriteFile("dtf_settings.ini", _settings, Encoding.UTF8);
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
				var profile = await DtfClient.Login(email, password);
				var username = (string) profile["name"]!;
				LoggedAs.Content = $"Вы вошли как @{username}";

				_settings!.Global["username"] = username;
				_settings.Sections.AddSection("Cookies");
				foreach (var (key, value) in DtfClient.SaveCookies())
					_settings.Sections["Cookies"].AddKey(key, value.Value);

				await WriteSettings();
				LoginOverlay.Visibility = Visibility.Collapsed;
			}
			catch (ApplicationException e) {
				Console.WriteLine($"Failed to auth: {e}");
				MessageBox.Show(this, "Неправильная почта или пароль.", "Ошибка", MessageBoxButton.OK,
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
	}
}
