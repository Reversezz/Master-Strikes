using RBMAI;
using System;
using TaleWorlds.MountAndBlade;
using RBMConfigForMS = RBMConfig.RBMConfig;

namespace MasterStrikes.RBM
{
    internal static class RBMIntegration
    {
        internal const float MSStaminaCost = 25f;
        internal const float MSPostureCost = 80f;    // MasterStrikes.RBM
                                                    // stamina and posture expenses
        internal const float ClinchStaminaCost = 20f;
        internal const float ClinchPostureCost = 50f;

        internal const float DodgeStaminaCost = 80f;
        internal const float DodgePostureCost = 20f;


        internal const float MSStaminaMinRequired = MSStaminaCost + 10f;
        internal const float MSPostureMinRequired = MSPostureCost + 15f;

        internal const float ClinchStaminaMinRequired = ClinchStaminaCost + 10f;
        internal const float ClinchPostureMinRequired = ClinchPostureCost + 15f;

        internal const float DodgeStaminaMinRequired = DodgeStaminaCost + 10f;
        internal const float DodgePostureMinRequired = DodgePostureCost + 15f;

                                                // when dealing damage with a move
        internal const float MSStaminaDamage = 50f;
        internal const float MSPostureDamage = 150f;

        internal const float ClinchStaminaDamage = 30f;
        internal const float ClinchPostureDamage = 90f;


        private static bool TryGetStance(Agent agent, out Stance stance)
        {
            stance = null;
            if (agent == null || !agent.IsActive()) return false;
            if (AgentStances.values == null) return false;
            return AgentStances.values.TryGetValue(agent, out stance);
        }

        internal static bool HasEnoughPosture(this Agent agent, float required)
        {
            if (!TryGetStance(agent, out Stance s)) return false;
            return s.posture > required;
        }

        internal static bool HasEnoughStamina(this Agent agent, float required)
        {
            if (!TryGetStance(agent, out Stance s)) return false;
            return s.stamina > required;
        }

        internal static void ReduceStamina(this Agent agent, float amount)
        {
            if (!TryGetStance(agent, out Stance s)) return;
            if (RBMConfigForMS.staminaEnabled)
                s.reduceStamina(amount);
        }

        internal static void ReducePosture(this Agent agent, float amount)
        {
            if (!TryGetStance(agent, out Stance s)) return;
            if (RBMConfigForMS.postureEnabled)
                s.reducePosture(amount);
        }

        internal static void EnsurePosture(Agent agent, float minPosture)
        {
            if (!TryGetStance(agent, out Stance s)) return;
            s.posture = Math.Max(s.posture, minPosture);
        }

        internal static float GetPosture(this Agent agent)
        {
            if (!TryGetStance(agent, out Stance s)) return 100f;
            return s.posture;
        }

        internal static void AddStamina(this Agent agent, float amount)
        {
            if (!TryGetStance(agent, out Stance s)) return;
            if (RBMConfigForMS.staminaEnabled)
                s.addStamina(amount);
        }

        internal static float GetStamina(this Agent agent)
        {
            if (!TryGetStance(agent, out Stance s)) return 1500f;
            return s.stamina;
        }





        internal static bool OnCanStartMasterStrike(Agent affected, Agent affector)
        {
            if (affected == null || affector == null) return false;
            if (!affected.IsAgentCorrect() || !affector.IsAgentCorrect()) return false;

            if (!RBMConfigForMS.postureEnabled && !RBMConfigForMS.staminaEnabled) return true;

            bool hasPosture = !RBMConfigForMS.postureEnabled || affected.HasEnoughPosture(MSPostureMinRequired);
            bool hasStamina = !RBMConfigForMS.staminaEnabled || affected.HasEnoughStamina(MSStaminaMinRequired);

            return hasPosture && hasStamina;
        }

        internal static void OnMasterStrikeStarted(Agent affector, Agent affected)
        {
            if (affector == null || affected == null) return;
            if (!affector.IsAgentCorrect() || !affected.IsAgentCorrect()) return;

            if (RBMConfigForMS.staminaEnabled)
                affector.ReduceStamina(MSStaminaCost);

            if (RBMConfigForMS.postureEnabled)
                affector.ReducePosture(MSPostureCost);
        }

        internal static void OnMasterStrikeFinished(Agent affector, Agent affected, AttackCollisionData attackCollisionData)
        {
            if (affector == null || affected == null) return;
            if (!affector.IsAgentCorrect() || !affected.IsAgentCorrect() || affected.Health <= 0f) return;

            if (RBMConfigForMS.staminaEnabled)
                affected.ReduceStamina(MSStaminaDamage);

            if (RBMConfigForMS.postureEnabled)
            {
                affected.ReducePosture(MSPostureDamage);

                if (affected.GetPosture() <= 0f)
                {
                    // Удаляем из словаря перед стаггером, раз анимацию прерываем
                    if (MasterStrikesBehavior.Instance.GetMasterstrikeAgents.ContainsKey(affector))
                    {
                        MasterStrikesBehavior.Instance.GetMasterstrikeAgents.Remove(affector);
                    }
                    else if (MasterStrikesBehavior.Instance.GetMasterstrikeAgents.ContainsKey(affected))
                    {
                        MasterStrikesBehavior.Instance.GetMasterstrikeAgents.Remove(affected);
                    }

                    // Определяем директорию и верный стаггер
                    ActionIndexCache staggerAction;

                    switch (attackCollisionData.AttackDirection)
                    {
                        case Agent.UsageDirection.AttackLeft:
                            staggerAction = ActionIndexCache.act_stagger_right; // удар слева -> шатается вправо
                            break;
                        case Agent.UsageDirection.AttackRight:
                            staggerAction = ActionIndexCache.act_stagger_left; // удар справа -> шатается влево
                            break;
                        case Agent.UsageDirection.AttackUp:
                        case Agent.UsageDirection.AttackDown:
                            staggerAction = ActionIndexCache.act_stagger_backward; // удар сверху или колющий -> шатается назад     
                            break;
                        default:
                            staggerAction = ActionIndexCache.act_stagger_backward;
                            break;
                    }

                    affected.SetActionChannel(0, staggerAction, true, 0UL, 0f, 0.85f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);

                    if (TryGetStance(affected, out Stance stance))
                        StanceLogic.ResetPostureForAgent(ref stance, 0.75f);
                }
            }
        }




