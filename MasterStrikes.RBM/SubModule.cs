using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;

namespace MasterStrikes.RBM
{
    public class SubModule : MBSubModuleBase
    {
        private static bool isPatchingValid;
        public static Harmony MasterStrikesRBMHarmony = new Harmony("MasterStrikes.RBM");
               
        private static void ApplyMSPatches()
        {
            // Отключаем патчи перед новым патчингом
            MasterStrikesRBMHarmony.UnpatchAll(MasterStrikesRBMHarmony.Id);            

            // RBM-патчи
            MasterStrikesRBMHarmony.Patch(
                original: AccessTools.Method(typeof(RBMAI.StanceLogic), nameof(RBMAI.StanceLogic.forceStaggerAnimation)),
                prefix: new HarmonyMethod(typeof(RBMPatcher), nameof(RBMPatcher.ForceStaggerAnimationPrefix))
            );

            MasterStrikesRBMHarmony.Patch(
                original: AccessTools.Method(typeof(RBMAI.StanceLogic), nameof(RBMAI.StanceLogic.forceTiredAnimation)),
                prefix: new HarmonyMethod(typeof(RBMPatcher), nameof(RBMPatcher.ForceTiredAnimationPrefix))
            );

            MasterStrikesRBMHarmony.Patch(
                original: AccessTools.Method(typeof(RBMAI.Stance), nameof(RBMAI.Stance.tickStaminaRegen)),
                prefix: new HarmonyMethod(typeof(RBMPatcher), nameof(RBMPatcher.TickStaminaRegenPrefix))
            );

            MasterStrikesRBMHarmony.Patch(
                original: AccessTools.Method(typeof(MasterStrikesBehavior), nameof(MasterStrikesBehavior.LaunchStaggerAction)),
                prefix: new HarmonyMethod(typeof(RBMPatcher), nameof(RBMPatcher.LaunchStaggerActionPrefix))
            );
        }


        private static bool _hooksSubscribed;
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            if (ModuleHelper.IsModuleActive("RBM"))
            {
                isPatchingValid = true;
                ApplyMSPatches();
            }
            else
            {
                isPatchingValid = false;
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=FgLhzCLv}MasterStrikes (RBM) patches were not applied. RBM is missing. RBM integration will not work.").ToString(), Color.ConvertStringToColor("#ff0f0fff")));
            }

            if (!_hooksSubscribed)
            {
                // Подписка на хуки базового MasterStrikes.dll
                MasterStrikesBehavior.OnCanStartMasterStrike = RBMIntegration.OnCanStartMasterStrike;
                MasterStrikesBehavior.OnMasterStrikeStarted = RBMIntegration.OnMasterStrikeStarted;
                MasterStrikesBehavior.OnMasterStrikeFinished = RBMIntegration.OnMasterStrikeFinished;

                MasterStrikesBehavior.OnCanStartClinch = RBMIntegration.OnCanStartClinch;
                MasterStrikesBehavior.OnClinchStarted = RBMIntegration.OnClinchStarted;
                MasterStrikesBehavior.OnClinchFinished = RBMIntegration.OnClinchFinished;

                MasterStrikesBehavior.OnCanDodge = RBMIntegration.OnCanDodge;
                MasterStrikesBehavior.OnDodgeStarted = RBMIntegration.OnDodgeStarted;

                _hooksSubscribed = true;
            }
        }

        protected override void RegisterSubModuleTypes()
        {
            base.RegisterSubModuleTypes();
            if (isPatchingValid == true) ApplyMSPatches();            
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (isPatchingValid == true)
            {
                ApplyMSPatches();
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=HoNganQb}Loaded MasterStrikes (RBM) succeeded").ToString(), Color.ConvertStringToColor("#c2ff66ff")));
            }
        }
    }
}