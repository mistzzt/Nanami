using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace Nanami {
	[JsonObject(MemberSerialization.OptIn)]
	public class Configuration {
		public static readonly string FilePath = Path.Combine(TShock.SavePath, "nanami.json");

		[JsonProperty("PvP玩家重生时间")]
		public int RespawnPvPSeconds = 5;

		[JsonProperty("连续击杀提示颜色")]
		public Color[] Colors = {
			new Color(255, 0, 255),
			new Color(255, 0, 0),
			new Color(255, 255, 255)
		};

		[JsonProperty("连续击杀提示文本")]
		public string[] KillsText = {
			"连续消灭两人!",
			"连续消灭三人!",
			"连续消灭四人! 已经超神!"
		};

		[JsonProperty("提示最少连续击杀")]
		public int MinKillTime = 2;

		[JsonProperty("自动播报最强玩家")]
		public bool AutoBroadcastBestKiller = false;

		[JsonProperty("自动播报时间间隔")]
		public int AutoBroadcastSeconds = 5;

		public static Configuration Read(string path) {
			if (!File.Exists(path))
				return new Configuration();
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				using (var sr = new StreamReader(fs)) {
					var cf = JsonConvert.DeserializeObject<Configuration>(sr.ReadToEnd());
					return cf;
				}
			}
		}

		public void Write(string path) {
			using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write)) {
				var str = JsonConvert.SerializeObject(this, Formatting.Indented);
				using (var sw = new StreamWriter(fs)) {
					sw.Write(str);
				}
			}
		}

		public struct Color {
			public int R;
			public int G;
			public int B;

			public Color(int r, int g, int b) {
				R = r;
				G = g;
				B = b;
			}
		}
	}
}
