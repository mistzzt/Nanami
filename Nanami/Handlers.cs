using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Streams;
using Terraria;
using Terraria.DataStructures;
using Terraria.Localization;
using TShockAPI;

namespace Nanami
{
	internal class Handlers
	{
		private static readonly Dictionary<PacketTypes, GetDataHandlerDelegate>
			GetDataHandlerDelegates = new Dictionary<PacketTypes, GetDataHandlerDelegate>
			{
				{ PacketTypes.PlayerDamage , HandlePlayerDamage },
				{ PacketTypes.PlayerKillMe, HandleKillMe },
				{ PacketTypes.PlayerDeathV2, HandleKillMeV2 },
				{ PacketTypes.PlayerHurtV2, HandleHurtV2 }
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
				args.Player.Disable($"玩家攻击数值超过 {TShock.Config.MaxDamage}.", DisableFlags.WriteToLogAndConsole);
				args.Player.SendData(PacketTypes.PlayerHp, "", id);
				args.Player.SendData(PacketTypes.PlayerUpdate, "", id);
				return true;
			}

			if (!TShock.Players[id].TPlayer.hostile && id != args.Player.Index)
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
			var data = PlayerPvpData.GetPlayerData(args.Player);

			var calculatedDmg = (int) Main.CalculatePlayerDamage(dmg, Main.player[id].statDefense);

			data.Damage(calculatedDmg);

			// 记录 承受伤害量
			PlayerPvpData.GetPlayerData(id).Hurt(calculatedDmg);

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
			var data = PlayerPvpData.GetPlayerData(args.Player);

			// 处理死亡事件
			data.Die(dmg);

			var killer = -1;
			foreach (var ply in TShock.Players)
			{
				if (ply != null && ply.Active && ply.TPlayer.hostile)
				{
					if (ply.Index == args.Player.Index)
					{
						continue;
					}

					if (text.Contains(ply.Name))
					{
						killer = ply.Index;
					}
				}
			}

			if (killer == -1)
			{
				return false;
			}

			var deathText = $"被{TShock.Players[killer].Name}杀死了!";

			// 处理杀死事件
			var killerData = PlayerPvpData.GetPlayerData(killer);
			killerData.Kill(ref deathText);

			Main.player[id].KillMeOld(dmg, direction, pvp == 1, deathText);
			NetMessage.SendData((int)PacketTypes.PlayerKillMe, -1, id, NetworkText.FromLiteral(deathText), id, direction, dmg, pvp);

			return true;
		}

		private static bool HandleKillMeV2(GetDataHandlerArgs args)
		{
			using (var br = new BinaryReader(args.Data))
			{
				int plr = br.ReadByte();
				var playerDeathReason2 = PlayerDeathReason.FromReader(br);
				int dmg = br.ReadInt16();
				var direction = br.ReadByte() - 1;
				var pvp2 = ((BitsByte)br.ReadByte())[0];

				if (!pvp2)
					return false;

				if (dmg > 20000) //Abnormal values have the potential to cause infinite loops in the server.
				{
					TShock.Utils.ForceKick(args.Player, "尝试破坏服务器.", true);
					TShock.Log.ConsoleError("死亡数值上限攻击: 数值 {0}", dmg);
					return false;
				}

				if (plr >= Main.maxPlayers)
				{
					return true;
				}

				if (!string.IsNullOrWhiteSpace(playerDeathReason2.SourceCustomReason) && playerDeathReason2.SourceCustomReason?.Length > 500)
				{
					TShock.Utils.Kick(TShock.Players[plr], "尝试破坏服务器.", true);
					return true;
				}

				args.Player.RespawnTimer = Nanami.Config.RespawnPvPSeconds;
				var data = PlayerPvpData.GetPlayerData(args.Player);

				// 处理死亡事件
				data.Die(dmg);

				var killer = playerDeathReason2.SourcePlayerIndex;
				var killerProj = playerDeathReason2.SourceProjectileType;
				var killerItem = playerDeathReason2.SourceItemType;

				var deathText = "被{0}的{1}杀死了!";

				if (killerProj != 0)
				{
					deathText = string.Format(deathText, TShock.Players[killer].Name, Lang.GetProjectileName(killerProj));
				}
				else if (killerItem != 0)
				{
					deathText = string.Format(deathText, TShock.Players[killer].Name, Lang.GetItemNameValue(killerItem));
				}
				else
				{
					deathText = $"被{TShock.Players[killer].Name}杀死了！";
				}

				// 处理杀死事件
				var killerData = PlayerPvpData.GetPlayerData(killer);
				killerData.Kill(ref deathText);

				playerDeathReason2.SourceCustomReason = args.Player.Name + deathText;

				Main.player[plr].KillMe(playerDeathReason2, dmg, direction, true);
				NetMessage.SendPlayerDeath(plr, playerDeathReason2, dmg, direction, true, -1, args.Player.Index);

				return true;
			}
		}

