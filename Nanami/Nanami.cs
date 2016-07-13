using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace Nanami {
	[ApiVersion(1, 23)]
	[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Local")]
	public class Nanami : TerrariaPlugin {
		public const string NanamiPlayerData = "nanami";

		public override string Name => "Nanami";
		public override string Author => "MistZZT";
		public override string Description => "A TShock-based plugin which collect statistics of PvP players.";
		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		internal static Configuration Config;
		internal static List<PlayerData> PlayerDatas = new List<PlayerData>();
		internal static bool PluginEnabled { get; private set; } = true;

		public Nanami(Main game) : base(game) { }

		public override void Initialize() {
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetGetData.Register(this, OnGetData, 1000);
			ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
			ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);

			GetDataHandlers.TogglePvp += OnPvpToggle;
		}

		protected override void Dispose(bool disposing) {
			if(disposing) {
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
				ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
				ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
			}
			base.Dispose(disposing);
		}

		private void OnInitialize(EventArgs args) {
			Config = Configuration.Read(Configuration.FilePath);
			Config.Write(Configuration.FilePath);

			Commands.ChatCommands.Add(new Command("nanami.pvp.show", Show, "pvp", "战绩"));
			Commands.ChatCommands.Add(new Command("nanami.pvp.switch", Switch, "pvpm", "统计"));

			GeneralHooks.ReloadEvent += e => {
				Config = Configuration.Read(Configuration.FilePath);
				Config.Write(Configuration.FilePath);
				e.Player.SendSuccessMessage("已重新载入 Nanami 配置.");
			};
		}

		private void OnGreetPlayer(GreetPlayerEventArgs args) {
			var player = TShock.Players[args.Who];
			if(player == null)
				return;

			var data = new PlayerData(player.Index);
			player.SetData(NanamiPlayerData, data);
			PlayerDatas.Add(data);
		}

		private void OnLeave(LeaveEventArgs args)
			=> PlayerDatas.RemoveAll(data => data.PlayerIndex == args.Who);

		private void OnGetData(GetDataEventArgs args) {
			if(!PluginEnabled)
				return;

			var type = args.MsgID;

			var player = TShock.Players[args.Msg.whoAmI];
			if(player == null || !player.ConnectionAlive) {
				return;
			}

			using(var data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length - 1)) {
				args.Handled = Handlers.HandleGetData(type, player, data);
			}
		}

		private DateTime _lastCheck = DateTime.UtcNow;

		private void OnUpdate(EventArgs args) {
			if(!PluginEnabled)
				return;

			if(!Config.AutoBroadcastBestKiller)
				return;

			if(!((DateTime.UtcNow - _lastCheck).TotalSeconds >= Config.AutoBroadcastSeconds))
				return;

			if(Main.player.Where(p => p != null && p.active).All(p => !p.hostile))
				return;

			var max = from d in PlayerDatas
					  where Main.player[d.PlayerIndex].hostile
					  orderby d.MaxSuccessiveKills
					  select new { d.MaxSuccessiveKills, d.PlayerIndex };

			max = max.Reverse();

			var sb = new StringBuilder("[PvP战绩] 连续击杀排行: ");
			for(var i = 0; i < 3; ++i) {
				if(max.Count() <= i)
					break;

				sb.Append($"{$"第{i + 1}名",3}{TShock.Players[max.ElementAt(i).PlayerIndex].Name,8}/{max.ElementAt(i).MaxSuccessiveKills} | ");
			}
			var sbText = sb.ToString();

			TShock.Players.Where(p => p != null && p.Active && p.RealPlayer && p.TPlayer.hostile).ForEach(p => {
				p.SendMessage(sbText, Color.Orange);
			});

			_lastCheck = DateTime.UtcNow;
		}

		private void OnPvpToggle(object sender, GetDataHandlers.TogglePvpEventArgs args) {
			if(!args.Pvp)
				return;

			if(!PluginEnabled)
				return;

			TShock.Players[args.PlayerId].SendInfoMessage("你可以通过 {0} 查看你的战绩.", TShock.Utils.ColorTag("/pvp", Color.LightSkyBlue));
		}

		private void Show(CommandArgs args) {
			if(!PluginEnabled) {
				args.Player.SendErrorMessage("PvP战绩记录功能未启用.");
				return;
			}

			if(!args.Player.RealPlayer) {
				args.Player.SendErrorMessage("只有玩家才能使用战绩.");
				return;
			}

			var dt = PlayerData.GetData(args.Player.Index);
			args.Player.SendInfoMessage($"{"---- PvP战绩 ----",38}");
			args.Player.SendInfoMessage($"{"",11}* | 消灭 {dt.Kills,8} | {"连续消灭数目",6} {dt.SuccessiveKills,8} |");
			args.Player.SendInfoMessage($"{"",11}* | 伤害 {dt.Hurts,8} | {"总承受伤害量",6} {dt.Endurance,8} |");
			args.Player.SendInfoMessage($"{"",11}* | 死亡 {dt.Deaths,8} | {"最大连续消灭",6} {dt.MaxSuccessiveKills,8} |");
		}

		private void Switch(CommandArgs args) {
			PluginEnabled = !PluginEnabled;
			args.Player.SendInfoMessage("{0}PvP战绩统计功能.", PluginEnabled ? "开启" : "关闭");
			if(PluginEnabled)
				TSPlayer.All.SendInfoMessage("PvP战绩记录已开启! 使用 {0} 查看.", TShock.Utils.ColorTag("/pvp", Color.LightSkyBlue));
		}
	}
}
