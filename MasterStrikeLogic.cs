using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace MasterStrikes
{
    public class MasterStrikeLogic
    {
        // affected парирует и хуярит affector
        // агенты поэтому и меняются местами       

        // В УСЛОВИЯХ НА ЗАНОС АНИМАЦИЙ (1h) В ValidActions
        // ПРОВЕРЯЕМ ТОЛЬКО AFFECTOR, ПОТОМУ ЧТО ОН ОБЯЗАН СОБЛЮСТИ УСЛОВИЕ, А AFFECTED — НЕТ.        
        // НАПРИМЕР В masterstrike_1h_sword_only_1:
        // — AFFECTOR ОБЯЗАН БЫТЬ БЕЗ ЩИТА
        // — AFFECTED МОЖЕТ БЫТЬ С ЩИТОМ ИЛИ БЕЗ


        // Из главного класса:    affectedAgent        affectorAgent (жертва)
        public MasterStrikeLogic(Agent affectorAgent, Agent affectedAgent)
        {
            
            string act_affected_masterstrike_1h_sword_only_1 = !affectedAgent.HasShieldInHand() ? "act_affected_masterstrike_1h_sword_only_1" : "act_affected_masterstrike_1h_sword_only_1_shield_variation";
            string act_affected_masterstrike_1h_sword_only_3 = !affectedAgent.HasShieldInHand() ? "act_affected_masterstrike_1h_sword_only_3" : "act_affected_masterstrike_1h_sword_only_3_shield_variation";
            string act_affected_masterstrike_1h_sword_stun = !affectedAgent.HasShieldInHand() ? "act_affected_masterstrike_1h_sword_stun" : "act_affected_masterstrike_1h_sword_stun_shield_variation";


            var ValidActions = new HashSet<(string affected, string affector, float distance)>();

            // --- АНИМАЦИИ С ОДНОРУЧНЫМ ОРУЖИЕМ ---
            if (affectorAgent.HasOneHandedWeapon() && affectedAgent.HasOneHandedWeapon())
            {
                // --- AFFECTOR БЕЗ ЩИТА ---
                if (!affectorAgent.HasShieldInHand() && !affectorAgent.UseWeaponClass(WeaponClass.Banner, isOffHand: true))
                {
                    // Уникальные анимации только для агентов с мечом
                    //if (affectorHasSword)
                    //{                        
                    //    ValidActions.Add(("act_affected_masterstrike_1h_sword_only_2", "act_affector_masterstrike_1h_sword_only_2", 2f));                        
                    //}

                    // Анимации для любого типа одноручного оружия
                    ValidActions.Add((act_affected_masterstrike_1h_sword_only_1, "act_affector_masterstrike_1h_sword_only_1", 1.7f));
                    ValidActions.Add((act_affected_masterstrike_1h_sword_only_3, "act_affector_masterstrike_1h_sword_only_3", 1.6f));
                    //ValidActions.Add(("act_affected_masterstrike_1h_sidestep_shove", "act_affector_masterstrike_1h_sidestep_shove", 1.5f));
                    ValidActions.Add((act_affected_masterstrike_1h_sword_stun, "act_affector_masterstrike_1h_sword_stun", 1.8f));
                    ValidActions.Add(("act_affected_masterstrike_1h_sword_1", "act_affector_masterstrike_1h_sword_1", 2.2f));
                }

                // --- AFFECTOR С ЩИТОМ ---
                if (affectorAgent.HasShieldInHand() && affectorAgent.GetWeaponLength() <= 160)
                {
                    ValidActions.Add(("act_affected_masterstrike_1h_and_shield_ukol_v_plecho", "act_affector_masterstrike_1h_and_shield_ukol_v_plecho", 2.0f));
                    ValidActions.Add(("act_affected_masterstrike_1h_and_shield_ukol_v_plecho_2", "act_affector_masterstrike_1h_and_shield_ukol_v_plecho_2", 2.3f));
                    ValidActions.Add(("act_affected_masterstrike_1h_and_shield_double_stab", "act_affector_masterstrike_1h_and_shield_double_stab", 2.0f));
                    ValidActions.Add(("act_affected_masterstrike_1h_and_shield_just_shove", "act_affector_masterstrike_1h_and_shield_just_shove", 1.8f));
                    ValidActions.Add(("act_affected_masterstrike_1h_and_shield_face_punch", "act_affector_masterstrike_1h_and_shield_face_punch", 1.85f));
                    ValidActions.Add(("act_affected_masterstrike_onehanded_and_shield_2", "act_affector_masterstrike_onehanded_and_shield_2", 2.0f));
                    ValidActions.Add(("act_affected_masterstrike_1h_sword_1", "act_affector_masterstrike_1h_sword_1", 2.2f));
                    ValidActions.Add(("act_affected_masterstrike_onehanded_and_shield_1", "act_affector_masterstrike_onehanded_and_shield_1", 1.9f));
                }
            }

            // --- АНИМАЦИИ С ОДНОРУЧНЫМ ОРУЖИЕМ ПРОТИВ ДВУРУЧНОГО ---
            else if (affectorAgent.HasOneHandedWeapon() && affectedAgent.HasTwoHandedWeapon())
            {
                if (affectedAgent.Has2HSword() || affectedAgent.Has2HAxe() || affectedAgent.Has2HMace() || affectedAgent.Has2HPolearm(true) || affectedAgent.Has2HPolearm(false))
                {
                    if (affectorAgent.HasShieldInHand() && affectorAgent.GetWeaponLength() <= 160)
                    {
                        ValidActions.Add(("act_affected_masterstrike_2h_vs_1h_and_shield_double_stab", "act_affector_masterstrike_1h_and_shield_double_stab", 2.0f));
                        ValidActions.Add(("act_affected_masterstrike_2h_vs_1h_and_shield_just_shove", "act_affector_masterstrike_1h_and_shield_just_shove", 1.8f));
                        ValidActions.Add(("act_affected_masterstrike_2h_vs_1h_and_shield_face_punch", "act_affector_masterstrike_1h_and_shield_face_punch", 1.85f));
                        ValidActions.Add(("act_affected_masterstrike_2h_vs_1h_and_shield_ukol_v_plecho", "act_affector_masterstrike_1h_and_shield_ukol_v_plecho", 2.0f));
                        ValidActions.Add(("act_affected_masterstrike_2h_vs_1h_sword_1", "act_affector_masterstrike_1h_sword_1", 2.2f));
                    }
                    else if (!affectorAgent.HasShieldInHand() && !affectorAgent.UseWeaponClass(WeaponClass.Banner, isOffHand: true))
                    {
                        ValidActions.Add((act_affected_masterstrike_1h_sword_only_1, "act_affector_masterstrike_1h_sword_only_1", 1.7f));
                        ValidActions.Add(("act_affected_masterstrike_2h_vs_1h_sword_only_3", "act_affector_masterstrike_1h_sword_only_3", 1.6f));
                        ValidActions.Add(("act_affected_masterstrike_2h_vs_1h_sword_stun", "act_affector_masterstrike_1h_sword_stun", 1.8f));
                    }                    
                }
            }

            // --- АНИМАЦИИ С ДВУРУЧНЫМ ОРУЖИЕМ ---
            else if (affectorAgent.HasTwoHandedWeapon() && affectedAgent.HasTwoHandedWeapon())
            {
                if (affectedAgent.Has2HSword() || affectedAgent.Has2HAxe() || affectedAgent.Has2HMace() || affectedAgent.Has2HPolearm(true) || affectedAgent.Has2HPolearm(false))
                {
                    if (affectorAgent.Has2HSword())
                    {
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_1", "act_affector_masterstrike_2h_sword_1", 2.2f));
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_2", "act_affector_masterstrike_2h_sword_2", 2.2f));
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_3", "act_affector_masterstrike_2h_sword_3", 2.2f));
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_4", "act_affector_masterstrike_2h_sword_4", 2.0f));
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_5", "act_affector_masterstrike_2h_sword_5", 2.1f));
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_6", "act_affector_masterstrike_2h_sword_6", 2.1f));
                    }
                    else if (affectorAgent.Has2HMace())
                    {
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_2", "act_affector_masterstrike_2h_sword_2", 2.2f));
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_4", "act_affector_masterstrike_2h_sword_4", 2.0f));
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_5", "act_affector_masterstrike_2h_sword_5", 2.1f));
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_6", "act_affector_masterstrike_2h_sword_6", 2.1f));
                    }
                    else if (affectorAgent.Has2HAxe() || affectorAgent.Has2HPolearm(true))
                    {
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_2", "act_affector_masterstrike_2h_sword_2", 2.2f));
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_4", "act_affector_masterstrike_2h_sword_4", 2.0f));
                        ValidActions.Add(("act_affected_masterstrike_2h_sword_6", "act_affector_masterstrike_2h_sword_6", 2.1f));
                    }
                }
            }

            // --- АНИМАЦИИ С ДВУРУЧНЫМ ОРУЖИЕМ ПРОТИВ ОДНОРУЧНОГО ---
            else if (affectorAgent.HasTwoHandedWeapon() && affectedAgent.HasOneHandedWeapon())
            {
                if (affectorAgent.Has2HSword())
                {
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_1", "act_affector_masterstrike_2h_sword_1", 2.2f));
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_2", "act_affector_masterstrike_2h_sword_2", 2.3f));
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_4", "act_affector_masterstrike_2h_sword_4", 2.0f));
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_5", "act_affector_masterstrike_2h_sword_5", 2.1f));
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_6", "act_affector_masterstrike_2h_sword_6", 2.1f));
                }
                else if (affectorAgent.Has2HMace())
                {
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_2", "act_affector_masterstrike_2h_sword_2", 2.3f));
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_4", "act_affector_masterstrike_2h_sword_4", 2.0f));
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_5", "act_affector_masterstrike_2h_sword_5", 2.1f));
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_6", "act_affector_masterstrike_2h_sword_6", 2.1f));
                }
                else if (affectorAgent.Has2HAxe() || affectorAgent.Has2HPolearm(true))
                {
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_2", "act_affector_masterstrike_2h_sword_2", 2.3f));
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_4", "act_affector_masterstrike_2h_sword_4", 2.0f));
                    ValidActions.Add(("act_affected_masterstrike_1h_vs_2h_sword_6", "act_affector_masterstrike_2h_sword_6", 2.1f));
                }
            }


            // Если нет подходящих анимаций - выходим
            if (ValidActions.Count == 0) 
            {
                MasterStrikesBehavior.Instance!._masterstrikeAgents.Remove(affectorAgent);
                return;
            }
            
            

            var MS = ValidActions.ToList()[MBRandom.RandomInt(ValidActions.Count)];
            
            if (MasterStrikesBehavior.Instance!.LastAgentsMS.TryGetValue(affectorAgent, out string lastAnim))
            {
                if (MS.affector == lastAnim && ValidActions.Count > 1)
                {
                    ValidActions.Remove(MS);                    
                    MS = ValidActions.ToList()[MBRandom.RandomInt(ValidActions.Count)];
                }
            }

            MasterStrikesBehavior.Instance!.LastAgentsMS[affectorAgent] = MS.affector;


            affectorAgent.SetAutomaticTargetSelection(false);
            affectedAgent.SetAutomaticTargetSelection(false);

            // Неуязвимость во время анимаций
            if (ModSettings.Instance.EnableInvulnerabilityDuringActions)
            {
                affectorAgent.SetMortalityState(Agent.MortalityState.Invulnerable);
                affectedAgent.SetMortalityState(Agent.MortalityState.Invulnerable);
            }

            if (ModSettings.Instance.Highlight)
            {
                Gags.HighlightAgent(affectorAgent, Gags.Action.MasterStrike);
                Gags.HighlightAgent(affectedAgent, Gags.Action.MasterStrike);
            }

            AgentExtensions.SyncAgentPositions(affectedAgent, affectorAgent, MS.distance, updatePosition: true);
            MasterStrikesBehavior.OnMasterStrikeStarted?.Invoke(affectorAgent, affectedAgent);

            affectedAgent.SetActionChannel(0, ActionIndexCache.Create(MS.affected), true, 0UL, 0f, AgentExtensions.CalculateActionSpeed(affectedAgent, AgentExtensions.MasterStrikesAction.MasterStrike), -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
            affectorAgent.SetActionChannel(0, ActionIndexCache.Create(MS.affector), true, 0UL, 0f, AgentExtensions.CalculateActionSpeed(affectorAgent, AgentExtensions.MasterStrikesAction.MasterStrike), -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
        }
    }    
}