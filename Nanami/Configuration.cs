using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using TShockAPI;

namespace Nanami
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Configuration
	{
		public static readonly string FilePath = Path.Combine(TShock.SavePath, "nanami.json");

		[JsonProperty("PvP玩家重生时间")]
		public int RespawnPvPSeconds = 5;

		[JsonProperty("连续击杀提示颜色")]
		public byte[][] Colors = {
			new byte[] {255, 0, 255},
			new byte[] {255, 0, 0},
			new byte[] {0, 255, 255},
			new byte[] {108, 166, 205},
			new byte[] {159, 182, 205},
			new byte[] {219, 112, 147}
		};

		public Color[] RealColors { get; private set; }

		[JsonProperty("连续击杀提示文本")]
		public string[] KillsText = {
			"双杀!",
			"连续消灭三人!",
			"连续消灭四人! 吼啊!",
			"成功取得五人斩!",
			"连续歼灭六人! 来人阻止他!",
			"连续杀了七个! 强啊"
		};

		[JsonProperty("提示最少连续击杀")]
		public int MinKillTime = 2;

		[JsonProperty("自动播报最强玩家")]
		public bool AutoBroadcastBestKiller = true;

		[JsonProperty("自动播报时间间隔")]
		public int AutoBroadcastSeconds = 30;

		public static Configuration Read(string path)
		{
			if (!File.Exists(path))
				return new Configuration();
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (var sr = new StreamReader(fs))
				{
					var cf = JsonConvert.DeserializeObject<Configuration>(sr.ReadToEnd());
					cf.RealColors = cf.Colors.Select(c => new Color(c[0], c[1], c[2])).ToArray(); // 加载颜色
					return cf;
				}
			}
		}

		public void Write(string path)
		{
			using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
			{
				var str = JsonConvert.SerializeObject(this, Formatting.Indented);
				using (var sw = new StreamWriter(fs))
				{
					sw.Write(str);
				}
			}
		}
	}
}
