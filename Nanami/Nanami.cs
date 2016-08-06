using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace Nanami
{
	[ApiVersion(1, 23)]
	[SuppressMessage("ReSharper", "MemberCanBeMadeStatic.Local")]
	public class Nanami : TerrariaPlugin
	{
		public const string NanamiPvpData = "nanami-pvp";

		public override string Name => "Nanami";
		public override string Author => "MistZZT";
		public override string Description => "A TShock-based plugin which collect statistics of players.";
		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		internal static Configuration Config;
		private Timer _updateTextTimer;

		public Nanami(Main game) : base(game) { }

		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
			ServerApi.Hooks.NetGetData.Register(this, OnGetData, 1000);
			ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
			ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);

			GetDataHandlers.TogglePvp += OnPvpToggle;
			GeneralHooks.ReloadEvent += OnReload;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
				ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
				ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);

				GetDataHandlers.TogglePvp -= OnPvpToggle;
				GeneralHooks.ReloadEvent -= OnReload;
			}
			base.Dispose(disposing);
		}

		private void OnInitialize(EventArgs args)
		{
			Config = Configuration.Read(Configuration.FilePath);
			Config.Write(Configuration.FilePath);

			Commands.ChatCommands.Add(new Command("nanami.pvp.show", Show, "pvp", "战绩"));
		}

		private void OnPostInitialize(EventArgs args)
		{
			_updateTextTimer = new Timer(1000)
			{
				AutoReset = true,
				Enabled = true
			};
			_updateTextTimer.Elapsed += OnTimerUpdate;
		}

		private int _timerCount;
		private void OnTimerUpdate(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			if (!Config.AutoBroadcastBestKiller)
			{
				return;
			}

			if (++_timerCount < Config.AutoBroadcastSeconds)
			{
				return;
			}

			if (Main.player.Where(p => p != null && p.active).All(p => !p.hostile))
			{
				return;
			}

			var max =
				from player in TShock.Players
				where player != null && player.Active && player.RealPlayer && player.TPlayer.hostile
				let data = PlayerPvpData.GetData(player.Index)
				orderby data.SuccessiveKills descending
				select data;

			var sb = new StringBuilder("[PvP战绩] 连续击杀排行: ");
			for (var i = 0; i < 3; ++i)
			{
				if (max.Count() <= i)
					break;

				sb.Append($"{$"第{i + 1}名",3}{TShock.Players[max.ElementAt(i).PlayerIndex].Name,8}/{max.ElementAt(i).SuccessiveKills} | ");
			}
			var sbText = sb.ToString();

			TShock.Players.Where(p => p != null && p.Active && p.RealPlayer && p.TPlayer.hostile).ForEach(p =>
			{
				p.SendMessage(sbText, Color.Orange);
			});

			_timerCount = 0;
		}

		private void OnGreetPlayer(GreetPlayerEventArgs args)
		{
			var player = TShock.Players[args.Who];
			if (player == null)
				return;

			var data = new PlayerPvpData(player.Index);
			player.SetData(NanamiPvpData, data);
		}

		private void OnLeave(LeaveEventArgs args)
			=> TShock.Players[args.Who]?.RemoveData(NanamiPvpData);

		private void OnGetData(GetDataEventArgs args)
		{
			var type = args.MsgID;

			var player = TShock.Players[args.Msg.whoAmI];
			if (player == null || !player.ConnectionAlive)
			{
				return;
			}

			using (var data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length - 1))
			{
				args.Handled = Handlers.HandleGetData(type, player, data);
			}
		}

		private void OnUpdate(EventArgs args)
		{
		}

		private void OnPvpToggle(object sender, GetDataHandlers.TogglePvpEventArgs args)
		{
			if (!args.Pvp)
			{
				return;
			}

			TShock.Players[args.PlayerId]
				.SendInfoMessage("你可以通过 {0} 查看你的战绩.", TShock.Utils.ColorTag("/pvp", Color.LightSkyBlue));
		}

		private void Show(CommandArgs args)
		{
			if (!args.Player.RealPlayer)
			{
				args.Player.SendErrorMessage("只有玩家才能使用战绩.");
				return;
			}

			var dt = PlayerPvpData.GetData(args.Player.Index);
			args.Player.SendInfoMessage($"{"---- PvP战绩 ----",38}");
			args.Player.SendInfoMessage($"{"",11}* | 消灭 {dt.Kills,8} | {"连续消灭数目",6} {dt.SuccessiveKills,8} |");
			args.Player.SendInfoMessage($"{"",11}* | 伤害 {dt.Hurts,8} | {"总承受伤害量",6} {dt.Endurance,8} |");
			args.Player.SendInfoMessage($"{"",11}* | 死亡 {dt.Deaths,8} | {"最大连续消灭",6} {dt.MaxSuccessiveKills,8} |");
		}

		private static void OnReload(ReloadEventArgs e)
		{
			Config = Configuration.Read(Configuration.FilePath);
			Config.Write(Configuration.FilePath);
			e.Player.SendSuccessMessage("已重新载入 Nanami 配置.");
		}
	}
}
