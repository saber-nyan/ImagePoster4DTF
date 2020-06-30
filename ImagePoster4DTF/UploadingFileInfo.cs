using Newtonsoft.Json.Linq;

namespace ImagePoster4DTF {
	public class UploadingFileInfo {
		public readonly string Mimetype;
		public readonly string Path;
		public readonly string Title;
		public JObject ResultJson;
		public bool Success = false;

		public UploadingFileInfo(string path, string title, string mimetype) {
			Path = path;
			Title = title;
			Mimetype = mimetype;
		}

		public override string ToString() {
			return $"{nameof(Path)}: {Path}, {nameof(Title)}: {Title}";
		}
	}
}