using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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

		public Nanami(Main game) : base(game) { }

		public override void Initialize() {
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetGetData.Register(this, OnGetData, 1000);
			ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreetPlayer);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);

			GetDataHandlers.PlayerDamage += OnPlayerDamage;
			GetDataHandlers.PlayerHP += OnPlayerHP;
		}

		protected override void Dispose(bool disposing) {
			if (disposing) {
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
				ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreetPlayer);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
			}
			base.Dispose(disposing);
		}

		private void OnInitialize(EventArgs args) {
			Config = Configuration.Read(Configuration.FilePath);
			Config.Write(Configuration.FilePath);
		}

		private void OnGreetPlayer(GreetPlayerEventArgs args) {
			var player = TShock.Players[args.Who];
			if (player == null)
				return;
			
			var data = new PlayerData(player.Index);
			player.SetData(NanamiPlayerData, data);
			PlayerDatas.Add(data);
		}

		private void OnLeave(LeaveEventArgs args)
			=> PlayerDatas.RemoveAll(data => data.PlayerIndex == args.Who);

		private void OnGetData(GetDataEventArgs args) {
			var type = args.MsgID;

			var player = TShock.Players[args.Msg.whoAmI];
			if (player == null || !player.ConnectionAlive) {
				return;
			}

			using (var data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length - 1)) {
				args.Handled = Handlers.HandleGetData(type, player, data);
			}
		}

		private void OnPlayerDamage(object sender, GetDataHandlers.PlayerDamageEventArgs args) {
			
		}

		private void OnPlayerHP(object sender, GetDataHandlers.PlayerHPEventArgs args) {
			
		}
	}
}
