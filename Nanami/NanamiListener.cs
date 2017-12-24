using System;
using Terraria;
using TShockAPI;

namespace Nanami
{
    internal class NanamiListener : IDisposable
    {
        public NanamiListener()
        {
            GetDataHandlers.PlayerDamage += OnPlayerDamage;
            GetDataHandlers.KillMe += OnKillMe;
        }

        public void Dispose()
        {
            GetDataHandlers.PlayerDamage -= OnPlayerDamage;
            GetDataHandlers.KillMe -= OnKillMe;
        }

        private static void OnPlayerDamage(object sender, GetDataHandlers.PlayerDamageEventArgs args)
        {
            // 记录 伤害量
            var data = PlayerPvpData.GetPlayerData(args.Player);

            var calculatedDmg = (int)Main.CalculatePlayerDamage(args.Damage, Main.player[args.ID].statDefense);

            data.Damage(calculatedDmg);

            // 记录 承受伤害量
            PlayerPvpData.GetPlayerData(args.ID).Hurt(calculatedDmg);
        }

        private static void OnKillMe(object sender, GetDataHandlers.KillMeEventArgs args)
        {
            if (!args.Pvp)
            {
                return;
            }

            args.Player.RespawnTimer = Nanami.Config.RespawnPvPSeconds;
            var data = PlayerPvpData.GetPlayerData(args.Player);

            // 处理死亡事件
            data.Die(args.Damage);

            var killer = args.PlayerDeathReason.SourcePlayerIndex;
            var killerProj = args.PlayerDeathReason.SourceProjectileType;
            var killerItem = args.PlayerDeathReason.SourceItemType;

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

            args.PlayerDeathReason.SourceCustomReason = args.Player.Name + deathText;

            Main.player[args.PlayerId].KillMe(args.PlayerDeathReason, args.Damage, args.Direction, true);
            NetMessage.SendPlayerDeath(args.PlayerId, args.PlayerDeathReason, args.Damage, args.Direction, true, -1, args.Player.Index);

            args.Handled = true;
        }
    }
}
