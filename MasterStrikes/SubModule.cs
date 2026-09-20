using Bannerlord.ButterLib.HotKeys;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using HotKeyManager = Bannerlord.ButterLib.HotKeys.HotKeyManager;

// 1.4.8, 1.4.7, 1.4.6
namespace MasterStrikes
{
    public class SubModule : MBSubModuleBase
    {
        public static Harmony MasterStrikesHarmony = new Harmony("MasterStrikes");


        private static bool _hotKeysRegistered = false;
        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            
            if (!_hotKeysRegistered && ModuleHelper.IsModuleActive("Bannerlord.Harmony"))
            {
                HotKeyManager MasterStrikesHotKeys = HotKeyManager.Create("MasterStrikesHK");

                var msKey = MasterStrikesHotKeys.Add<MasterStrikeKey>();
                var dodgeKey = MasterStrikesHotKeys.Add<DodgeKey>();
                var clinchKey = MasterStrikesHotKeys.Add<ClinchKey>();

                MasterStrikesHotKeys.Build();

                MasterStrikesBehavior.MSKey = (GameKey)msKey;
                MasterStrikesBehavior.DodgeKey = (GameKey)dodgeKey;
                MasterStrikesBehavior.ClinchKey = (GameKey)clinchKey;

                _hotKeysRegistered = true;                               
            }
        }


        private static bool _patchesApplied = false;        
        protected override void RegisterSubModuleTypes()
        {
            base.RegisterSubModuleTypes();
            
            if (!_patchesApplied && ModuleHelper.IsModuleActive("Bannerlord.ButterLib"))
            {
                MasterStrikesHarmony.Patch(
                    original: AccessTools.Method(typeof(Agent), "KickClear"),
                    postfix: new HarmonyMethod(typeof(MasterStrikesPatches), nameof(MasterStrikesPatches.KickClearPostfix))
                );

                MasterStrikesHarmony.Patch(
                    original: AccessTools.Method(typeof(Agent), "OnAIInputSet"),
                    postfix: new HarmonyMethod(typeof(MasterStrikesPatches), nameof(MasterStrikesPatches.OnAIInputSetPostfix))
                );

                // Проверка после применения                
                if (Harmony.HasAnyPatches(MasterStrikesHarmony.Id))
                    _patchesApplied = true;
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (_patchesApplied && _hotKeysRegistered)
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=IUcbnwAZ}Loaded MasterStrikes succeeded").ToString(), Color.ConvertStringToColor("#adff66ff")));
            
            if (ModSettings.Instance.Highlight)
                if (Gags.HighlightTimers != null || Gags.HighlightTimers.Count > 0) Gags.HighlightTimers.Clear();
        }


        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            if (mission != null && mission == Mission.Current)
                mission.AddMissionBehavior(new MasterStrikesBehavior());
        }


        // БИНДЫ В НАСТРОЙКАХ 
        public class MasterStrikeKey : HotKeyBase
        {
            public MasterStrikeKey() : base(nameof(MasterStrikeKey),
                displayName: "{=VVuvGqGO}Master Strike",
                description: "{=uBuugWVL}Blocking an enemy's attack, press your chosen key simultaneously with block to perform a master strike.",
                defaultKey: InputKey.LeftMouseButton,
                category: HotKeyManager.Categories[HotKeyCategory.Action])
            { }
        }

        public class DodgeKey : HotKeyBase
        {
            public DodgeKey() : base(nameof(DodgeKey),
                displayName: "{=EJqiXgJS}Dodge",
                description: "{=raVxceTU}Hold your chosen key and press A/S/D to dodge.",
                defaultKey: InputKey.LeftControl,
                category: HotKeyManager.Categories[HotKeyCategory.Action])
            { }
        }

        public class ClinchKey : HotKeyBase
        {
            public ClinchKey() : base(nameof(ClinchKey),
                displayName: "{=lGzOnCKE}Clinch",
                description: "{=lbaMTFBA}Press this key when very close to an enemy to start a clinch.",
                defaultKey: InputKey.Q,
                category: HotKeyManager.Categories[HotKeyCategory.Action])
            { }
        }
    }    
}