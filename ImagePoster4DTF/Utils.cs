using System;
using System.Collections.Generic;
using System.Linq;

namespace ImagePoster4DTF {
	public static class Utils {
		// https://stackoverflow.com/a/1396143/10018051
		public static IEnumerable<List<T>> Partition<T>(this IList<T> source, int size) {
			for (var i = 0; i < Math.Ceiling(source.Count / (double) size); i++)
				yield return new List<T>(source.Skip(size * i).Take(size));
		}
	}
}
