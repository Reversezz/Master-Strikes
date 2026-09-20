using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace MasterStrikes.RBM
{
    public sealed class ModSettings : AttributeGlobalSettings<ModSettings>
    {
        public override string Id => "MSbReversezz_RBM_Submod";
        public override string DisplayName => $"Master Strikes (RBM) {typeof(ModSettings).Assembly.GetName().Version.ToString(3)}";
        public override string FolderName => "MasterStrikes.RBM";
        public override string FormatType => "json";


        [SettingPropertyGroup("{=VFKEDUwW}General Settings", GroupOrder = 0)]
        [SettingPropertyBool("{=UDuGewGx}Accelerate the recovery of stamina while walking", HintText = "{=WTTKVeEu}This feature is rather cheaty, but it allows you to use Master Strikes and Dodges from my mod more often. Increases stamina recovery speed (depending on Athletics skill) while walking.", Order = 1, RequireRestart = false)]
        public bool SpeedUpRecoveryStaminaInWalkMode { get; set; } = false;
    }
}