        internal static bool OnCanStartClinch(Agent affector, Agent affected)
        {
            if (affected == null || affector == null) return false;
            if (!affected.IsAgentCorrect() || !affector.IsAgentCorrect()) return false;

            if (!RBMConfigForMS.postureEnabled && !RBMConfigForMS.staminaEnabled) return true;

            bool hasPosture = !RBMConfigForMS.postureEnabled || affector.HasEnoughPosture(ClinchPostureMinRequired);
            bool hasStamina = !RBMConfigForMS.staminaEnabled || affector.HasEnoughStamina(ClinchStaminaMinRequired);

            return hasPosture && hasStamina;
        }

        internal static void OnClinchStarted(Agent affector, Agent affected)
        {
            if (affector == null || affected == null) return;
            if (!affector.IsAgentCorrect() || !affected.IsAgentCorrect()) return;

            if (RBMConfigForMS.staminaEnabled)
                affector.ReduceStamina(ClinchStaminaCost);

            if (RBMConfigForMS.postureEnabled)
                affector.ReducePosture(ClinchPostureCost);
        }

        internal static void OnClinchFinished(Agent affector, Agent affected, AttackCollisionData attackCollisionData)
        {
            if (affector == null || affected == null) return;
            if (!affector.IsAgentCorrect() || !affected.IsAgentCorrect() || affected.Health <= 0f) return;

            if (RBMConfigForMS.staminaEnabled)
                affected.ReduceStamina(ClinchStaminaDamage);

            if (RBMConfigForMS.postureEnabled)
            {
                affected.ReducePosture(ClinchPostureDamage);

                if (affected.GetPosture() <= 0f)
                {
                    // Удаляем из словаря перед стаггером, раз анимацию прерываем
                    if (MasterStrikesBehavior.Instance.GetClinchAgents.ContainsKey(affector))
                    {
                        affector.SetAutomaticTargetSelection(true);
                        affected.SetAutomaticTargetSelection(true);
                        MasterStrikesBehavior.Instance.GetClinchAgents.Remove(affector);
                    }
                    else if (MasterStrikesBehavior.Instance.GetClinchAgents.ContainsKey(affected))
                    {
                        affector.SetAutomaticTargetSelection(true);
                        affected.SetAutomaticTargetSelection(true);
                        MasterStrikesBehavior.Instance.GetClinchAgents.Remove(affected);
                    }

                    // Определяем директорию и верный стаггер
                    ActionIndexCache staggerAction;

                    switch (attackCollisionData.AttackDirection)
                    {
                        case Agent.UsageDirection.AttackLeft:
                            staggerAction = ActionIndexCache.act_stagger_right; // удар слева -> шатается вправо
                            break;
                        case Agent.UsageDirection.AttackRight:
                            staggerAction = ActionIndexCache.act_stagger_left; // удар справа -> шатается влево
                            break;
                        case Agent.UsageDirection.AttackUp:
                        case Agent.UsageDirection.AttackDown:
                            staggerAction = ActionIndexCache.act_stagger_backward; // удар сверху или колющий -> шатается назад     
                            break;
                        default:
                            staggerAction = ActionIndexCache.act_stagger_backward;
                            break;
                    }

                    affected.SetActionChannel(0, staggerAction, true, 0UL, 0f, 0.85f, -0.2f, 0.4f, 0f, false, -0.2f, 0, true);

                    if (TryGetStance(affected, out Stance stance))
                        StanceLogic.ResetPostureForAgent(ref stance, 0.75f);
                }
            }
        }


        internal static bool OnCanDodge(Agent agent)
        {
            if (agent == null || !agent.IsAgentCorrect()) return false;

            if (!RBMConfigForMS.postureEnabled && !RBMConfigForMS.staminaEnabled) return true;

            bool hasPosture = !RBMConfigForMS.postureEnabled || agent.HasEnoughPosture(DodgePostureMinRequired);
            bool hasStamina = !RBMConfigForMS.staminaEnabled || agent.HasEnoughStamina(DodgeStaminaMinRequired);

            return hasPosture && hasStamina;
        }

        internal static void OnDodgeStarted(Agent agent)
        {
            if (agent == null || !agent.IsAgentCorrect()) return;

            if (RBMConfigForMS.postureEnabled)
                agent.ReducePosture(DodgePostureCost);

            if (RBMConfigForMS.staminaEnabled)
                agent.ReduceStamina(DodgeStaminaCost);
        }
    }
}