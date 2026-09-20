using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace MasterStrikes
{
    public class MasterStrikesPatches
    {

        // Блок альтернативной атаки игроку
        [HarmonyPriority(Priority.Last)]
        public static void KickClearPostfix(Agent __instance, ref bool __result)
        {
            if (Mission.Current == null) return;
            if (__instance == null || !__instance.IsActive()) return;

            if (__instance.IsBusy() || __instance.IsInAnyMSAction() || __instance.IsStaggered())
            {
                __result = false; // скипаем оригинальный KickClear, говорим что кик невозможен, если агент в MS
            }
        }


        // Блок альтернативной атаки ИИ (предотвращение возможного функционала других модифицакаций)
        [HarmonyPriority(Priority.Last)]
        public static void OnAIInputSetPostfix(Agent __instance, ref Agent.EventControlFlag eventFlag)
        {
            if (Mission.Current == null) return;
            if (__instance == null || !__instance.IsActive()) return;

            if (__instance.IsBusy() || __instance.IsInAnyMSAction() || __instance.IsStaggered())
            {
                eventFlag &= ~Agent.EventControlFlag.Kick;
            }
        }        
    }
}