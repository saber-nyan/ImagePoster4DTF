using System;

namespace ImagePoster4DTF {
	/// <summary>
	///     Interaction logic for App.xaml
	/// </summary>
	public partial class App {
		private App() {
			try
			{
				ConsoleManager.Show();
			}
			catch (Exception) { }
		}
	}
}
