using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace MasterStrikes
{
    internal class AIClinchComponent(Agent agent) : AgentComponent(agent)
    {

        // Константы
        private const int MaxLastActions = 4;
        private const float ApproachTimeout = 3f;
        private const float ClinchCooldown = 2f;

        // Очередь действий до следующего клинча
        private readonly Queue<string> _lastActions = new Queue<string>();

        private Agent _clinchTarget;
        private float _nextClinchTime;

        private float _approachTimer;
        private bool isApproaching;

        public override void OnTick(float dt)
        {
            base.OnTick(dt);

            if (!ModSettings.Instance.EnableClinch) return;
            if (Agent.IsMainAgent) return;
            if (!Agent.IsAgentCorrect() || Agent.GetSkillValueFromWeaponClass() < ModSettings.Instance.ClinchRequiredWeaponSkill || Agent.MountAgent != null) return;


            // Если уже занят — сбрасываем цель
            if (IsUsingOtherAnim(Agent))
            {
                //InformationManager.DisplayMessage(new InformationMessage($"{Agent.Name}: Выполняю {Agent.GetCurrentAction(0).GetName()}! Сбрасываю попытку сближения!", Color.ConvertStringToColor("#c2d981ff")));
                AddLastAction(Agent.GetCurrentAction(0).GetName());
                ResetClinchAttempt(withCooldown: true);
                return;
            }

            // Если выполняет удар
            if (Agent.IsAttacking())
            {
                //InformationManager.DisplayMessage(new InformationMessage($"{Agent.Name}: Выполняю {Agent.GetCurrentAction(1).GetName()}! Сбрасываю попытку сближения!", Color.ConvertStringToColor("#c2d981ff")));
                AddLastAction(Agent.GetCurrentAction(1).GetName());
                ResetClinchAttempt(withCooldown: true);
                return;
            }

            // Кулдаун после клинча/попытки
            if (Mission.Current.CurrentTime < _nextClinchTime) return;


            // Если недавно был клинч — ждём, пока агент сделает 3 других действия
            if (WasRecentAction("clinch") && _lastActions.Count < MaxLastActions)
            {
                ResetClinchAttempt(withCooldown: true);
                return;
            }

            // Если нет цели — ищем новую
            if (_clinchTarget == null || !_clinchTarget.IsAgentCorrect() || _clinchTarget.MountAgent != null)
            {
                if (!TryFindNewTarget()) return;
            }

            // Если есть цель — пробуем сблизиться
            ApproachTarget(dt);
        }


        public override void OnHit(Agent affectorAgent, int damage, in MissionWeapon affectorWeapon, in Blow b, in AttackCollisionData collisionData)
        {
            base.OnHit(affectorAgent, damage, affectorWeapon, b, collisionData);
            if (Agent.IsMainAgent) { return; }

            if (isApproaching)
            {
                if (damage > 0)
                { 
                    // Агент получил урон — боль мешает реализовать план
                    ResetClinchAttempt();
                }
            }
        }



        private bool TryFindNewTarget()
        {
            Agent enemy = Agent.GetNearestEnemyForClinch();
            if (!enemy.IsAgentCorrect() || enemy.MountAgent != null || enemy.Team == Agent.Team)
                return false;

            if (IsUsingOtherAnim(Agent) || Agent.IsStaggered() || IsUsingOtherAnim(enemy))
                return false;

            _clinchTarget = enemy;
            _approachTimer = 0f;

            Agent.SetTargetAgent(enemy);
            return true;
        }


        private void ApproachTarget(float dt)
        {
            if (_clinchTarget == null || !_clinchTarget.IsAgentCorrect())
            {
                ResetClinchAttempt(withCooldown: false);
                return;
            }

            // Если агент (уворачивается / в MS / в клинче / оглушён / уже почти завершил удар) — сбрасываем сближение
            if (IsUsingOtherAnim(Agent) || Agent.IsStaggered() || IsUsingOtherAnim(_clinchTarget))
            {                
                ResetClinchAttempt(withCooldown: true);
                return;
            }
            

            // Если враг слишком далеко — не сближаемся, просто сбрасываем таймер
            if (Agent.Position.Distance(_clinchTarget.Position) > 2.6f)
            {
                if (isApproaching == true)
                    isApproaching = false;

                _approachTimer = 0f; // сброс таймера — пусть подождёт
                return;
            }

            isApproaching = true;

            // Успех — цель близко
            if (Agent.Position.Distance(_clinchTarget.Position) <= 1.52f)
            {               
                // Прекращаем сближение
                Agent.ClearTargetFrame();
                                
                isApproaching = false;

                //InformationManager.DisplayMessage(new InformationMessage($"{Agent.Name}: Подошёл достаточно близко! Пытаюсь войти в клинч!", Color.ConvertStringToColor("#a0e26fff")));

                TryStartClinch();
                return;
            }

            // Превышен таймаут
            _approachTimer += dt;
            if (_approachTimer >= ApproachTimeout)
            {
                ResetClinchAttempt(withCooldown: false);
                return;
            }

            // Постоянно обновляем цель
            Agent.SetTargetAgent(_clinchTarget);

            //InformationManager.DisplayMessage(new InformationMessage($"{Agent.Name}: Сближаюсь с {_clinchTarget.Name} ({Agent.Position.Distance(_clinchTarget.Position):F1} м)"));
        }


        private void TryStartClinch()
        {
            if (Mission.Current.CurrentTime < _nextClinchTime) return;            

            if (_clinchTarget == null || !_clinchTarget.IsAgentCorrect()) return;
            if (IsUsingOtherAnim(Agent) || Agent.IsStaggered() || IsUsingOtherAnim(_clinchTarget)) return;

            float chance = CalculateClinchChance(_clinchTarget) * ModSettings.Instance.AIClinchChanceMultiplier;

            if (chance >= MBRandom.RandomInt(1, 100))
            {
                if (MasterStrikesBehavior.OnCanStartClinch != null && !MasterStrikesBehavior.OnCanStartClinch(Agent, _clinchTarget)) return;

                //InformationManager.DisplayMessage(new InformationMessage($"{Agent.Name}: Вхожу в клинч с {_clinchTarget.Name}! (Шанс: {chance:F0}%)", Color.ConvertStringToColor("#47ff4eff")));

                MasterStrikesBehavior.Instance._clinchAgents.Add(Agent, _clinchTarget);
                new ClinchLogic(Agent, _clinchTarget);
            }

            ResetClinchAttempt();
        }

        private bool IsUsingOtherAnim(Agent agent)
        {
            if (!agent.IsAgentCorrect()) return false;
            else return agent.IsBusy() || agent.IsInAnyMSAction() || agent.СurrentActionTypeIsKickOrBash();
        }

        private void ResetClinchAttempt(bool withCooldown = true)
        {
            _clinchTarget = null;
            _approachTimer = 0f;

            if (withCooldown)
                _nextClinchTime = Mission.Current.CurrentTime + ClinchCooldown;

            Agent.ClearTargetFrame();
        }

        private float CalculateClinchChance(Agent target)
        {
            if (!Agent.IsAgentCorrect() || !target.IsAgentCorrect()) return 0f;

            // Базовый шанс от навыка: 300/5 = 60% максимум
            float baseChance = Agent.GetSkillValueFromWeaponClass() / 5f;

            // Длина оружия: если у врага длиннее — бонус, но не до 50% сразу
            if (target.GetWeaponLength() > Agent.GetWeaponLength())
            {
                baseChance += (target.GetWeaponLength() - Agent.GetWeaponLength()) / 10f; // +10% за каждые 10 см разницы
            }

            // Численность: минус за каждого лишнего союзника цели
            if (target?.GetNearbyAgentsCountAtRadius(3f, ally: true) is int enemies && enemies > 1)
            {
                baseChance -= (enemies - 1) * 8f;
            }

            // Навык: минус, если наш навык ниже
            float mySkill = Agent.GetSkillValueFromWeaponClass();
            float enemySkill = target.GetSkillValueFromWeaponClass();
            if (enemySkill > mySkill)
            {
                baseChance -= (enemySkill - mySkill) / 15f;
            }

            // Здоровье
            float healthRatio = Agent.Health / Agent.HealthLimit;
            baseChance *= MathF.Lerp(0.3f, 1f, healthRatio);

            // Щит
            if (!Agent.HasShieldInHand() && target.HasShieldInHand())
                baseChance *= 0.7f;

            return MathF.Clamp(baseChance, 5f, 70f); // максимум 70%
        }


        private void AddLastAction(string actionName)
        {
            if (string.IsNullOrEmpty(actionName)) return;

            // Если такое же действие уже последнее в очереди — не дублируем
            if (_lastActions.Count > 0 && _lastActions.Last() == actionName)
                return;

            _lastActions.Enqueue(actionName);
            if (_lastActions.Count > MaxLastActions)
                _lastActions.Dequeue();
        }

        private bool WasRecentAction(string keyword)
        {
            foreach (var action in _lastActions)
            {
                if (action.Contains(keyword)) return true;
            }
            return false;
        }
    }
}