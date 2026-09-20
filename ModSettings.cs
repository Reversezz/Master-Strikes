using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using System;
using System.Diagnostics;
using System.Linq;
using TaleWorlds.ModuleManager;

namespace MasterStrikes
{
    /// <summary>
    /// МCM Настройки.
    /// </summary>
    public class ModSettings : AttributeGlobalSettings<ModSettings>
    {
        public override string Id => "MSbReversezz";
        public override string DisplayName => $"Master Strikes {typeof(ModSettings).Assembly.GetName().Version.ToString(3)}";
        public override string FolderName => "MasterStrikes";
        public override string FormatType => "json";


        [SettingPropertyGroup("{=IkVGCPzp}Support Author", GroupOrder = 0)]
        [SettingPropertyButton("{=MJlwKrHw}Support on Boosty", Content = "{=RtJUCkUu}Open Boosty page", Order = 0, HintText = "{=cfNyUaDS}Click to go to my Boosty page in the browser. Thanks for the support!")]
        public Action OpenBoosty { get; set; } = (() => Process.Start(new ProcessStartInfo("https://boosty.to/reversezz") { UseShellExecute = true }));

        [SettingPropertyGroup("{=IkVGCPzp}Support Author", GroupOrder = 0)]
        [SettingPropertyButton("DALINK", Content = "{=YsggMjHt}Open DALINK", Order = 1, HintText = "{=ruCvvbHD}Click to go to my DALINK page in the browser. Thanks for the donate!")]
        public Action OpenDALINK { get; set; } = (() => Process.Start(new ProcessStartInfo("https://dalink.to/reversezz") { UseShellExecute = true }));

        

        [SettingPropertyGroup("{=SHwSVSRH}Features", GroupOrder = 1)]
        [SettingPropertyBool("{=SoxWZDgR}Enable Master Strikes", Order = 1, RequireRestart = false, HintText = "{=JlPErQLH}Toggle Master Strikes on or off. If disabled, neither AI nor player can perform master strikes.")]
        public bool EnableMasterStrikes { get; set; } = true;

        [SettingPropertyGroup("{=SHwSVSRH}Features", GroupOrder = 1)]
        [SettingPropertyBool("{=gcESEDSD}Enable Dodges", Order = 2, RequireRestart = false, HintText = "{=GdFrTEFe}Toggle dodges on or off. If disabled, neither AI nor player can dodge.")]
        public bool EnableDodges { get; set; } = true;

        [SettingPropertyGroup("{=SHwSVSRH}Features", GroupOrder = 1)]
        [SettingPropertyBool("{=OPxCkZRU}Enable Clinches", Order = 3, RequireRestart = false, HintText = "{=vBwWJHyV}Toggle clinches on or off. If disabled, neither AI nor player can enter a clinch.")]
        public bool EnableClinch { get; set; } = true;


        /// <summary>
        /// Вкл/выкл оригинальные анимации оглушения/потери равновесия (stagger) от мастерского удара или клинча.
        /// </summary>
        [SettingPropertyGroup("{=SHwSVSRH}Features" + "/" + "{=mAjcifnr}Additionally", GroupOrder = 1)]
        [SettingPropertyBool("{=lvzjtaCN}Enable Stun", Order = 0, RequireRestart = false, HintText = "{=aagLyxHP}Enable or disable stun. If the feature is disabled, master strikes or clinching will not contribute to stun. It will lose all meaning if MasterStrikes.RBM is enabled.")]        
        public bool EnableStaggerActions { get; set; } = true;

        /// <summary>
        /// Вкл/выкл неуязвимость во время парных анимаций.
        /// </summary>
        [SettingPropertyGroup("{=SHwSVSRH}Features" + "/" + "{=mAjcifnr}Additionally", GroupOrder = 1)]
        [SettingPropertyBool("{=ugScFozt}Enable Invulnerability During Animations", Order = 1, RequireRestart = false, HintText = "{=IEMNjgoZ}Enable invulnerability during custom animations (master strikes and clinch attacks, if they are enabled).")]
        public bool EnableInvulnerabilityDuringActions { get; set; } = false;



        [SettingPropertyGroup("{=BPjFzACq}General Settings", GroupOrder = 2)]
        [SettingPropertyInteger("{=ZihNuHzV}Master Strike Skill Threshold", 0, 300, Order = 1, RequireRestart = false, HintText = "{=GyDojzNR}Minimum level of weapon skill (OneHanded, TwoHanded, Polearm, etc.) that an agent must have to be able to perform a Master Strike (including the player).")]
        public int MSRequiredWeaponSkill { get; set; } = 70;

        [SettingPropertyGroup("{=BPjFzACq}General Settings", GroupOrder = 2)]
        [SettingPropertyInteger("{=JxTJiRaW}Clinch Skill Threshold", 0, 300, Order = 2, RequireRestart = false, HintText = "{=PDXITrXY}Minimum level of weapon skill (OneHanded, TwoHanded, Polearm, etc.) that an agent must have to be able to perform a Clinch (including the player).")]
        public int ClinchRequiredWeaponSkill { get; set; } = 50;

        [SettingPropertyGroup("{=BPjFzACq}General Settings", GroupOrder = 2)]
        [SettingPropertyInteger("{=qIPRveZa}Dodge Athletics Threshold", 0, 300, Order = 3, RequireRestart = false, HintText = "{=rWVVRYDI}Minimum Athletics skill an agent must have to be able to dodge (including the player). Agents with Athletics below this value will never attempt a dodge.")]
        public int DodgeRequiredAthletics { get; set; } = 40;