		private static bool HandleHurtV2(GetDataHandlerArgs args)
		{
			using (var br = new BinaryReader(args.Data))
			{
				int plr = br.ReadByte();
				if (!Main.player[plr].hostile || !args.TPlayer.hostile)
				{
					return false;
				}
				PlayerDeathReason.FromReader(br);
				int dmg = br.ReadInt16();
				br.ReadByte();
				BitsByte bitsByte19 = br.ReadByte();
				var pvp = bitsByte19[1];
				br.ReadSByte();

				if (plr >= Main.maxPlayers || TShock.Players[plr] == null)
				{
					return true;
				}

				if (!pvp) // 玩家受到普通伤害
					return false;

				if (dmg > TShock.Config.MaxDamage && !args.Player.HasPermission(Permissions.ignoredamagecap) && plr != args.Player.Index)
				{
					if (TShock.Config.KickOnDamageThresholdBroken)
					{
						TShock.Utils.Kick(args.Player, $"玩家攻击数值超过 {TShock.Config.MaxDamage}.");
						return true;
					}
					args.Player.Disable($"玩家攻击数值超过 {TShock.Config.MaxDamage}.", DisableFlags.WriteToLogAndConsole);
					args.Player.SendData(PacketTypes.PlayerHp, "", plr);
					args.Player.SendData(PacketTypes.PlayerUpdate, "", plr);
					return true;
				}

				if (!TShock.Players[plr].TPlayer.hostile && plr != args.Player.Index)
				{
					args.Player.SendData(PacketTypes.PlayerHp, "", plr);
					args.Player.SendData(PacketTypes.PlayerUpdate, "", plr);
					return true;
				}

				if (TShock.CheckIgnores(args.Player))
				{
					args.Player.SendData(PacketTypes.PlayerHp, "", plr);
					args.Player.SendData(PacketTypes.PlayerUpdate, "", plr);
					return true;
				}

				if (TShock.CheckRangePermission(args.Player, TShock.Players[plr].TileX, TShock.Players[plr].TileY, 100))
				{
					args.Player.SendData(PacketTypes.PlayerHp, "", plr);
					args.Player.SendData(PacketTypes.PlayerUpdate, "", plr);
					return true;
				}

				if ((DateTime.UtcNow - args.Player.LastThreat).TotalMilliseconds < 5000)
				{
					args.Player.SendData(PacketTypes.PlayerHp, "", plr);
					args.Player.SendData(PacketTypes.PlayerUpdate, "", plr);
					return true;
				}

				// 记录 伤害量
				var data = PlayerPvpData.GetPlayerData(args.Player);

				var calculatedDamage = (int)Main.CalculatePlayerDamage(dmg, Main.player[plr].statDefense);
				data.Damage(calculatedDamage);

				// 记录 承受伤害量
				PlayerPvpData.GetPlayerData(plr).Hurt(calculatedDamage);

				return false;
			}
		}
	}
}
