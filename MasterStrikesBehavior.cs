using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;
using static MasterStrikes.ModSettings;
using static TaleWorlds.MountAndBlade.HighlightsController;

namespace MasterStrikes
{
    public class MasterStrikesBehavior : MissionLogic
    {
        public static MasterStrikesBehavior Instance { get; set; }
        public MasterStrikesBehavior() { Instance = this; }


        public static Func<Agent, Agent, bool>  OnCanStartMasterStrike;
        public static Func<Agent, Agent, bool>  OnCanStartClinch;
        public static Func<Agent, bool>         OnCanDodge;

        public static Action<Agent, Agent>      OnMasterStrikeStarted;
        public static Action<Agent, Agent, AttackCollisionData>  OnMasterStrikeFinished;

        public static Action<Agent, Agent>      OnClinchStarted;
        public static Action<Agent, Agent, AttackCollisionData>  OnClinchFinished;

        public static Action<Agent>             OnDodgeStarted;


        public override void OnAgentCreated(Agent agent)
        {
            base.OnAgentCreated(agent);

            if (Mission.Current != null && ( Mission.Current.IsFieldBattle
                                          || Mission.Current.IsSiegeBattle
                                          || Mission.Current.IsSallyOutBattle
                                          || Mission.Current.IsNavalBattle
                                          || Mission.Current.IsNavalRaidBattle
                                          || Mission.Current.Mode == MissionMode.Battle
                                          || Mission.Current.SceneName.Contains("hideout")
                                          || Mission.Current.SceneName.Contains("arena")
                                          || Mission.Current.SceneName.Contains("tournament")
                                          || Mission.Current.SceneName.Contains("village")))
            { // ---------------------------------------------------------------------------------------------------------------------------------------------------- //
                
                if (agent != null && agent.IsAgentCorrect())
                {
                    // Выдаём компонент клинча при условии, что у агента достаточный навык владения оружием
                    if (ModSettings.Instance.EnableClinch && agent.GetSkillValueFromWeaponClass() >= ModSettings.Instance.ClinchRequiredWeaponSkill)
                    {
                        agent.AddComponent(new AIClinchComponent(agent));
                        //InformationManager.DisplayMessage(new InformationMessage($"{agent?.Name}: Получил AIClinchComponent"));                        
                    }

                    // Выдаём компонент доджа при условии, что у агента достаточный навык атлетики
                    if (ModSettings.Instance.EnableDodges && agent.Character.GetSkillValue(DefaultSkills.Athletics) >= ModSettings.Instance.DodgeRequiredAthletics)
                    {
                        agent.AddComponent(new AIDodgeComponent(agent));
                        //InformationManager.DisplayMessage(new InformationMessage($"{agent?.Name}: Получил AIDodgeComponent"));                        
                    }
                }                    
                
            } // ---------------------------------------------------------------------------------------------------------------------------------------------------- //
        }

        // Клавиши с биндов
        internal static GameKey MSKey;
        internal static GameKey DodgeKey;
        internal static GameKey ClinchKey;

        internal readonly Dictionary<Agent, Agent> _masterstrikeAgents = new Dictionary<Agent, Agent>();
        internal readonly Dictionary<Agent, Agent> _clinchAgents = new Dictionary<Agent, Agent>();
        public Dictionary<Agent, Agent> GetMasterstrikeAgents => _masterstrikeAgents;
        public Dictionary<Agent, Agent> GetClinchAgents => _clinchAgents;

        internal Dictionary<Agent, string> LastAgentsMS = new Dictionary<Agent, string>();
        internal Dictionary<Agent, string> LastAgentsCL = new Dictionary<Agent, string>();

        internal readonly Dictionary<Agent, int> _streakAgents = new Dictionary<Agent, int>();
               

        private readonly string[] ShieldBashSounds =
        [
            "shield_bash_1",
            "shield_bash_2",
            "shield_bash_3",
            "shield_bash_4",
            "shield_bash_5"
        ];

        private readonly string[] WoodShieldBlockSounds =
        [
            "wood_shield_block_1",
            "wood_shield_block_2",
            "wood_shield_block_3",
            "wood_shield_block_4",
            "wood_shield_block_5",
            "wood_shield_block_6",
            "wood_shield_block_7"
        ];

        private readonly string[] MetalShieldBlockSounds =
        [
            "metal_shield_block_1",
            "metal_shield_block_2",
            "metal_shield_block_3"
        ];

        private readonly string[] PunchSounds =
        [
            "punch_1",
            "punch_2",
            "punch_3",
            "punch_4",
        ];


        public override void OnAgentHit(Agent affectedAgent, Agent affectorAgent, in MissionWeapon affectorWeapon, in Blow blow, in AttackCollisionData attackCollisionData)
        {
            base.OnAgentHit(affectedAgent, affectorAgent, affectorWeapon, blow, attackCollisionData);
            if (!ModSettings.Instance.EnableMasterStrikes) return;

            if (Mission.Current.Mode != MissionMode.Battle) return;

            if (!affectedAgent.IsAgentCorrect() || !affectorAgent.IsAgentCorrect()) return;
            //if (affectorAgent.IsMainAgent && blow.IsMissile) InformationManager.DisplayMessage(new InformationMessage($"Урон {blow.InflictedDamage} оружием {affectorWeapon}"));

            if (affectorAgent.IsBusy() || affectorAgent.IsStaggered()) return;

            if (affectedAgent.IsBusy() && (affectedAgent.IsThisWasTargetedAttackBy(affectorAgent, blow, attackCollisionData) || attackCollisionData.IsHorseCharge))
            {
                ResetMSAgentsAfterExternalDamage(affectedAgent);
                return;
            }
            else if (affectedAgent.IsBusy() || affectedAgent.IsStaggered()) return;

            if (affectorAgent.GetCurrentAction(0).GetName().Contains("jump") || affectedAgent.GetCurrentAction(0).GetName().Contains("jump")) return;
            


            if (affectorAgent.Team != affectedAgent.Team)
            {
                if (affectorAgent.MountAgent == null && affectedAgent.MountAgent == null)
                {
                    if (!blow.IsMissile && !attackCollisionData.IsAlternativeAttack)
                    {
                        if (!(blow.BlowFlag == BlowFlags.KnockBack) && !(blow.BlowFlag == BlowFlags.KnockDown))
                        {                            
                            if (!affectedAgent.WieldedWeapon.IsEmpty && !affectorAgent.WieldedWeapon.IsEmpty)
                            {
                                if (affectedAgent.Position.Distance(affectorAgent.Position) <= 2.1f)
                                {
                                    if (attackCollisionData.CollisionResult == CombatCollisionResult.Blocked || attackCollisionData.CollisionResult == CombatCollisionResult.Parried)
                                    {
                                        // Проверяем навыки оружия на руках агента (в зависимости от класса запускаются MS)
                                        if (affectedAgent.GetSkillValueFromWeaponClass() < ModSettings.Instance.MSRequiredWeaponSkill) return;

                                        if (OnCanStartMasterStrike != null && !OnCanStartMasterStrike(affectedAgent, affectorAgent)) return;

                                        // Если контратакующий - это игрок
                                        if (affectedAgent.IsMainAgent)
                                        {
                                            if (Mission.Current?.InputManager.IsGameKeyDown(CombatHotKeyCategory.Defend) == true && IsMSKeyPressed())
                                            {
                                                _masterstrikeAgents.Add(affectedAgent, affectorAgent);
                                                new MasterStrikeLogic(affectedAgent, affectorAgent);                                                
                                            }
                                        }
                                        else
                                        {
                                            if ((affectedAgent.GetSkillValueFromWeaponClass() / 4) * ModSettings.Instance.AIMasterStrikeChanceMultiplier >= MBRandom.RandomInt(1, 100))
                                            {
                                                _masterstrikeAgents.Add(affectedAgent, affectorAgent);
                                                new MasterStrikeLogic(affectedAgent, affectorAgent);                                              
                                            }
                                        }

                                    }
                                }                                
                            }
                        }
                    }
                }
            }

        }
        


        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            // ВКЛ/ОТКЛ ДОДЖЕЙ
            if (InputKey.CapsLock.IsPressed())
            {
                CanDodge = !CanDodge;

                if (ModSettings.Instance.DodgeToggleMessage)
                {
                    string message = CanDodge ? new TextObject("{=PAeezuMG}You switched to run mode. Dodges enabled.").ToString()
                                              : new TextObject("{=nVKCqsRX}You switched to walk mode. Dodges disabled.").ToString();

                    InformationManager.DisplayMessage(new InformationMessage("MS: " + message));
                }
            }

