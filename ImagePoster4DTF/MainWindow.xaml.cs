using System;
using System.Windows;

namespace ImagePoster4DTF {
	/// <summary>
	///     Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow {
		private static readonly DtfClient DtfClient = new DtfClient();

		public MainWindow() {
			InitializeComponent();
		}

		private async void ButtonLogin_OnClick(object sender, RoutedEventArgs ev) {
			Console.WriteLine("Login called!");
			try {
				await DtfClient.Login("saber-nyan@ya.ru", "CENSORED");
			}
			catch (ApplicationException e) {
				Console.WriteLine($"Failed to auth: {e}");
			}
			catch (Exception e) {
				Console.WriteLine($"Failed to auth: unknown error {e}");
			}
		}

		private async void ButtonCreatePost_OnClick(object sender, RoutedEventArgs ev) {
			Console.WriteLine("Create post called!");
			try {
				await DtfClient.CreatePost();
			}
			catch (Exception e) {
				Console.WriteLine($"Failed to auth: unknown error {e}");
			}
		}
	}
}
