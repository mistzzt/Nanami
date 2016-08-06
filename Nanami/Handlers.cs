using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Streams;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TShockAPI;

namespace Nanami
{
	[SuppressMessage("ReSharper", "InvertIf")] // most stolen from TShockAPI.GetDataHandlers
	internal class Handlers
	{
		private static readonly Dictionary<PacketTypes, GetDataHandlerDelegate>
			GetDataHandlerDelegates = new Dictionary<PacketTypes, GetDataHandlerDelegate>
			{
				{ PacketTypes.PlayerDamage , HandlePlayerDamage },
				{ PacketTypes.PlayerKillMe, HandleKillMe }
			};

		public static bool HandleGetData(PacketTypes type, TSPlayer player, MemoryStream data)
		{
			GetDataHandlerDelegate handler;
			if (GetDataHandlerDelegates.TryGetValue(type, out handler))
			{
				try
				{
					return handler(new GetDataHandlerArgs(player, data));
				}
				catch (Exception ex)
				{
					TShock.Log.Error(ex.ToString());
					return true;
				}
			}
			return false;
		}

		private static bool HandlePlayerDamage(GetDataHandlerArgs args)
		{
			var id = args.Data.ReadInt8();
			args.Data.ReadInt8();
			var dmg = args.Data.ReadInt16();
			args.Data.ReadString();
			var bits = (BitsByte)args.Data.ReadInt8();
			var pvp = bits[0];

			if (id >= Main.maxPlayers || TShock.Players[id] == null)
			{
				return true;
			}

			if (!pvp) // 玩家受到普通伤害
				return false;

			if (dmg > TShock.Config.MaxDamage && !args.Player.HasPermission(Permissions.ignoredamagecap) && id != args.Player.Index)
			{
				if (TShock.Config.KickOnDamageThresholdBroken)
				{
					TShock.Utils.Kick(args.Player, $"玩家攻击数值超过 {TShock.Config.MaxDamage}.");
					return true;
				}
				else
				{
					args.Player.Disable($"玩家攻击数值超过 {TShock.Config.MaxDamage}.", DisableFlags.WriteToLogAndConsole);
				}
				args.Player.SendData(PacketTypes.PlayerHp, "", id);
				args.Player.SendData(PacketTypes.PlayerUpdate, "", id);
				return true;
			}

			if (!TShock.Players[id].TPlayer.hostile && pvp && id != args.Player.Index)
			{
				args.Player.SendData(PacketTypes.PlayerHp, "", id);
				args.Player.SendData(PacketTypes.PlayerUpdate, "", id);
				return true;
			}

			if (TShock.CheckIgnores(args.Player))
			{
				args.Player.SendData(PacketTypes.PlayerHp, "", id);
				args.Player.SendData(PacketTypes.PlayerUpdate, "", id);
				return true;
			}

			if (TShock.CheckRangePermission(args.Player, TShock.Players[id].TileX, TShock.Players[id].TileY, 100))
			{
				args.Player.SendData(PacketTypes.PlayerHp, "", id);
				args.Player.SendData(PacketTypes.PlayerUpdate, "", id);
				return true;
			}

			if ((DateTime.UtcNow - args.Player.LastThreat).TotalMilliseconds < 5000)
			{
				args.Player.SendData(PacketTypes.PlayerHp, "", id);
				args.Player.SendData(PacketTypes.PlayerUpdate, "", id);
				return true;
			}

			// 记录 伤害量
			var data = args.Player.GetData<PlayerData>(Nanami.NanamiPlayerData);
			data.Damage(dmg, id);

			// 记录 承受伤害量
			PlayerData.GetData(id).Hurt(dmg);

			return false;
		}

		private static bool HandleKillMe(GetDataHandlerArgs args)
		{
			var id = args.Data.ReadInt8();
			var direction = (byte)(args.Data.ReadInt8() - 1);
			var dmg = args.Data.ReadInt16();
			var pvp = args.Data.ReadInt8(); // 此处疑似是非pvp
			var text = args.Data.ReadString();

			if (pvp == 0)
			{
				return false;
			}

			if (dmg > 20000) //Abnormal values have the potential to cause infinite loops in the server.
			{
				TShock.Utils.ForceKick(args.Player, "尝试破坏服务器.", true);
				TShock.Log.ConsoleError("死亡数值上限攻击: 数值 {0}", dmg);
				return false;
			}

			if (id >= Main.maxPlayers)
			{
				return true;
			}

			if (text.Length > 500)
			{
				TShock.Utils.Kick(TShock.Players[id], "尝试破坏服务器.", true);
				return true;
			}

			args.Player.RespawnTimer = Nanami.Config.RespawnPvPSeconds;
			var data = args.Player.GetData<PlayerData>(Nanami.NanamiPlayerData);

			// 处理死亡事件
			data.Die(dmg);

			var killer = -1;
			foreach (var dt in Nanami.PlayerDatas)
			{
				if (dt.PlayerIndex == args.Player.Index)
				{
					continue;
				}

				if (text.Contains(TShock.Players[dt.PlayerIndex].Name))
				{
					killer = dt.PlayerIndex;
				}
			}
			if (killer == -1)
			{
				TShock.Log.ConsoleError("[Nanami] {0} 的死亡消息异常: {0} {1}", args.Player.Name, text);
				return false;
			}

			var deathText = $" 被 {TShock.Players[killer].Name} 杀死了!";

			// 处理杀死事件
			var killerData = PlayerData.GetData(killer);
			killerData.Kill(ref deathText);

			Main.player[id].KillMe(dmg, direction, pvp == 1, deathText);
			NetMessage.SendData((int)PacketTypes.PlayerKillMe, -1, id, deathText, id, direction, dmg, pvp);

			return true;
		}
	}
}
