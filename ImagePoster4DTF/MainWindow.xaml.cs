using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Flurl.Http;
using MessageBox.Avalonia;
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;
using Serilog;

namespace ImagePoster4DTF {
	public class MainWindow : Window {
		private static readonly DtfClient DtfClient = new DtfClient();
		private TextBox _loginCookie;
		private TextBox _loginEmail;
		private Grid _loginLayout;
		private Panel _loginOverlay;
		private TextBox _loginPassword;

		public MainWindow() {
			InitializeComponent();
#if DEBUG
			this.AttachDevTools();
			Log.Debug("DevTools attached");
#endif
			Log.Information("MainWindow initialized");
		}

		private void InitializeComponent() {
			AvaloniaXamlLoader.Load(this);
			_loginEmail = this.Find<TextBox>("LoginEmail");
			_loginPassword = this.Find<TextBox>("LoginPassword");
			_loginCookie = this.Find<TextBox>("LoginCookie");
			_loginLayout = this.Find<Grid>("LoginLayout");
			_loginOverlay = this.Find<Panel>("LoginOverlay");
		}

		protected override void OnClosed(EventArgs e) {
			Log.Information("Exiting now...");
			Log.CloseAndFlush();
			base.OnClosed(e);
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
				// TODO
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
		// ReSharper restore UnusedMember.Local
		// ReSharper restore UnusedParameter.Local
	}
}
