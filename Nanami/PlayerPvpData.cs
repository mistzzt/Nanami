using System.Diagnostics.CodeAnalysis;
using Terraria;
using TShockAPI;
using Microsoft.Xna.Framework;

namespace Nanami
{
	[SuppressMessage("ReSharper", "InvertIf")]
	internal class PlayerPvpData
	{
		/// <summary> 击杀 </summary>
		public int Kills { get; private set; }

		/// <summary> 死亡 </summary>
		public int Deaths { get; private set; }

		/// <summary> 伤害量 </summary>
		public int Hurts { get; private set; }

		/// <summary> 承受伤害量 </summary>
		public int Endurance { get; private set; }

		/// <summary> 连续击杀 </summary>
		public int SuccessiveKills { get; private set; }

		/// <summary> 最高连续击杀 </summary>
		public int MaxSuccessiveKills { get; private set; }

		public readonly int PlayerIndex;

		public PlayerPvpData(int index)
		{
			PlayerIndex = index;
		}

		public static PlayerPvpData GetData(int index)
		{
			var player = TShock.Players[index];
			var data = player.GetData<PlayerPvpData>(Nanami.NanamiPvpData);

			return data;
		}

		/// <summary>
		/// 玩家歼敌事件
		/// </summary>
		public void Kill(ref string deathText)
		{
			Kills++;
			SuccessiveKills++;

			if (SuccessiveKills > MaxSuccessiveKills)
				MaxSuccessiveKills = SuccessiveKills;

			if (SuccessiveKills >= Nanami.Config.MinKillTime)
			{
				var clrIndex = SuccessiveKills - Nanami.Config.MinKillTime;

				var gradeMsg = string.Format(" {0} {1}", TShock.Players[PlayerIndex].Name,
					Nanami.Config.KillsText.Length > clrIndex ? Nanami.Config.KillsText[clrIndex] : $"连续消灭 {SuccessiveKills} 人!");

				var succKillClr = Nanami.Config.RealColors.Length > clrIndex ? Nanami.Config.RealColors[clrIndex] : Color.Yellow;

				deathText += TShock.Utils.ColorTag(gradeMsg, succKillClr);
			}
		}

		/// <summary>
		/// 玩家死亡事件
		/// </summary>
		/// <param name="dmg">未经计算的攻击数值</param>
		public void Die(int dmg)
		{
			Deaths++;
			Endurance -= (int)Main.CalculatePlayerDamage(dmg, Main.player[PlayerIndex].statDefense);
			if (SuccessiveKills >= Nanami.Config.MinKillTime)
				TShock.Players[PlayerIndex].SendInfoMessage("你已死亡, 临死前最大连续击杀数: {0}", SuccessiveKills);
			SuccessiveKills = 0;
		}

		/// <summary>
		/// 玩家受伤事件
		/// </summary>
		/// <param name="calculatedDmg">经计算的攻击数值</param>
		public void Hurt(int calculatedDmg)
		{
			Endurance += calculatedDmg;
		}

		/// <summary>
		/// 玩家攻击事件
		/// </summary>
		/// <param name="calculatedDmg">经计算的攻击数值</param>
		public void Damage(int calculatedDmg)
		{
			Hurts += calculatedDmg;
		}
	}
}
