using Newtonsoft.Json.Linq;

namespace ImagePoster4DTF {
	public class UploadingFileInfo {
		public string Path;
		public JObject resultJson;
		public string Title;

		public UploadingFileInfo(string path, string title) {
			Path = path;
			Title = title;
		}
	}
}