        [SettingPropertyGroup("{=BPjFzACq}General Settings" + "/" + "{=fxuEuxoj}Multipliers", GroupOrder = 1)]
        [SettingPropertyFloatingInteger("{=kIVrsZHy}AI Master Strike Chance Multiplier", 0f, 3f, Order = 1, RequireRestart = false)]
        public float AIMasterStrikeChanceMultiplier { get; set; } = 1f;
        
        [SettingPropertyGroup("{=BPjFzACq}General Settings" + "/" + "{=fxuEuxoj}Multipliers", GroupOrder = 1)]
        [SettingPropertyFloatingInteger("{=UfVnVGJv}AI Clinch Chance Multiplier", 0f, 3f, Order = 2, RequireRestart = false)]
        public float AIClinchChanceMultiplier { get; set; } = 1f;

        [SettingPropertyGroup("{=BPjFzACq}General Settings" + "/" + "{=fxuEuxoj}Multipliers", GroupOrder = 1)]
        [SettingPropertyFloatingInteger("{=ehznVzCS}AI Dodge Chance Multiplier", 0f, 3f, Order = 3, RequireRestart = false)]
        public float AIDodgeChanceMultiplier { get; set; } = 1f;      



        /// <summary>
        /// Cообщение в журнале событий о вкл/выкл доджа для игрока.
        /// </summary>
        [SettingPropertyGroup("{=BPjFzACq}General Settings" + "/" + "{=OJNlPEmO}Notifications", GroupOrder = 2)]
        [SettingPropertyBool("{=ZCOBpSTb}Dodge Toggle Message", Order = 1, RequireRestart = false)]
        public bool DodgeToggleMessage { get; set; } = true;

        /// <summary>
        /// Cообщение в журнале событий о эффекте адреналина при 3-х успешных парных анимаций подряд.
        /// </summary>
        [SettingPropertyGroup("{=BPjFzACq}General Settings" + "/" + "{=OJNlPEmO}Notifications", GroupOrder = 2)]
        [SettingPropertyBool("{=cxbOwvTD}Adrenaline Buff Messages", Order = 2, RequireRestart = false)]
        public bool AdrenalineBuffMessages { get; set; } = true;



        [SettingPropertyGroup("{=HRkXragt}Controller", GroupOrder = 3)]
        [SettingPropertyDropdown("{=LaEqjxna}MasterStrikes Gamepad Button", Order = 1, RequireRestart = false, HintText = "{=xOkbDtwC}If you don't play with a gamepad, set it to Invalid.")]
        public Dropdown<ControllerKey> MSControllerKey { get; set; } = new Dropdown<ControllerKey>(
            Enum.GetValues(typeof(ControllerKey)).Cast<ControllerKey>().ToArray(),
            selectedIndex: (int)ControllerKey.Invalid
        );

        [SettingPropertyGroup("{=HRkXragt}Controller", GroupOrder = 3)]
        [SettingPropertyDropdown("{=ZBGrVBEH}Clinch Gamepad Button", Order = 2, RequireRestart = false, HintText = "{=xOkbDtwC}If you don't play with a gamepad, set it to Invalid.")]
        public Dropdown<ControllerKey> ClinchControllerKey { get; set; } = new Dropdown<ControllerKey>(
            Enum.GetValues(typeof(ControllerKey)).Cast<ControllerKey>().ToArray(),
            selectedIndex: (int)ControllerKey.Invalid
        );

        [SettingPropertyGroup("{=HRkXragt}Controller", GroupOrder = 3)]
        [SettingPropertyDropdown("{=vKjHuWnH}Dodge Gamepad Button", Order = 3, RequireRestart = false, HintText = "{=xOkbDtwC}If you don't play with a gamepad, set it to Invalid.")]
        public Dropdown<ControllerKey> DodgeControllerKey { get; set; } = new Dropdown<ControllerKey>(
            Enum.GetValues(typeof(ControllerKey)).Cast<ControllerKey>().ToArray(),
            selectedIndex: (int)ControllerKey.Invalid
        );


        public enum ControllerKey
        {
            Invalid,
            ControllerLStick = 222,
            ControllerRStick = 223,
            ControllerLOptionTap = 231,
            ControllerLStickUp = 232,
            ControllerLStickDown = 233,
            ControllerLStickLeft = 234,
            ControllerLStickRight = 235,
            ControllerRStickUp = 236,
            ControllerRStickDown = 237,
            ControllerRStickLeft = 238,
            ControllerRStickRight = 239,
            ControllerLUp = 240,
            ControllerLDown = 241,
            ControllerLLeft = 242,
            ControllerLRight = 243,
            ControllerRUp = 244,
            ControllerRDown = 245,
            ControllerRLeft = 246,
            ControllerRRight = 247,
            ControllerLBumper = 248,
            ControllerRBumper = 249,
            ControllerLOption = 250,
            ControllerROption = 251,
            ControllerLThumb = 252,
            ControllerRThumb = 253,
            ControllerLTrigger = 254,
            ControllerRTrigger = 255
        }

        [SettingPropertyGroup("Подсветка", GroupOrder = 4)]
        [SettingPropertyBool("Подсвечивать MS и CL", Order = 1, RequireRestart = false)]
        public bool Highlight { get; set; } = false;

        [SettingPropertyGroup("Подсветка", GroupOrder = 4)]
        [SettingPropertyBool("Подсвечивать доджи", Order = 2, RequireRestart = false)]
        public bool HighlightDodges { get; set; } = false;
    }
}