            // Если включена система бессмертия во время МС и клинчей
            if (ModSettings.Instance.EnableInvulnerabilityDuringActions) 
            {   
                if (Mission.Current != null &&   ( Mission.Current.IsFieldBattle
                                                || Mission.Current.IsSiegeBattle
                                                || Mission.Current.IsSallyOutBattle
                                                || Mission.Current.IsNavalRaidBattle
                                                || Mission.Current.Mode == MissionMode.Battle
                                                || Mission.Current.SceneName.Contains("battle")
                                                || Mission.Current.SceneName.Contains("hideout")
                                                || Mission.Current.SceneName.Contains("arena")
                                                || Mission.Current.SceneName.Contains("tournament")
                                                || Mission.Current.SceneName.Contains("village")))
                { // ------------------------------------------------------------------------------------ //
                    HandleStrandedInvulnerableAgents();
                }
                else if (Mission.Current != null && Mission.Current.IsNavalBattle && (Mission.Current.Mode == MissionMode.Battle || Mission.Current.MissionTeamAIType == Mission.MissionTeamAITypeEnum.NavalBattle))
                {
                    HandleNavalBattleAgents();
                } 
                //InformationManager.DisplayMessage(new InformationMessage($"\n\nMissionTeamAIType: {Mission.Current?.MissionTeamAIType}"));
                //InformationManager.DisplayMessage(new InformationMessage($"\nMissionMode: {Mission.Current?.Mode}"));
                //InformationManager.DisplayMessage(new InformationMessage($"\nSceneName: {Mission.Current?.SceneName}"));
            }

            if (Mission.Current.Mode == MissionMode.Battle)
            {
                // --- MASTER STRIKES ---
                if (_masterstrikeAgents.Count > 0) 
                    WorkingWithMS();         
            
                // --- CLINCHES ---
                if (_clinchAgents.Count > 0) 
                    WorkingWithCL();
            }
            else if (_masterstrikeAgents.Count != 0 || _clinchAgents.Count != 0)
            {
                var ms = _masterstrikeAgents;
                var cl = _clinchAgents;

                foreach (var agent in ms.Keys)
                {
                    ResetMSAgentsAfterExternalDamage(agent);
                }

                foreach (var agent in cl.Keys)
                {
                    ResetMSAgentsAfterExternalDamage(agent);
                }
            }

            if (ModSettings.Instance.Highlight)
            {
                if (Gags.HighlightTimers.Count > 0)
                    Gags.TickHighlightTimers();
            }           


            //  --- PLAYER CLINCH ---
            if (ModSettings.Instance.EnableClinch)
                if (Agent.Main.IsAgentCorrect() && Agent.Main.GetSkillValueFromWeaponClass() >= ModSettings.Instance.ClinchRequiredWeaponSkill)
                    PlayerClinchLogic();

            // --- PLAYER DODGE --- 
            if (ModSettings.Instance.EnableDodges)
                if (Agent.Main.IsAgentCorrect() && Agent.Main.Character.GetSkillValue(DefaultSkills.Athletics) >= ModSettings.Instance.DodgeRequiredAthletics)
                    PlayerDodgeLogic();
        }




