using Avalonia;
using Avalonia.Logging.Serilog;
using Serilog;
using Serilog.Events;
using SharpGen.Runtime;

namespace ImagePoster4DTF {
	internal static class Program {
		private const string DefaultDebugTemplate =
			"{Level}: [{Area}] {Message} ({SourceType} #{SourceHash}) {Exception}";

		private const string DefaultFileTemplate =
			"{Timestamp:HH:mm:ss} - [{Area}] {Message} ({SourceType} #{SourceHash}){NewLine}{Exception}";

		// Initialization code. Don't use any Avalonia, third-party APIs or any
		// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
		// yet and stuff might break.
		public static void Main(string[] args) {
			try {
				BuildAvaloniaApp()
					.StartWithClassicDesktopLifetime(args);
			}
			catch (SharpGenException) {
				BuildAvaloniaApp()
					.With(new AvaloniaNativePlatformOptions {UseGpu = false})
					.StartWithClassicDesktopLifetime(args);
			}
		}

		// Avalonia configuration, don't remove; also used by visual designer.
		private static AppBuilder BuildAvaloniaApp() {
			var app = AppBuilder.Configure<App>()
				.LogToDebug()
				.UsePlatformDetect();
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Is(LogEventLevel.Debug)
				.Enrich.FromLogContext()
				.WriteTo.Debug(outputTemplate: DefaultDebugTemplate)
				.WriteTo.File("ImagePoster4DTF_.log",
					rollingInterval: RollingInterval.Hour,
					outputTemplate: DefaultFileTemplate)
				.CreateLogger();
			SerilogLogger.Initialize(Log.Logger); // Дерьмово. Но работает.
			Log.Debug("Logger created");
			return app;
		}
	}
}
