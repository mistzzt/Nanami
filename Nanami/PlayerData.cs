using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanami {
	internal class PlayerData {
		/// <summary> 等待判定玩家 </summary>
		public bool WaitforKiller = false;
		/// <summary> 临死受到伤害量 </summary>
		public int DeathHurtValue;

		/// <summary> 击杀 </summary>
		public int Kills = 0;
		/// <summary> 死亡 </summary>
		public int Deaths = 0;

		/// <summary> 伤害量 </summary>
		public int Hurt = 0;
		/// <summary> 承受伤害量 </summary>
		public int Endurance = 0;

		/// <summary> 最高连续击杀 </summary>
		public int MaxSuccessiveKills = 0;

		public readonly int PlayerIndex;

		public Stack<int> Damages = new Stack<int>(5); 

		public PlayerData(int index) {
			PlayerIndex = index;
		}

	}
}
