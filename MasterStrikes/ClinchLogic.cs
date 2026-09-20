using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace MasterStrikes
{
    public class ClinchLogic
    {
        public ClinchLogic(Agent affectorAgent, Agent affectedAgent)
        {
            
            var ValidActions = new HashSet<(string affected, string affector, float distance)>();


            // --- АНИМАЦИИ С ОДНОРУЧНЫМ ОРУЖИЕМ ---
            if (affectorAgent.HasOneHandedWeapon() && affectedAgent.HasOneHandedWeapon())
            {
                if (affectorAgent.GetRelevantWeaponLengthForClinch() != 0)
                {
                    ValidActions.Add(("act_affected_1h_clinch_1", "act_affector_1h_clinch_1", 1.3f));
                    ValidActions.Add(("act_affected_1h_clinch_2", "act_affector_1h_clinch_2", 1.3f));
                    ValidActions.Add(("act_affected_1h_clinch_3", "act_affector_1h_clinch_3", 1.3f));
                }                
            }

            // --- АНИМАЦИИ С ОДНОРУЧНЫМ ОРУЖИЕМ ПРОТИВ ДВУРУЧНОГО ---
            else if (affectorAgent.HasOneHandedWeapon() && affectedAgent.HasTwoHandedWeapon())
            {
                if (affectedAgent.HasAny2HWeaponNoSpear() || affectedAgent.Has2HPolearm(false))
                {
                    if (affectorAgent.GetRelevantWeaponLengthForClinch() != 0)
                    {
                        ValidActions.Add(("act_affected_1h_vs_2h_clinch_2", "act_affector_1h_clinch_2", 1.3f));
                        ValidActions.Add(("act_affected_1h_vs_2h_clinch_3", "act_affector_1h_clinch_3", 1.3f));
                    }
                }
            }

            // --- АНИМАЦИИ С ДВУРУЧНЫМ ОРУЖИЕМ ---
            else if (affectorAgent.HasTwoHandedWeapon() && affectedAgent.HasTwoHandedWeapon())
            {
                if (affectedAgent.HasAny2HWeaponNoSpear() || affectedAgent.Has2HPolearm(false))
                {
                    if (affectorAgent.HasAny2HWeaponNoSpear() && affectorAgent.GetRelevantWeaponLengthForClinch() != 0)
                    {
                        ValidActions.Add(("act_affected_2h_clinch_1", "act_affector_2h_clinch_1", 1.2f));
                        ValidActions.Add(("act_affected_2h_clinch_2", "act_affector_2h_clinch_2", 1.4f));
                    }
                }
            }

            // --- АНИМАЦИИ С ДВУРУЧНЫМ ОРУЖИЕМ ПРОТИВ ОДНОРУЧНОГО ---
            else if (affectorAgent.HasTwoHandedWeapon() && affectedAgent.HasOneHandedWeapon())
            {
                if (affectorAgent.HasAny2HWeaponNoSpear() && affectorAgent.GetRelevantWeaponLengthForClinch() != 0)
                {
                    ValidActions.Add(("act_affected_2h_vs_1h_clinch_1", "act_affector_2h_clinch_1", 1.2f));
                    ValidActions.Add(("act_affected_2h_vs_1h_clinch_2", "act_affector_2h_clinch_2", 1.4f));
                }
            }
                        

            if (ValidActions.Count == 0)
            {
                MasterStrikesBehavior.Instance!.GetClinchAgents.Remove(affectorAgent);
                return;
            }


            var CL = ValidActions.ToList()[MBRandom.RandomInt(ValidActions.Count)];

            if (MasterStrikesBehavior.Instance!.LastAgentsCL.TryGetValue(affectorAgent, out string lastAnim))
            {
                if (CL.affector == lastAnim && ValidActions.Count > 1)
                {
                    ValidActions.Remove(CL);
                    CL = ValidActions.ToList()[MBRandom.RandomInt(ValidActions.Count)];
                }
            }

            MasterStrikesBehavior.Instance!.LastAgentsCL[affectorAgent] = CL.affector;


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
                Gags.HighlightAgent(affectorAgent, Gags.Action.Clinch);
                Gags.HighlightAgent(affectedAgent, Gags.Action.Clinch);
            }

            AgentExtensions.SyncAgentPositions(affectedAgent, affectorAgent, CL.distance, updatePosition: true);
            MasterStrikesBehavior.OnClinchStarted?.Invoke(affectorAgent, affectedAgent);

            affectedAgent.SetActionChannel(0, ActionIndexCache.Create(CL.affected), true, 0UL, 0f, AgentExtensions.CalculateActionSpeed(affectedAgent, AgentExtensions.MasterStrikesAction.Clinch), -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
            affectorAgent.SetActionChannel(0, ActionIndexCache.Create(CL.affector), true, 0UL, 0f, AgentExtensions.CalculateActionSpeed(affectorAgent, AgentExtensions.MasterStrikesAction.Clinch), -0.2f, 0.4f, 0f, false, -0.2f, 0, true);            
        }
    }
}