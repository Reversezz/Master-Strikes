using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace MasterStrikes
{
    public static class AgentExtensions
    {
        public static bool IsDodging(this Agent agent) => agent.GetCurrentAction(0).GetName().Contains("dodge");
        public static bool IsInMasterStrikeAction(this Agent agent) => agent.GetCurrentAction(0).GetName().Contains("masterstrike");
        public static bool IsInClinchAction(this Agent agent) => agent.GetCurrentAction(0).GetName().Contains("clinch");
        public static bool IsInAnyMSAction(this Agent agent) => agent.IsDodging() || agent.IsInMasterStrikeAction() || agent.IsInClinchAction();

        public static bool IsStaggered(this Agent agent) => agent.GetCurrentAction(0).GetName().Contains("stagger");


        public static bool IsBusy(this Agent agent)
        {
            MasterStrikesBehavior instance = MasterStrikesBehavior.Instance;

            return instance != null && agent.IsAgentCorrect()
                && (instance._masterstrikeAgents.ContainsKey(agent) || instance._masterstrikeAgents.ContainsValue(agent)
                 || instance._clinchAgents.ContainsKey(agent) || instance._clinchAgents.ContainsValue(agent));
        }


        public static bool IsAttacking(this Agent agent) => agent.GetCurrentActionType(1) == Agent.ActionCodeType.ReleaseMelee;
        public static bool IsBlocking(this Agent agent) => agent.CurrentGuardMode != Agent.GuardMode.None;


        public static bool СurrentActionTypeIsKickOrBash(this Agent agent) => (agent.GetCurrentActionType(0) >= Agent.ActionCodeType.KickAllBegin && agent.GetCurrentActionType(0) <= Agent.ActionCodeType.KickAllEnd) ||
                                                                               agent.GetCurrentAction(0).GetName().Contains("kick") || agent.GetCurrentAction(0).GetName().Contains("bash");
        public static bool IsAgentCorrect(this Agent agent) => agent != null && agent.IsHuman && agent.IsActive() && agent.ActionSet.IsValid;
        public static int GetSkillValueFromWeaponClass(this Agent agent) => agent.Character.GetSkillValue(agent?.WieldedWeapon.CurrentUsageItem?.RelevantSkill);

        public static bool HasShieldInHand(this Agent agent) => agent.IsAgentHaveShield() && agent.WieldedOffhandWeapon.CurrentUsageItem?.IsShield == true;
        public static bool HasOneHandedWeapon(this Agent agent) => agent.WieldedWeapon.CurrentUsageItem?.IsOneHanded == true;
        public static bool HasTwoHandedWeapon(this Agent agent) => agent.WieldedWeapon.CurrentUsageItem?.IsTwoHanded == true;
        public static bool Has2HSword(this Agent agent) => agent.UseWeaponClass(WeaponClass.TwoHandedSword);
        public static bool Has2HAxe(this Agent agent) => agent.UseWeaponClass(WeaponClass.TwoHandedAxe);
        public static bool Has2HMace(this Agent agent) => agent.UseWeaponClass(WeaponClass.TwoHandedMace);
        public static bool HasMissile(this Agent agent)
        {
            WeaponComponentData weapon = agent.WieldedWeapon.CurrentUsageItem;
            return (weapon.WeaponClass >= WeaponClass.Sling && weapon.WeaponClass <= WeaponClass.Javelin) || weapon.WeaponClass == WeaponClass.SlingStone || weapon.WeaponClass == WeaponClass.BallistaBoulder || weapon.WeaponClass == WeaponClass.BallistaStone;
        }

        public static bool Has2HPolearm(this Agent agent, bool requireSwingDamage)
        {
            if (agent.UseWeaponClass(WeaponClass.TwoHandedPolearm) && !agent.WieldedWeapon.CurrentUsageItem.IsConsumable)
            {
                // ЕСЛИ requireSwingDamage = false  --> Тогда это true и проходит дальше, потому что первое условие подошло
                // ЕСЛИ requireSwingDamage = true   --> Тогда это false и нам нужен true во втором условии, чтобы прошло дальше
                if (!requireSwingDamage || agent.WieldedWeapon.CurrentUsageItem.SwingDamage > 0)
                    return true;
            }
            return false;
        }
        public static bool HasAny2HWeaponNoSpear(this Agent agent)
        {
            return agent.Has2HSword() || agent.Has2HAxe() || agent.Has2HMace() || agent.Has2HPolearm(true);
        }


        public static int GetWeaponLength(this Agent agent) => agent?.WieldedWeapon.CurrentUsageItem?.WeaponLength ?? 0;
        public static int GetRelevantWeaponLengthForClinch(this Agent agent, bool isForClinch = true)
        {
            WeaponComponentData item = agent.WieldedWeapon.CurrentUsageItem;

            if (agent.IsAgentCorrect())
            {
                // Одноручное
                if (agent.HasOneHandedWeapon() && (item.WeaponClass != WeaponClass.OneHandedPolearm || item.SwingDamage > 0)) return agent.GetWeaponLength();

                // Двуручное
                else if (agent.HasTwoHandedWeapon() && agent.GetWeaponLength() <= 160) return agent.GetWeaponLength();                                             
            }

            return 0;
        }


        public static bool IsBeingAttacked(this Agent agent, Agent enemy)
        {
            if (agent.IsAgentCorrect() && enemy.IsAgentCorrect())
            {
                if (Mission.Current.AgentLookingAtAgent(agent, enemy))
                {
                    return enemy.IsAttacking() && enemy.GetCurrentActionProgress(1) <= 0.2f;
                }
            }

            return false;
        }

        public static Agent GetNearestEnemyForClinch(this Agent agent)
        {
            if (!agent.IsAgentCorrect()) return null;

            MBList<Agent> nearbyEnemies = new MBList<Agent>();
            Mission.Current.GetNearbyEnemyAgents(agent.Position.AsVec2, 5f, agent.Team, nearbyEnemies);

            if (nearbyEnemies.Count == 0) return null;

            Agent bestTarget = null;
            float bestScore = float.MaxValue;

            foreach (Agent enemy in nearbyEnemies)
            {
                if (!enemy.IsAgentCorrect() || enemy.MountAgent != null) continue;
                if (enemy.IsBusy() || enemy.IsInAnyMSAction() || enemy.IsStaggered()) continue;
                if (enemy.WieldedWeapon.IsEmpty) continue;

                // Дистанция — чем ближе, тем лучше
                float distance = agent.Position.Distance(enemy.Position);

                // Направление взгляда: смотрим ли мы на врага
                Vec3 directionToEnemy = (enemy.Position - agent.Position);
                directionToEnemy.Normalize();
                float lookAngle = Vec3.AngleBetweenTwoVectors(agent.LookDirection, directionToEnemy);

                // Угол > 60 градусов — враг за спиной, не подходит
                if (lookAngle > 1.05f) continue; // 60 градусов в радианах

                // Итоговый счёт: дистанция + штраф за угол
                if ((distance + lookAngle * 1.5f) is float score && score < bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            return bestTarget;
        }

        public static bool IsThisWasTargetedAttackBy(this Agent agent, Agent enemy, Blow blow, AttackCollisionData attackCollisionData)
        {
            if (agent.WieldedWeapon.IsEmpty || enemy.WieldedWeapon.IsEmpty) return false;

            // Если цель enemy - это агент
            if (enemy.GetTargetAgent() == agent)
            {
                Vec3 directionToAgent = (agent.Position - enemy.Position);
                directionToAgent.Normalize();

                // Если enemy смотрит на агента (чуть больше 30 градусов в радианах)
                if (Vec3.AngleBetweenTwoVectors(enemy.LookDirection, directionToAgent) < 0.65f)
                {
                    // Например удар ногой может выбить из равновесия так же, как мощный удар топором, но урона у него почти нет, так что проверка на > 35 
                    if ((blow.InflictedDamage >= 35 || (blow.IsMissile && blow.InflictedDamage >= 25) || attackCollisionData.IsAlternativeAttack) && attackCollisionData.CollisionResult == CombatCollisionResult.StrikeAgent)
                    {
                        return true;                        
                    }
                }
            }

            return false;
        }

        public static bool UseWeaponClass(this Agent agent, WeaponClass weaponClass, bool isOffHand = false)
        {
            if (!agent.IsAgentCorrect()) return false;

            MissionWeapon weapon = !(isOffHand) ? (agent.WieldedWeapon) : (agent.WieldedOffhandWeapon);
            return !weapon.IsEmpty && weapon.CurrentUsageItem?.WeaponClass == weaponClass;
        }       
        

        public static bool IsAgentHaveShield(this Agent agent)
        {
            for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot; index < EquipmentIndex.NumAllWeaponSlots; index++)
                if (!agent.Equipment[index].IsEmpty && agent.Equipment[index].IsShield())
                    return true;

            return false;
        }

        public static bool WieldedOffhandWeaponIsMetalShield(this Agent agent)
        {
            if (agent.IsAgentCorrect())
            {
                if (agent.IsAgentHaveShield())
                {
                    if (agent.WieldedOffhandWeapon.CurrentUsageItem?.IsShield == true)
                    {
                        if (agent.WieldedOffhandWeapon.CurrentUsageItem.PhysicsMaterial.Contains("metal"))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }


        internal static void SyncAgentPositions(Agent affected, Agent affector, float distance, bool updatePosition)
        {
            Vec3 directionToAffected;
            Vec3 directionToAffector;            

            if (affected.IsMainAgent)
            {
                directionToAffector = (affector.Position - affected.Position);
                directionToAffector.Normalize();
                directionToAffected = -directionToAffector;
                if (updatePosition)
                    affector.TeleportToPosition(affected.Position + directionToAffector * distance);
            }
            else if (affector.IsMainAgent)
            {
                directionToAffected = (affected.Position - affector.Position);
                directionToAffected.Normalize();
                directionToAffector = -directionToAffected;
                if (updatePosition)
                    affected.TeleportToPosition(affector.Position + directionToAffected * distance);
            }
            else
            {
                directionToAffected = (affected.Position - affector.Position);
                directionToAffected.Normalize();
                directionToAffector = -directionToAffected;
                if (updatePosition)
                    affected.TeleportToPosition(affector.Position + directionToAffected * distance);
            }            

            affected.EventControlFlags = Agent.EventControlFlag.None;
            affector.EventControlFlags = Agent.EventControlFlag.None;

            affected.LookDirection = directionToAffector;
            affector.LookDirection = directionToAffected;

            affected.SetTargetAgent(affector);
            affector.SetTargetAgent(affected);

        
            //// Проверка на застрявших в словаре
            //if (affector.IsBusy() && affected.IsBusy() && !(affector.IsInMasterStrikeAction() || affector.IsInClinchAction()) && !(affected.IsInMasterStrikeAction() || affected.IsInClinchAction()))
            //{
            //    Gags.HighlightAgent(affector, Gags.Action.Problem);
            //    Gags.HighlightAgent(affected, Gags.Action.Problem);
            //}            
        }

        internal static int GetNearbyAgentsCountAtRadius(this Agent agent, float radius, bool ally = false, bool enemy = false)
        {
            if (!agent.IsAgentCorrect()) return 0;

            if (ally) return Mission.Current.GetNearbyAllyAgents(agent.Position.AsVec2, radius, agent.Team, new MBList<Agent>()).Count(a => a.MountAgent == null);
            else if (enemy) return Mission.Current.GetNearbyEnemyAgents(agent.Position.AsVec2, radius, agent.Team, new MBList<Agent>()).Count(a => a.MountAgent == null);
            else return Mission.Current.GetNearbyAgents(agent.Position.AsVec2, radius, new MBList<Agent>()).Count(a => a.MountAgent == null);
        }

        public static bool ActionProgress(this Agent agent, float progress) => agent.GetCurrentActionProgress(0) >= progress - 0.004f && agent.GetCurrentActionProgress(0) <= MathF.Clamp(progress + 0.004f, progress, 1f); 
        
        public static void PlaySoundAtActionProgress(this Agent agent, string clipName, float requiredProgress)
        {
            if (agent.ActionProgress(requiredProgress))
                MBSoundEvent.PlaySound(SoundEvent.GetEventIdFromString(clipName), agent.Position);
        }

        //public static void SpawnBloodAtActionProgress(this Agent affected, Agent affector, float requiredProgress, sbyte boneIndex)
        //{
        //    float currentProgress = affected.GetCurrentActionProgress(0);

        //    if (currentProgress >= requiredProgress - 0.004f && currentProgress <= requiredProgress + 0.004f)
        //    {
        //        MatrixFrame bloodFrame = affector.Frame.Advance(1.2f).Elevate(0.6f);
        //        bloodFrame.rotation.RotateAboutSide(90f);
        //        Mission.Current.Scene.CreateBurstParticle(ParticleSystemManager.GetRuntimeIdByName("psys_game_blood_sword_enter"), bloodFrame);
        //        affected.CreateBloodBurstAtLimb(boneIndex, 0.5f);
        //    }
        //}


        
        public static bool TryGetKeyByValue<TKey, TValue>(this Dictionary<TKey, TValue> dict, TValue value, out TKey key)
        {
            key = dict.FirstOrDefault(x => Equals(x.Value, value)).Key;
            return key != null && dict.ContainsKey(key);
        }

        public enum MasterStrikesAction
        {
            Dodge,
            MasterStrike,
            Clinch
        }

        public static float CalculateActionSpeed(Agent agent, MasterStrikesAction action)
        {
            int skill;
            float minSpeed;
            float maxSpeed;

            switch (action)
            {
                case MasterStrikesAction.Dodge:
                    skill = agent.Character.GetSkillValue(DefaultSkills.Athletics);
                    minSpeed = 0.92f;
                    maxSpeed = 1.142f;
                    break;

                case MasterStrikesAction.MasterStrike:
                    skill = agent.GetSkillValueFromWeaponClass();
                    minSpeed = 0.9f;
                    maxSpeed = 1.12f;
                    break;

                case MasterStrikesAction.Clinch:
                    skill = agent.GetSkillValueFromWeaponClass();
                    minSpeed = 0.91f;
                    maxSpeed = 1.14f;
                    break;

                default:
                    return 1f;
            }

            float t = Math.Min(skill / 300f, 1f);
            return minSpeed + (maxSpeed - minSpeed) * t;
        }

        public static float GetDodgeCooldownByAge(this Agent agent)
        {
            //Age: 18 - Dodge rate: 0.08
            //Age: 19 - Dodge rate: 0.09
            //Age: 20 - Dodge rate: 0.10
            //Age: 21 - Dodge rate: 0.11
            //Age: 22 - Dodge rate: 0.12
            //Age: 23 - Dodge rate: 0.13
            //Age: 24 - Dodge rate: 0.14
            //Age: 25 - Dodge rate: 0.16
            //Age: 26 - Dodge rate: 0.20
            //Age: 27 - Dodge rate: 0.22
            //Age: 28 - Dodge rate: 0.24
            //Age: 29 - Dodge rate: 0.25
            //Age: 30 - Dodge rate: 0.27
            //Age: 31 - Dodge rate: 0.29
            //Age: 32 - Dodge rate: 0.31
            //Age: 33 - Dodge rate: 0.33
            //Age: 34 - Dodge rate: 0.35
            //Age: 35 - Dodge rate: 0.37
            //Age: 36 - Dodge rate: 0.52
            //Age: 37 - Dodge rate: 0.55
            //Age: 38 - Dodge rate: 0.58
            //Age: 39 - Dodge rate: 0.61
            //Age: 40 - Dodge rate: 0.64
            //Age: 41 - Dodge rate: 0.67
            //Age: 42 - Dodge rate: 0.71
            //Age: 43 - Dodge rate: 0.74
            //Age: 44 - Dodge rate: 0.77
            //Age: 45 - Dodge rate: 0.81
            //Age: 46 - Dodge rate: 2.12
            //Age: 47 - Dodge rate: 2.21
            //Age: 48 - Dodge rate: 2.30
            //Age: 49 - Dodge rate: 2.40
            //Age: 50 - Dodge rate: 2.50
            //Age: 51 - Dodge rate: 3.25
            //Age: 52 - Dodge rate: 3.38
            //Age: 53 - Dodge rate: 3.51
            //Age: 54 - Dodge rate: 3.65
            //Age: 55 - Dodge rate: 3.78
            //Age: 56 - Dodge rate: 3.92
            //Age: 57 - Dodge rate: 4.06
            //Age: 58 - Dodge rate: 4.21
            //Age: 59 - Dodge rate: 4.35
            //Age: 60 - Dodge rate: 4.50
            //Age: 61 - Dodge rate: 46.51
            //Age: 62 - Dodge rate: 48.05
            //Age: 63 - Dodge rate: 49.61
            //Age: 64 - Dodge rate: 51.20
            //Age: 65 - Dodge rate: 52.81
            //Age: 66 - Dodge rate: 54.45
            //Age: 67 - Dodge rate: 56.11
            //Age: 68 - Dodge rate: 57.80
            //Age: 69 - Dodge rate: 59.51
            //Age: 70 - Dodge rate: 61.25
            //Age: 71 - Dodge rate: 63.01
            //Age: 72 - Dodge rate: 64.80
            //Age: 73 - Dodge rate: 66.61
            //Age: 74 - Dodge rate: 68.45
            //Age: 75 - Dodge rate: 70.31
            //Age: 76 - Dodge rate: 72.20
            //Age: 77 - Dodge rate: 74.11
            //Age: 78 - Dodge rate: 76.05
            //Age: 79 - Dodge rate: 78.01
            //Age: 80 - Dodge rate: 80.00

            float divisor = agent.Age < 26 ? 40 : (agent.Age < 36) ? 33 : (agent.Age < 46) ? 25 : (agent.Age < 51) ? 10 : (agent.Age < 61) ? 8 : 0.8f;
            return agent.Age / 100f * (agent.Age / divisor);
        }
    }



    /// <summary>
    /// Класс-инструмент, подсвечивающий агентов по контруру при определённых условиях.
    /// </summary>
    internal static class Gags
    {
        internal enum Action
        {
            MasterStrike = 1,
            Clinch = 2,
            Dodge = 3,
            Other = 4,
            Problem = 5
        }

        private const float MasterStrikeHighlightDuration = 5f;
        private const float ClinchHighlightDuration = 4f;
        private const float DodgeHighlightDuration = 2f;
        private const float OtherHighlightDuration = 5f;
        private const float ProblemHighlightDuration = 20f;

        public static uint GetRelevantColor(Action action)
        {
            if (action == Action.MasterStrike) return Color.ConvertStringToColor("#47ff4eff").ToUnsignedInteger();
            if (action == Action.Clinch) return Color.ConvertStringToColor("#ffde61ff").ToUnsignedInteger();
            if (action == Action.Dodge) return Color.ConvertStringToColor("#0beac4ff").ToUnsignedInteger();
            if (action == Action.Other) return Color.ConvertStringToColor("#f97affff").ToUnsignedInteger();
            if (action == Action.Problem) return Color.ConvertStringToColor("#ff2020ff").ToUnsignedInteger();

            return 0;
        }

        internal static Dictionary<Agent, float> HighlightTimers = new Dictionary<Agent, float>();

        internal static void HighlightAgent(Agent agent, Action action)
        {
            if (agent == null) return;
            if (Mission.Current == null) return;
            if (!agent.IsAgentCorrect()) return;
            if (agent.AgentVisuals == null) return;

            if (HighlightTimers.ContainsKey(agent))
                ResetHighlightAgent(agent);

            agent.AgentVisuals.SetContourColor(GetRelevantColor(action), true);

            float duration;

            if (action == Action.MasterStrike) duration = MasterStrikeHighlightDuration;
            else if (action == Action.Clinch) duration = ClinchHighlightDuration;
            else if (action == Action.Dodge) duration = DodgeHighlightDuration;
            else if (action == Action.Other) duration = OtherHighlightDuration;
            else if (action == Action.Problem) duration = ProblemHighlightDuration;
            else duration = OtherHighlightDuration;

            HighlightTimers[agent] = Mission.Current.CurrentTime + duration;
        }

        internal static void ResetHighlightAgent(Agent agent)
        {
            if (agent == null) return;
            if (!agent.IsAgentCorrect())
            {
                HighlightTimers.Remove(agent);
                return;
            }

            agent.AgentVisuals?.SetContourColor(null, true);
            HighlightTimers.Remove(agent);
        }

        internal static void TickHighlightTimers()
        {
            if (Mission.Current == null) return;
            if (HighlightTimers == null) return;

            float currentTime = Mission.Current.CurrentTime;
            List<Agent> agentsToReset = null;

            foreach (var pair in HighlightTimers.ToList())
            {
                Agent agent = pair.Key;

                if (agent == null || !agent.IsAgentCorrect())
                {
                    if (agentsToReset == null)
                        agentsToReset = new List<Agent>();
                    agentsToReset.Add(agent);
                    continue;
                }

                if (currentTime >= pair.Value)
                {
                    if (agentsToReset == null)
                        agentsToReset = new List<Agent>();
                    agentsToReset.Add(agent);
                }
            }

            if (agentsToReset != null)
            {
                foreach (var agent in agentsToReset)
                {
                    ResetHighlightAgent(agent);
                }
            }
        }
    }

}