using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Serilog;

namespace ImagePoster4DTF {
	public class MainWindow : Window {
		private static readonly DtfClient DtfClient = new DtfClient();

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
		}

		protected override void OnClosed(EventArgs e) {
			Log.Information("Exiting now...");
			Log.CloseAndFlush();
			base.OnClosed(e);
		}
	}
}
