using RBMAI;
using RBMConfigForMS = RBMConfig.RBMConfig;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace MasterStrikes.RBM
{
    internal static class RBMPatcher
    {
        internal static bool ForceStaggerAnimationPrefix(Agent agent)
        {
            if (Mission.Current == null) return true;
            if (agent == null || !agent.IsAgentCorrect()) return true;
            if (!RBMConfigForMS.postureEnabled) return true;

            // Если занят - блочим стаггер во время парной анимации
            return !agent.IsBusy();
        }

        internal static bool ForceTiredAnimationPrefix(Agent agent)
        {
            if (Mission.Current == null) return true;
            if (agent == null || !agent.IsAgentCorrect()) return true;
            if (!RBMConfigForMS.postureEnabled) return true;

            return !agent.IsBusy();
        }

        internal static void TickStaminaRegenPrefix(Stance __instance, ref float multiplier)
        {
            if (Mission.Current == null) return;
            if (AgentStances.values == null) return;
            if (ModSettings.Instance == null || !ModSettings.Instance.SpeedUpRecoveryStaminaInWalkMode) return;

            foreach (KeyValuePair<Agent, Stance> entry in AgentStances.values)
            {
                if (entry.Value != __instance || entry.Value == null) continue;
                if (entry.Value.maxStamina <= 0f) return;
                if (!entry.Key.IsAgentCorrect()) continue;

                if (entry.Key.GetStamina() >= entry.Value.maxStamina / 2.5f) return;

                if (entry.Key.WalkMode)
                {
                    multiplier *= 2f + entry.Key.Character.GetSkillValue(DefaultSkills.Athletics) / 250;
                    break;
                }
            }
        }

        internal static bool LaunchStaggerActionPrefix(ref bool __result)
        {
            __result = false; // результат будет false, т.е. логика стаггера не будет работать из оригинала
            return false; // скипаем оригинальный метод
        }
    }
}