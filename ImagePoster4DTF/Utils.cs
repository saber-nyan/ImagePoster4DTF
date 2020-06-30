using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Serilog;

namespace ImagePoster4DTF {
	public static class Utils {
		// https://stackoverflow.com/a/1396143/10018051
		public static IEnumerable<List<T>> Partition<T>(this IList<T> source, int size) {
			for (var i = 0; i < Math.Ceiling(source.Count / (double) size); i++)
				yield return new List<T>(source.Skip(size * i).Take(size));
		}

		// https://stackoverflow.com/a/38604462/10018051
		public static void OpenBrowser(string url) {
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				Process.Start(new ProcessStartInfo(url) {UseShellExecute = true}); // Works ok on windows
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				Process.Start("xdg-open", url); // Works ok on linux
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				Process.Start("open", url); // Not tested
			else
				Log.Error($"Cannot open browser: unsupported platform {RuntimeInformation.OSDescription}!");
		}

		/// <summary>
		///     Плюрализует (склоняет в зависимости от числительного) русские слова.
		/// </summary>
		/// <param name="rawNumber">числительное</param>
		/// <param name="titles">
		///     массив из трех форм слова: (1, 2, 20);
		///     например (Аудиозапись, Аудиозаписи, Аудиозаписей)
		/// </param>
		/// <returns>Плюрализованное слово</returns>
		public static string PluralizeRussian(long rawNumber, List<string> titles) {
			var number = Math.Abs(rawNumber);
			var cases = new[] {2, 0, 1, 1, 1, 2};
			return titles[
				number % 100 > 4 && number % 100 < 20
					? 2
					: cases[number % 10 < 5 ? number % 10 : 5]
			];
		}
	}
}