        private void WorkingWithMS()
        {
            foreach (KeyValuePair<Agent, Agent> pair in _masterstrikeAgents.ToList<KeyValuePair<Agent, Agent>>())
            {
                Agent affector = pair.Key;
                Agent affected = pair.Value;

                CheckingCorrectnessOfAgents(affector, affected, _masterstrikeAgents);

                var MasterStrikeDistance = new Dictionary<string, (float distance, bool IsNeedUseMultiplier)>
                {
                    // Одноручное
                    ["act_affector_masterstrike_1h_sword_stun"] = (1.8f, false),
                    ["act_affector_masterstrike_1h_sword_only_1"] = (1.82f, false),
                    ["act_affector_masterstrike_1h_and_shield_just_shove"] = (1.8f, false),
                    ["act_affector_masterstrike_1h_and_shield_face_punch"] = (1.85f, false),
                    ["act_affector_masterstrike_1h_and_shield_ukol_v_plecho"] = (2.0f, false),
                    ["act_affector_masterstrike_onehanded_and_shield_2"] = (2.0f, false),
                    ["act_affector_masterstrike_1h_sword_only_2"] = (2.0f, false),
                    ["act_affector_masterstrike_1h_sword_only_3"] = (1.6f, false),
                    ["act_affector_masterstrike_1h_sword_1"] = (2.2f, true),
                    ["act_affector_masterstrike_1h_and_shield_ukol_v_plecho_2"] = (2.3f, false),
                    ["act_affector_masterstrike_onehanded_and_shield_1"] = (1.9f, true),
                    ["act_affector_masterstrike_1h_and_shield_double_stab"] = (2.0f, false),

                    // Двуручное
                    ["act_affector_masterstrike_2h_sword_1"] = (2.2f, true),
                    ["act_affector_masterstrike_2h_sword_2"] = (affected.GetCurrentAction(0).GetName() == "act_affected_masterstrike_1h_vs_2h_sword_2" ? 2.3f : 2.2f, true),
                    ["act_affector_masterstrike_2h_sword_3"] = (2.2f, false),
                    ["act_affector_masterstrike_2h_sword_4"] = (2.0f, false),
                    ["act_affector_masterstrike_2h_sword_5"] = (2.1f, true),
                    ["act_affector_masterstrike_2h_sword_6"] = (2.1f, false),
                };

                float distance;
                if (MasterStrikeDistance.TryGetValue(affector.GetCurrentAction(0).GetName(), out var dict))
                {
                    distance = dict.distance;

                    if (dict.IsNeedUseMultiplier)
                        distance *= GetWeaponLengthMultiplier(affector);
                }
                else distance = 1.5f;

                AgentExtensions.SyncAgentPositions(affected, affector, distance, updatePosition: true);               


                if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_1h_sword_stun")
                {
                    affector.PlaySoundAtActionProgress(affector.UseWeaponClass(WeaponClass.OneHandedSword) ? "sword_parry" : "metal_parry", 0.02f);

                    affected.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.53f);
                    if (affected.ActionProgress(0.53f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_1h_sword_only_1")
                {
                    affector.PlaySoundAtActionProgress("punch_block_1", 0.289f);

                    affected.PlaySoundAtActionProgress("stabbing_the_1h_sword_2", 0.415f);

                    if (affected.ActionProgress(0.415f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_1h_sword_only_2")
                {
                    affector.PlaySoundAtActionProgress(affector.UseWeaponClass(WeaponClass.OneHandedSword) ? "sword_parry" : "metal_parry", 0.158f);

                    affector.PlaySoundAtActionProgress("punch_block_1", 0.263f);

                    affected.PlaySoundAtActionProgress("MS_SOUND_light_damage", 0.42f);
                    if (affected.ActionProgress(0.42f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_1h_sword_only_3")
                {
                    affector.PlaySoundAtActionProgress("punch_block_1", 0.2f);

                    affected.PlaySoundAtActionProgress("MS_SOUND_light_damage", 0.356f);
                    if (affected.ActionProgress(0.356f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_1h_sword_1")
                {
                    affector.PlaySoundAtActionProgress(affector.UseWeaponClass(WeaponClass.OneHandedSword) ? "sword_parry" : "metal_parry", 0.2f);

                    affected.PlaySoundAtActionProgress("stabbing_the_1h_sword_2", 0.4f);
                    if (affected.ActionProgress(0.4f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_1h_and_shield_just_shove")
                {
                    affector.PlaySoundAtActionProgress(affector.UseWeaponClass(WeaponClass.OneHandedSword) ? "sword_parry" : "metal_parry", 0.154f);

                    affected.PlaySoundAtActionProgress("leather_damage_2", 0.37f);
                    if (affected.ActionProgress(0.37f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    affector.PlaySoundAtActionProgress("hand_push_1", 0.62f);
                    if (affected.ActionProgress(0.62f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_1h_and_shield_face_punch")
                {
                    affector.PlaySoundAtActionProgress(GetRandomShieldBlockSound(affector), 0.19f);

                    affected.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.308f);
                    if (affected.ActionProgress(0.308f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    affected.PlaySoundAtActionProgress("leather_damage_3", 0.477f);

                    affected.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.58f);
                    if (affected.ActionProgress(0.58f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_1h_and_shield_ukol_v_plecho")
                {
                    affector.PlaySoundAtActionProgress(GetRandomShieldBlockSound(affector), 0.32f);

                    affected.PlaySoundAtActionProgress("stabbing_the_1h_sword_1", 0.493f);
                    if (affected.ActionProgress(0.493f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_1h_and_shield_ukol_v_plecho_2")
                {
                    affector.PlaySoundAtActionProgress(GetRandomShieldBlockSound(affector), 0.356f);

                    affected.PlaySoundAtActionProgress("stabbing_the_1h_sword_1", 0.35f);
                    if (affected.ActionProgress(0.35f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_1h_sidestep_shove")
                {
                    affected.PlaySoundAtActionProgress("punch_block_1", 0.169f);

                    affected.PlaySoundAtActionProgress("leather_damage_1", 0.282f);

                    affector.PlaySoundAtActionProgress("hand_push_1", 0.545f);

                    if (affector.ActionProgress(0.545f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Stun, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_onehanded_and_shield_1")
                {
                    affector.PlaySoundAtActionProgress(GetRandomShieldBlockSound(affector), 0.4f);

                    affected.PlaySoundAtActionProgress("MS_SOUND_light_damage", 0.345f);
                    if (affected.ActionProgress(0.345f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_onehanded_and_shield_2")
                {
                    affector.PlaySoundAtActionProgress(affector.UseWeaponClass(WeaponClass.OneHandedSword) ? "sword_parry" : "metal_parry", 0.4f);

                    affected.PlaySoundAtActionProgress(ShieldBashSounds[MBRandom.RandomInt(ShieldBashSounds.Length)], 0.57f);
                    affected.PlaySoundAtActionProgress("kick_3", 0.57f);

                    if (affected.ActionProgress(0.57f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_1h_and_shield_double_stab")
                {
                    affector.PlaySoundAtActionProgress(GetRandomShieldBlockSound(affector), 0.231f);

                    affected.PlaySoundAtActionProgress("MS_SOUND_light_damage", 0.514f);
                    if (affected.ActionProgress(0.514f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    affected.PlaySoundAtActionProgress("MS_SOUND_light_damage", 0.714f);
                    if (affected.ActionProgress(0.714f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_2h_sword_1")
                {
                    affector.PlaySoundAtActionProgress("metal_parry", 0.4f);

                    if (affected.GetCurrentAction(0).GetName() == "act_affected_masterstrike_2h_sword_1")
                    {
                        affected.PlaySoundAtActionProgress("MS_SOUND_light_damage", 0.4f);
                        if (affected.ActionProgress(0.4f))
                            affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);
                    }
                    else if (affected.GetCurrentAction(0).GetName() == "act_affected_masterstrike_1h_vs_2h_sword_1")
                    {
                        affected.PlaySoundAtActionProgress("MS_SOUND_light_damage", 0.467f);
                        if (affected.ActionProgress(0.467f))
                            affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);
                    }

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_2h_sword_2")
                {
                    affector.PlaySoundAtActionProgress("metal_parry_2", 0.246f);

                    if (affected.GetCurrentAction(0).GetName() == "act_affected_masterstrike_2h_sword_2")
                    {
                        affected.PlaySoundAtActionProgress("MS_SOUND_light_damage", 0.54f);
                        if (affected.ActionProgress(0.54f))
                            affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);
                    }
                    else if (affected.GetCurrentAction(0).GetName() == "act_affected_masterstrike_1h_vs_2h_sword_2")
                    {
                        affected.PlaySoundAtActionProgress("MS_SOUND_light_damage", 0.55f);
                        if (affected.ActionProgress(0.55f))
                            affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);
                    }

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_2h_sword_3")
                {
                    affected.PlaySoundAtActionProgress("MS_SOUND_light_damage", 0.375f);
                    if (affected.ActionProgress(0.375f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_2h_sword_4")
                {
                    affector.PlaySoundAtActionProgress("metal_parry", 0.16f);

                    affected.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.42f);
                    if (affected.ActionProgress(0.42f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    affected.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.59f);
                    if (affected.ActionProgress(0.59f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_2h_sword_5")
                {
                    affector.PlaySoundAtActionProgress("metal_parry", 0.28f);

                    affected.PlaySoundAtActionProgress("MS_SOUND_light_damage", 0.525f);
                    if (affected.ActionProgress(0.525f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_masterstrike_2h_sword_6")
                {
                    affector.PlaySoundAtActionProgress("metal_parry", 0.154f);

                    affected.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.49f);
                    if (affected.ActionProgress(0.49f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    affected.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.66f);
                    if (affected.ActionProgress(0.66f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishMasterStrike(affector, affector.GetCurrentAction(0).GetName());
                }

                else continue;

                // Проверяем, что агент не был удалён из словаря при запуске stagger
                if (!affector.IsInMasterStrikeAction() || affector.ActionProgress(0.999f))
                {
                    if (_masterstrikeAgents.ContainsKey(affector))
                    {
                        affector.SetAutomaticTargetSelection(true);
                        affected.SetAutomaticTargetSelection(true);

                        if (ModSettings.Instance.EnableInvulnerabilityDuringActions)
                        {
                            affector.SetMortalityState(Agent.MortalityState.Mortal);
                            affected.SetMortalityState(Agent.MortalityState.Mortal);
                        }

                        _masterstrikeAgents.Remove(affector);
                    }
                }
            }
        }

        private void FinishMasterStrike(Agent affector, string affectorActionName)
        {
            if (_masterstrikeAgents.TryGetValue(affector, out Agent affected))
            {
                if (affector.IsAgentCorrect() && affected.IsAgentCorrect() && !affector.WieldedWeapon.IsEmpty && !affected.WieldedWeapon.IsEmpty)
                {
                    Blow blow = new Blow(affector.Index);

                    // Для анимаций всех одинаковый урон
                    float damageMultiplier = 1f + (1f - affected.Health / affected.HealthLimit) * 0.8f;
                    blow.InflictedDamage = (int)(20f * damageMultiplier);

                    Agent.UsageDirection AttackDirection;

                    if (affectorActionName == "act_affector_masterstrike_1h_sword_stun")
                    {
                        blow.BoneIndex = affected.Monster.HeadLookDirectionBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackLeft;
                        blow.VictimBodyPart = BoneBodyPartType.Head;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_1h_sword_only_1")
                    {
                        blow.BoneIndex = affected.Monster.SpineUpperBoneIndex;
                        blow.StrikeType = StrikeType.Thrust;
                        blow.DamageType = DamageTypes.Pierce;
                        AttackDirection = Agent.UsageDirection.AttackUp;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    //else if (affectorActionName == "act_affector_masterstrike_1h_sword_only_2")
                    //{
                    //    blow.BoneIndex = affected.Monster.SpineUpperBoneIndex;
                    //    blow.StrikeType = StrikeType.Thrust;
                    //    blow.DamageType = DamageTypes.Pierce;
                    //    AttackDirection = Agent.UsageDirection.AttackUp;
                    //    blow.VictimBodyPart = BoneBodyPartType.Chest;
                    //    blow.InflictedDamage = 10;

                    //    blow.BlowFlag = BlowFlags.NoSound;
                    //}
                    else if (affectorActionName == "act_affector_masterstrike_1h_sword_only_3")
                    {
                        blow.BoneIndex = affected.Monster.SpineUpperBoneIndex;
                        blow.StrikeType = StrikeType.Thrust;
                        blow.DamageType = DamageTypes.Pierce;
                        AttackDirection = Agent.UsageDirection.AttackUp;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_1h_and_shield_just_shove")
                    {
                        blow.BoneIndex = affected.Monster.SpineUpperBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackLeft;
                        blow.VictimBodyPart = BoneBodyPartType.Head;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_1h_and_shield_face_punch")
                    {
                        blow.BoneIndex = affected.Monster.HeadLookDirectionBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackUp;
                        blow.VictimBodyPart = BoneBodyPartType.Head;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_1h_and_shield_double_stab")
                    {
                        blow.BoneIndex = affected.Monster.SpineUpperBoneIndex;
                        blow.StrikeType = StrikeType.Thrust;
                        blow.DamageType = DamageTypes.Pierce;
                        AttackDirection = Agent.UsageDirection.AttackLeft;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_1h_and_shield_ukol_v_plecho")
                    {
                        blow.BoneIndex = affected.Monster.RightUpperArmBoneIndex;
                        blow.StrikeType = StrikeType.Thrust;
                        blow.DamageType = DamageTypes.Pierce;
                        AttackDirection = Agent.UsageDirection.AttackDown;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_1h_and_shield_ukol_v_plecho_2")
                    {
                        blow.BoneIndex = affected.Monster.RightUpperArmBoneIndex;
                        blow.StrikeType = StrikeType.Thrust;
                        blow.DamageType = DamageTypes.Pierce;
                        AttackDirection = Agent.UsageDirection.AttackDown;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    //else if (affectorActionName == "act_affector_masterstrike_1h_sidestep_shove")
                    //{
                    //    blow.BoneIndex = affected.Monster.SpineUpperBoneIndex;
                    //    blow.StrikeType = StrikeType.Swing;
                    //    blow.DamageType = DamageTypes.Blunt;
                    //    AttackDirection = Agent.UsageDirection.AttackDown;
                    //    blow.VictimBodyPart = BoneBodyPartType.ShoulderRight;
                    //    blow.InflictedDamage = 1;

                    //    blow.BlowFlag = BlowFlags.NoSound;
                    //}
                    else if (affectorActionName == "act_affector_masterstrike_onehanded_and_shield_1")
                    {
                        blow.BoneIndex = affected.Monster.SpineLowerBoneIndex;
                        blow.StrikeType = StrikeType.Thrust;
                        blow.DamageType = DamageTypes.Pierce;
                        AttackDirection = Agent.UsageDirection.AttackDown;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_onehanded_and_shield_2")
                    {
                        blow.BoneIndex = affected.Monster.RightUpperArmBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackRight;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_1h_sword_1")
                    {
                        blow.BoneIndex = affected.Monster.SpineUpperBoneIndex;
                        blow.StrikeType = StrikeType.Thrust;
                        blow.DamageType = DamageTypes.Pierce;
                        AttackDirection = Agent.UsageDirection.AttackUp;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_2h_sword_1")
                    {
                        blow.BoneIndex = affected.Monster.HeadLookDirectionBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackUp;
                        blow.VictimBodyPart = BoneBodyPartType.Head;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_2h_sword_2")
                    {
                        blow.BoneIndex = affected.Monster.SpineLowerBoneIndex;
                        blow.StrikeType = StrikeType.Thrust;
                        blow.DamageType = DamageTypes.Pierce;
                        AttackDirection = Agent.UsageDirection.AttackDown;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_2h_sword_3")
                    {
                        blow.BoneIndex = affected.Monster.SpineUpperBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Cut;
                        AttackDirection = Agent.UsageDirection.AttackRight;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_2h_sword_4")
                    {
                        blow.BoneIndex = affected.Monster.SpineLowerBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackDown;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_2h_sword_5")
                    {
                        blow.BoneIndex = affected.Monster.SpineLowerBoneIndex;
                        blow.StrikeType = StrikeType.Thrust;
                        blow.DamageType = DamageTypes.Pierce;
                        AttackDirection = Agent.UsageDirection.AttackUp;
                        blow.VictimBodyPart = BoneBodyPartType.Abdomen;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_masterstrike_2h_sword_6")
                    {
                        blow.BoneIndex = affected.Monster.HeadLookDirectionBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackUp;
                        blow.VictimBodyPart = BoneBodyPartType.Head;
                        
                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else
                    {
                        return;
                    }

                    AttackCollisionData attackCollisionData = AttackCollisionData.GetAttackCollisionDataForDebugPurpose
                    (
                        false, false, false, true, false, false, false, false, false, false, false, false,
                        CombatCollisionResult.StrikeAgent, -1, 0, 2, blow.BoneIndex, blow.VictimBodyPart, -1,
                        AttackDirection, -1, CombatHitResultFlags.NormalHit, 0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, Vec3.Up,
                        blow.Direction, blow.GlobalPosition, Vec3.Zero, Vec3.Zero, affected.Position, Vec3.Up
                    );


                    // регаем урон от MS
                    affected.RegisterBlow(blow, attackCollisionData);

                    affector.SetAutomaticTargetSelection(true);
                    affected.SetAutomaticTargetSelection(true);

                    // инвок при завершении MS
                    OnMasterStrikeFinished?.Invoke(affector, affected, attackCollisionData);

                    if (!_streakAgents.ContainsKey(affector))
                        _streakAgents[affector] = 0;

                    // увеличиваем счётчик MS у агента
                    _streakAgents[affector]++;

                    if (_streakAgents[affector] >= 3)
                    {
                        _streakAgents[affector] = 0;

                        // ЗАПУСК БАФАА
                        ApplyAdrenalineBuff(affector);
                    }

                    if ((ModuleHelper.IsModuleActive("MasterStrikes.RBM") || ModSettings.Instance.EnableStaggerActions) && LaunchStaggerAction() && affected.Health > 0)
                    {
                        float staggerChance = MathF.Clamp(blow.InflictedDamage * 2.3f, 5f, 60f);

                        if (staggerChance >= MBRandom.RandomInt(1, 100))
                        {
                            var staggerAction = AttackDirection switch
                            {
                                Agent.UsageDirection.AttackLeft => ActionIndexCache.act_stagger_right,// удар слева -> шатается вправо
                                Agent.UsageDirection.AttackRight => ActionIndexCache.act_stagger_left,// удар справа -> шатается влево
                                Agent.UsageDirection.AttackUp or Agent.UsageDirection.AttackDown => ActionIndexCache.act_stagger_backward,// удар сверху или колющий -> шатается назад     
                                _ => ActionIndexCache.act_stagger_backward,
                            };

                            // Gags.HighlightAgent(affected, Gags.Action.Other);

                            if (ModSettings.Instance.EnableInvulnerabilityDuringActions)
                            {
                                affector.SetMortalityState(Agent.MortalityState.Mortal);
                                affected.SetMortalityState(Agent.MortalityState.Mortal);
                            }

                            // Чтобы удалялся перед стаггером, раз мы анимацию прерываем
                            _masterstrikeAgents.Remove(affector);

                            //InformationManager.DisplayMessage(new InformationMessage("СТАГГЕР!!! (от MS)"));

                            affected.SetActionChannel(0, staggerAction, true, 0UL, 0f, 0.94f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                            affected.MakeVoice(SkinVoiceManager.VoiceType.Stun, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);
                        }
                    }
                }
                else
                {
                    affector.SetAutomaticTargetSelection(true);
                    affected.SetAutomaticTargetSelection(true);

                    if (ModSettings.Instance.EnableInvulnerabilityDuringActions)
                    {
                        affector.SetMortalityState(Agent.MortalityState.Mortal);
                        affected.SetMortalityState(Agent.MortalityState.Mortal);
                    }

                    _masterstrikeAgents.Remove(affector);
                }
            }
        }




        private void WorkingWithCL()
        {
            foreach (KeyValuePair<Agent, Agent> pair in _clinchAgents.ToList())
            {
                Agent affector = pair.Key;
                Agent affected = pair.Value;

                CheckingCorrectnessOfAgents(affector, affected, _clinchAgents);

                var ClinchDistance = new Dictionary<string, float>
                {
                    // Одноручное
                    ["act_affector_1h_clinch_1"] = 1.3f,
                    ["act_affector_1h_clinch_2"] = 1.3f,
                    ["act_affector_1h_clinch_3"] = 1.3f,

                    // Двуручное
                    ["act_affector_2h_clinch_1"] = 1.2f,
                    ["act_affector_2h_clinch_2"] = 1.4f

                };


                float distance;
                if (ClinchDistance.TryGetValue(affector.GetCurrentAction(0).GetName(), out var d)) distance = d;
                else distance = 1.2f;

                AgentExtensions.SyncAgentPositions(affected, affector, distance, updatePosition: true);


                if (affector.GetCurrentAction(0).GetName() == "act_affector_2h_clinch_1")
                {
                    if (affected.ActionProgress(0.019f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.02f))
                        affector.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    affector.PlaySoundAtActionProgress("metal_parry", 0.02f);

                    affector.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.51f);

                    if (affected.ActionProgress(0.538f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);


                    if (affector.ActionProgress(0.98f))
                        FinishClinch(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_2h_clinch_2")
                {
                    if (affected.ActionProgress(0.019f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.02f))
                        affector.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    affector.PlaySoundAtActionProgress("metal_parry", 0.02f);

                    affected.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.4f);

                    if (affected.ActionProgress(0.4f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.98f))
                        FinishClinch(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_1h_clinch_1")
                {
                    if (affected.ActionProgress(0.019f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.02f))
                        affector.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    affector.PlaySoundAtActionProgress(affector.UseWeaponClass(WeaponClass.OneHandedSword) ? "sword_parry" : "metal_parry", 0.02f);

                    affected.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.461f);

                    if (affected.ActionProgress(0.461f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);


                    if (affector.ActionProgress(0.98f))
                        FinishClinch(affector, affector.GetCurrentAction(0).GetName());
                }

                else if (affector.GetCurrentAction(0).GetName() == "act_affector_1h_clinch_2")
                {
                    if (affected.ActionProgress(0.019f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.02f))
                        affector.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    affector.PlaySoundAtActionProgress(affector.UseWeaponClass(WeaponClass.OneHandedSword) ? "sword_parry" : "metal_parry", 0.02f);

                    affected.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.383f);
                    if (affected.ActionProgress(0.383f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);


                    if (affector.ActionProgress(0.98f))
                        FinishClinch(affector, affector.GetCurrentAction(0).GetName());
                }
                
                else if (affector.GetCurrentAction(0).GetName() == "act_affector_1h_clinch_3")
                {
                    if (affected.ActionProgress(0.019f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    if (affector.ActionProgress(0.02f))
                        affector.MakeVoice(SkinVoiceManager.VoiceType.Grunt, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    affector.PlaySoundAtActionProgress(affector.UseWeaponClass(WeaponClass.OneHandedSword) ? "sword_parry" : "metal_parry", 0.02f);

                    affected.PlaySoundAtActionProgress(PunchSounds[MBRandom.RandomInt(PunchSounds.Length)], 0.417f);
                    if (affected.ActionProgress(0.417f))
                        affected.MakeVoice(SkinVoiceManager.VoiceType.Pain, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);


                    if (affector.ActionProgress(0.98f))
                        FinishClinch(affector, affector.GetCurrentAction(0).GetName());
                }

                else continue;

                // Проверяем, что агент не был удалён из словаря при запуске stagger
                if (!affector.IsInClinchAction() || affector.ActionProgress(0.999f))
                {
                    if (_clinchAgents.ContainsKey(affector))
                    {
                        affector.SetAutomaticTargetSelection(true);
                        affected.SetAutomaticTargetSelection(true);

                        if (ModSettings.Instance.EnableInvulnerabilityDuringActions)
                        {
                            affector.SetMortalityState(Agent.MortalityState.Mortal);
                            affected.SetMortalityState(Agent.MortalityState.Mortal);
                        }

                        _clinchAgents.Remove(affector);
                    }                        
                }
            }
        }

        private void FinishClinch(Agent affector, string affectorActionName)
        {
            if (_clinchAgents.TryGetValue(affector, out Agent affected))
            {
                if (affector.IsAgentCorrect() && affected.IsAgentCorrect() && !affector.WieldedWeapon.IsEmpty && !affected.WieldedWeapon.IsEmpty)
                {
                    Blow blow = new Blow(affector.Index);

                    // Для анимаций всех одинаковый урон
                    float damageMultiplier = 1f + (1f - affected.Health / affected.HealthLimit) * 0.8f;
                    blow.InflictedDamage = (int)(10f * damageMultiplier);

                    Agent.UsageDirection AttackDirection;

                    if (affectorActionName == "act_affector_2h_clinch_1")
                    {
                        blow.BoneIndex = affected.Monster.HeadLookDirectionBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackRight;
                        blow.VictimBodyPart = BoneBodyPartType.Head;

                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_2h_clinch_2")
                    {
                        blow.BoneIndex = affected.Monster.HeadLookDirectionBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackLeft;
                        blow.VictimBodyPart = BoneBodyPartType.Head;

                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_1h_clinch_1")
                    {
                        blow.BoneIndex = affected.Monster.HeadLookDirectionBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackUp;
                        blow.VictimBodyPart = BoneBodyPartType.Head;

                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_1h_clinch_2")
                    {
                        blow.BoneIndex = affected.Monster.HeadLookDirectionBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackLeft;
                        blow.VictimBodyPart = BoneBodyPartType.Head;

                        blow.BlowFlag = BlowFlags.NoSound;
                    }
                    else if (affectorActionName == "act_affector_1h_clinch_3")
                    {
                        blow.BoneIndex = affected.Monster.HeadLookDirectionBoneIndex;
                        blow.StrikeType = StrikeType.Swing;
                        blow.DamageType = DamageTypes.Blunt;
                        AttackDirection = Agent.UsageDirection.AttackRight;
                        blow.VictimBodyPart = BoneBodyPartType.Head;

                        blow.BlowFlag = BlowFlags.NoSound;
                    }

                    else return;
                    

                    AttackCollisionData attackCollisionData = AttackCollisionData.GetAttackCollisionDataForDebugPurpose
                    (
                        false, false, false, true, false, false, false, false, false, false, false, false,
                        CombatCollisionResult.StrikeAgent, -1, 0, 2, blow.BoneIndex, blow.VictimBodyPart, -1,
                        AttackDirection, -1, CombatHitResultFlags.NormalHit, 0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f, Vec3.Up,
                        blow.Direction, blow.GlobalPosition, Vec3.Zero, Vec3.Zero, affected.Position, Vec3.Up
                    );
                    
                    affected.RegisterBlow(blow, attackCollisionData);                    

                    affector.SetAutomaticTargetSelection(true);
                    affected.SetAutomaticTargetSelection(true);

                    OnClinchFinished?.Invoke(affector, affected, attackCollisionData);

                    if (!_streakAgents.ContainsKey(affector))
                        _streakAgents[affector] = 0;

                    // увеличиваем счётчик MS у агента
                    _streakAgents[affector]++;

                    if (_streakAgents[affector] >= 3)
                    {
                        _streakAgents[affector] = 0;

                        // ЗАПУСК БАФАА
                        ApplyAdrenalineBuff(affector);
                    }

                    if ((ModuleHelper.IsModuleActive("MasterStrikes.RBM") || ModSettings.Instance.EnableStaggerActions) && LaunchStaggerAction() && affected.Health > 0)
                    {
                        // Шанс стаггера зависит от нанесённого урона
                        float staggerChance = MathF.Clamp(blow.InflictedDamage * 2f, 5f, 60f);

                        if (staggerChance >= MBRandom.RandomInt(1, 100))
                        {
                            var staggerAction = AttackDirection switch
                            {
                                Agent.UsageDirection.AttackLeft => ActionIndexCache.act_stagger_right,// удар слева -> шатается вправо
                                Agent.UsageDirection.AttackRight => ActionIndexCache.act_stagger_left,// удар справа -> шатается влево
                                Agent.UsageDirection.AttackUp or Agent.UsageDirection.AttackDown => ActionIndexCache.act_stagger_backward,// удар сверху или колющий -> шатается назад     
                                _ => ActionIndexCache.act_stagger_backward,
                            };


                            // Gags.HighlightAgent(affected, Gags.Action.Other);

                            if (ModSettings.Instance.EnableInvulnerabilityDuringActions)
                            {
                                affector.SetMortalityState(Agent.MortalityState.Mortal);
                                affected.SetMortalityState(Agent.MortalityState.Mortal);
                            }

                            // Чтобы удалялся перед стаггером, раз мы анимацию прерываем
                            _clinchAgents.Remove(affector);

                            //InformationManager.DisplayMessage(new InformationMessage("СТАГГЕР!!! (от клинча)"));

                            affected.SetActionChannel(0, staggerAction, true, 0UL, 0f, 0.94f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                            affected.MakeVoice(SkinVoiceManager.VoiceType.Stun, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);
                        }
                    }
                }
                else
                {
                    affector.SetAutomaticTargetSelection(true);
                    affected.SetAutomaticTargetSelection(true);

                    if (ModSettings.Instance.EnableInvulnerabilityDuringActions)
                    {
                        affector.SetMortalityState(Agent.MortalityState.Mortal);
                        affected.SetMortalityState(Agent.MortalityState.Mortal);
                    }

                    _clinchAgents.Remove(affector);
                }
            }
        }


        public static bool LaunchStaggerAction() => true;

        private void ApplyAdrenalineBuff(Agent agent)
        {
            if (!agent.IsAgentCorrect()) return;

            // Отхил HP
            agent.Health = Math.Min(agent.HealthLimit, agent.Health + agent.HealthLimit * 0.15f);

            // + Мораль
            agent.SetMorale(Math.Min(100f, agent.GetMorale() + 30f));

            // Орёт типа крутой
            agent.MakeVoice(SkinVoiceManager.VoiceType.Yell, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);


            // Повышение агрессивности AI
            if (!agent.IsMainAgent) agent.AgentDrivenProperties.AIAttackOnDecideChance = 0.9f;

            // Повышение морали ближайшим союзникам
            if (agent.Team != null)
            {
                MBList<Agent> nearbyAllies = new MBList<Agent>();
                Mission.Current.GetNearbyAllyAgents(agent.Position.AsVec2, 5f, agent.Team, nearbyAllies);
                foreach (Agent ally in nearbyAllies)
                {
                    if (ally.IsAgentCorrect() && ally != agent)
                    {
                        ally.SetMorale(Math.Min(100f, ally.GetMorale() + 10f));
                        ally.MakeVoice(SkinVoiceManager.VoiceType.Yell, SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);
                    }

                }
            }


            // Понижение морали ближайшим врагам
            if (agent.Team != null)
            {
                MBList<Agent> nearbyEnemies = new MBList<Agent>();
                Mission.Current.GetNearbyEnemyAgents(agent.Position.AsVec2, 5f, agent.Team, nearbyEnemies);
                foreach (Agent enemy in nearbyEnemies)
                {
                    if (enemy.IsAgentCorrect())
                        enemy.SetMorale(Math.Max(0f, enemy.GetMorale() - 8f));
                }
            }


            // Отладочное сообщение
            if (ModSettings.Instance.AdrenalineBuffMessages)
            {
                string messageText;
                Color messageColor;

                if (agent.IsMainAgent)
                {
                    messageText = new TextObject("{=EsxqIJHi}You have demonstrated masterful weapon skill! Thanks to you, the morale has risen. Warriors fight more fiercely.").ToString();
                    messageColor = Color.ConvertStringToColor("#34c924ff");
                }
                else if (agent.Team == Agent.Main?.Team)
                {
                    string warriorName = agent.IsHero ? agent.Name : new TextObject("{=dPenLpXe}A warrior").ToString();
                    messageText = new TextObject("{=YMytAWya}{WARRIOR} from your team has demonstrated masterful weapon skill! Morale has risen. Warriors fight more fiercely.").SetTextVariable("WARRIOR", warriorName).ToString();
                    messageColor = Color.ConvertStringToColor("#34c924ff");
                }
                else
                {
                    string warriorName = agent.IsHero ? agent.Name : new TextObject("{=dPenLpXe}A warrior").ToString();
                    messageText = new TextObject("{=ORhJKiDL}{WARRIOR} from enemy team has demonstrated masterful weapon skill! Their morale has risen. Warriors fight more fiercely.").SetTextVariable("WARRIOR", warriorName).ToString();
                    messageColor = Color.ConvertStringToColor("#b22222ff");
                }

                InformationManager.DisplayMessage(new InformationMessage(messageText, messageColor));
            }
        }       


        private void CheckingCorrectnessOfAgents(Agent affector, Agent affected, Dictionary<Agent, Agent> agentsDict)
        {
            if (!affector.IsInMasterStrikeAction() && !affector.IsInClinchAction())
            {
                affector.SetAutomaticTargetSelection(true);
                affected.SetAutomaticTargetSelection(true);
                agentsDict.Remove(affector);
            }

            if (!affector.IsAgentCorrect() || !affected.IsAgentCorrect())
            {
                if (!affector.IsAgentCorrect())
                {
                    affected.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                    affected.SetAutomaticTargetSelection(true);

                    if (ModSettings.Instance.EnableInvulnerabilityDuringActions)
                    {
                        if (affected.CurrentMortalityState == Agent.MortalityState.Invulnerable)
                        {
                            affected.SetMortalityState(Agent.MortalityState.Mortal);
                        }
                    }

                    agentsDict.Remove(affector);
                }
                else if (!affected.IsAgentCorrect())
                {
                    affector.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                    affector.SetAutomaticTargetSelection(true);

                    if (ModSettings.Instance.EnableInvulnerabilityDuringActions)
                    {
                        if (affector.CurrentMortalityState == Agent.MortalityState.Invulnerable)
                        {
                            affector.SetMortalityState(Agent.MortalityState.Mortal);
                        }
                    }

                    agentsDict.Remove(affector);
                }
            }

            if (affector.WieldedWeapon.IsEmpty || affected.WieldedWeapon.IsEmpty ||
                affector.WieldedWeapon.CurrentUsageItem?.WeaponClass == WeaponClass.Undefined ||
                affected.WieldedWeapon.CurrentUsageItem?.WeaponClass == WeaponClass.Undefined)
            {
                affector.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                affected.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);

                affector.SetAutomaticTargetSelection(true);
                affected.SetAutomaticTargetSelection(true);

                if (ModSettings.Instance.EnableInvulnerabilityDuringActions)
                {
                    if (affector.CurrentMortalityState == Agent.MortalityState.Invulnerable)
                    {
                        affector.SetMortalityState(Agent.MortalityState.Mortal);
                    }
                    if (affected.CurrentMortalityState == Agent.MortalityState.Invulnerable)
                    {
                        affected.SetMortalityState(Agent.MortalityState.Mortal);
                    }
                }

                agentsDict.Remove(affector);
            }
        }


        private void ResetMSAgentsAfterExternalDamage(Agent affected)
        {
            //Gags.HighlightAgent(affected, Gags.Action.Other);


            // ЕСЛИ МАСТЕРСКИЙ УДАР
            if (_masterstrikeAgents.TryGetValue(affected, out Agent value))
            {
                affected.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                value.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);                
                
                affected.SetAutomaticTargetSelection(true);
                value.SetAutomaticTargetSelection(true);

                _masterstrikeAgents.Remove(affected);
            }

            else if (_masterstrikeAgents.TryGetKeyByValue(affected, out Agent key))
            {
                key.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                affected.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);

                key.SetAutomaticTargetSelection(true);
                affected.SetAutomaticTargetSelection(true);

                _masterstrikeAgents.Remove(key);
            }

            // ЕСЛИ КЛИНЧ
            else if (_clinchAgents.TryGetValue(affected, out Agent clinchValue))
            {
                affected.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                clinchValue.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);

                affected.SetAutomaticTargetSelection(true);
                clinchValue.SetAutomaticTargetSelection(true);

                _clinchAgents.Remove(affected);
            }

            else if (_clinchAgents.TryGetKeyByValue(affected, out Agent clinchKey))
            {
                clinchKey.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                affected.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);

                clinchKey.SetAutomaticTargetSelection(true);
                affected.SetAutomaticTargetSelection(true);

                _clinchAgents.Remove(clinchKey);
            }
        }

        private void HandleStrandedInvulnerableAgents()
        {
            foreach (Agent agent in Mission.Current.Agents)
            {
                if (!agent.IsAgentCorrect()) continue;

                if (!agent.IsInMasterStrikeAction() && !agent.IsInClinchAction())
                {
                    if (agent.CurrentMortalityState == Agent.MortalityState.Invulnerable)
                    {
                        agent.SetMortalityState(Agent.MortalityState.Mortal);
                        agent.SetAutomaticTargetSelection(true);
                    }
                }
            }
        }

        private void HandleNavalBattleAgents()
        {
            foreach (Agent agent in Mission.Current.Agents)
            {
                if (!agent.IsAgentCorrect()) continue;

                if (!agent.IsOnLand() || agent.IsInWater())
                {
                    if (agent.IsInMasterStrikeAction() || agent.IsInClinchAction())
                        agent.SetActionChannel(0, ActionIndexCache.act_none, false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);

                    agent.SetMortalityState(Agent.MortalityState.Mortal);
                }
                else if (!agent.IsInMasterStrikeAction() && !agent.IsInClinchAction())
                {
                    if (agent.CurrentMortalityState == Agent.MortalityState.Invulnerable)
                    {
                        agent.SetMortalityState(Agent.MortalityState.Mortal);
                        agent.SetAutomaticTargetSelection(true);
                    }
                }
            }
        }

        private string GetRandomShieldBlockSound(Agent agent)
        {
            var sounds = (agent.WieldedOffhandWeaponIsMetalShield() ? MetalShieldBlockSounds : WoodShieldBlockSounds);
            return sounds[MBRandom.RandomInt(sounds.Length)];
        }

        private float GetWeaponLengthMultiplier(Agent agent) => agent.GetWeaponLength() != 0f && agent.GetWeaponLength() >= 120f ? (1.0f + (agent.GetWeaponLength() - 120f) / 500f) : 1.0f;
        


        //// ДЛЯ ГЕЙМПАДА ////
        private static bool IsMSKeyPressed()
        {
            if (MSKey.KeyboardKey.InputKey.IsDown()) return true;

            if (Input.IsControllerConnected && ModSettings.Instance.MSControllerKey.SelectedValue != ControllerKey.Invalid)
            {
                InputKey controllerKey = (InputKey)(int)ModSettings.Instance.MSControllerKey.SelectedValue;
                return controllerKey.IsDown();
            }

            return false;
        }

        private static bool IsClinchKeyPressed(bool isDownMethod = true)
        {
            if (isDownMethod)
            {
                if (ClinchKey.KeyboardKey.InputKey.IsDown()) return true;

                if (Input.IsControllerConnected && ModSettings.Instance.ClinchControllerKey.SelectedValue != ControllerKey.Invalid)
                {
                    InputKey controllerKey = (InputKey)(int)ModSettings.Instance.ClinchControllerKey.SelectedValue;
                    return controllerKey.IsDown();
                }
            }
            else
            {
                if (ClinchKey.KeyboardKey.InputKey.IsPressed()) return true;

                if (Input.IsControllerConnected && ModSettings.Instance.ClinchControllerKey.SelectedValue != ControllerKey.Invalid)
                {
                    InputKey controllerKey = (InputKey)(int)ModSettings.Instance.ClinchControllerKey.SelectedValue;
                    return controllerKey.IsPressed();
                }
            }

                return false;
        }

        private static bool IsDodgeKeyPressed()
        {
            if (DodgeKey.KeyboardKey.InputKey.IsDown()) return true;

            if (Input.IsControllerConnected && ModSettings.Instance.DodgeControllerKey.SelectedValue != ControllerKey.Invalid)
            {
                InputKey controllerKey = (InputKey)(int)ModSettings.Instance.DodgeControllerKey.SelectedValue;
                return controllerKey.IsDown();
            }

            return false;
        }                
        //////////////////////


        private bool CanDodge = true;
        private void PlayerDodgeLogic()
        {
            if (!CanDodge) return;

            if (!Agent.Main.IsAgentCorrect()) return;
            if (Agent.Main.IsDodging()) return;

            if (Agent.Main.MountAgent != null) return;

            if (Agent.Main.IsInMasterStrikeAction() || Agent.Main.IsInClinchAction() || Agent.Main.IsStaggered()) return;
                        
            if (Agent.Main.IsAttacking()) return;


            bool isTwoHandedSword = Agent.Main.UseWeaponClass(WeaponClass.TwoHandedSword);
            bool isOneHanded = Agent.Main.HasOneHandedWeapon() || Agent.Main.UseWeaponClass(WeaponClass.Javelin) || Agent.Main.UseWeaponClass(WeaponClass.ThrowingAxe) 
                           || Agent.Main.WieldedWeapon.IsEmpty || Agent.Main.UseWeaponClass(WeaponClass.ThrowingKnife) || Agent.Main.UseWeaponClass(WeaponClass.Bow);
            bool isOtherTwoHanded = Agent.Main.HasTwoHandedWeapon() == true && !Agent.Main.UseWeaponClass(WeaponClass.TwoHandedSword) || Agent.Main.UseWeaponClass(WeaponClass.Crossbow);


            bool left = InputKey.A.IsDown() || InputKey.ControllerLStickLeft.IsDown();
            bool right = InputKey.D.IsDown() || InputKey.ControllerLStickRight.IsDown();
            bool back = InputKey.S.IsDown() || InputKey.ControllerLStickDown.IsDown();

            // -- ДОДЖ -- 
            if (IsDodgeKeyPressed() && (left || right || back))
            {
                string dodgeAnim = null;

                if (right)
                    dodgeAnim = (isTwoHandedSword) ? "act_2h_right_dodge" : (isOtherTwoHanded) ? "act_other_2h_right_dodge" : (isOneHanded) ? "act_1h_right_dodge" : null;

                else if (left)
                    dodgeAnim = (isTwoHandedSword) ? "act_2h_left_dodge" : (isOtherTwoHanded) ? "act_other_2h_left_dodge" : (isOneHanded) ? "act_1h_left_dodge" : null;

                else if (back)
                    dodgeAnim = (isTwoHandedSword) ? "act_2h_back_dodge" : (isOtherTwoHanded) ? "act_other_2h_back_dodge" : (isOneHanded) ? "act_1h_back_dodge" : null;

                if (dodgeAnim == null) return;

                

                if (OnCanDodge != null && !OnCanDodge(Agent.Main)) return;                
                OnDodgeStarted?.Invoke(Agent.Main);

                Agent.Main.SetActionChannel(0, ActionIndexCache.Create(dodgeAnim), false, 0UL, 0f, AgentExtensions.CalculateActionSpeed(Agent.Main, AgentExtensions.MasterStrikesAction.Dodge), -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                Agent.Main.MakeVoice(SkinVoiceManager.VoiceType.Jump, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);
            }

            // -- ДЛЯ ТЕСТОВ -- 
            //else if (InputKey.X1MouseButton.IsDown() && InputKey.X2MouseButton.IsDown())
            //{
            //    Agent.Main.SetActionChannel(0, ActionIndexCache.Create("act_test_ik"), false, 0UL, 0f, 1f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
            //    //InformationManager.DisplayMessage(new InformationMessage($"RBM: {)}"));
            //}

        }


        private float _lastPlayerClinchTime;
        private int _quickClinchCount;
        
        private void PlayerClinchLogic()
        {
            if (!Agent.Main.IsAgentCorrect() || Agent.Main.MountAgent != null) return;
            if (Agent.Main.IsBusy() || Agent.Main.IsInAnyMSAction() || Agent.Main.IsStaggered()) return;
            if (Agent.Main.WieldedWeapon.IsEmpty) return;

            // например текущее время 10, а клинч был на 8, итого - 2 секунды назад
            float timeSinceLastClinch = Mission.Current.CurrentTime - _lastPlayerClinchTime;

            // Если игрок слишком часто клинчит — пауза 5 сек
            if (_quickClinchCount >= 3 && timeSinceLastClinch < 5f)
            {
                if (IsClinchKeyPressed(isDownMethod: false)) // isPressed использую
                {
                    InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=AWhmCWzl}Too often! Wait {SEC} seconds.").SetTextVariable("SEC", MathF.Ceiling(5f - timeSinceLastClinch)).ToString(), Color.ConvertStringToColor("#ff6b4aff")));
                }
                return;
            }

            // Если игрок использует клинч по кулдауну (4-5 сек) — никакой паузы
            if (timeSinceLastClinch >= 4f && timeSinceLastClinch <= 5f)
            {
                _quickClinchCount = 0; // сброс счётчика
            }

            Agent enemy = Agent.Main.GetNearestEnemyForClinch(); // уже есть проверка на MountAgent + оружие IsEmpty
            if (!enemy.IsAgentCorrect()) return;

            if (Agent.Main.Position.Distance(enemy.Position) <= 1.49f)
            {
                if (IsClinchKeyPressed())
                {
                    if (OnCanStartClinch != null && !OnCanStartClinch(Agent.Main, enemy)) return;

                    _clinchAgents.Add(Agent.Main, enemy);
                    new ClinchLogic(Agent.Main, enemy);

                    // Обновляем таймеры
                    _lastPlayerClinchTime = Mission.Current.CurrentTime;

                    // Если клинч был быстрым (меньше 2,5 сек с прошлого) — увеличиваем счётчик
                    if (timeSinceLastClinch < 2.5f)
                        _quickClinchCount++;
                    else
                        _quickClinchCount = 1;
                }
            }
        }


    }
}