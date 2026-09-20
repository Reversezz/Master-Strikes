using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace MasterStrikes
{
    public class AIDodgeComponent(Agent agent) : AgentComponent(agent) // основной конструтор (прям в названии)
    {

        public static readonly ActionIndexCache[] act_2h_dodges =
        [
            ActionIndexCache.Create("act_2h_right_dodge"),
            ActionIndexCache.Create("act_2h_left_dodge"),
            ActionIndexCache.Create("act_2h_back_dodge")
        ];

        public static readonly ActionIndexCache[] act_1h_dodges =
        [
            ActionIndexCache.Create("act_1h_right_dodge"),
            ActionIndexCache.Create("act_1h_left_dodge"),
            ActionIndexCache.Create("act_1h_back_dodge")
        ];

        public static readonly ActionIndexCache[] act_other_2h_dodges =
        [
            ActionIndexCache.Create("act_other_2h_right_dodge"),
            ActionIndexCache.Create("act_other_2h_left_dodge"),
            ActionIndexCache.Create("act_other_2h_back_dodge")
        ];

        // ДЛЯ ТЕСТОВ НА АРЕНЕ
        //if (Agent.IsAgentHaveShield())
        //    if (Mission.Current.SceneName.Contains("arena") && Agent.WieldedOffhandWeapon.CurrentUsageItem?.IsShield == true)
        //    {
        //        Agent.DropItem(Agent.GetOffhandWieldedItemIndex());
        //    }

        private float _nextDodgeTime;
        private string AgentLastAction;

        public override void OnTick(float dt)
        {
            base.OnTick(dt);

            if (!ModSettings.Instance.EnableDodges) return;

            if (Agent.IsMainAgent) return;          
            if (!Agent.IsAgentCorrect() || Agent.Character.GetSkillValue(DefaultSkills.Athletics) < ModSettings.Instance.DodgeRequiredAthletics || Agent.MountAgent != null) return;
            if (Agent.IsStaggered()) return;
                
            Agent enemy = Agent.GetTargetAgent();
            if (!enemy.IsAgentCorrect() || enemy.MountAgent != null) return;
            if (enemy.Team == Agent.Team) return;


            // Если уже в каком-либо словаре
            if (Agent.IsBusy() || enemy.IsBusy()) return;
            

            if (Mission.Current.CurrentTime < _nextDodgeTime) return;
            _nextDodgeTime = Mission.Current.CurrentTime + Agent.GetDodgeCooldownByAge();
            
            // Если выполняет удар
            if (Agent.IsAttacking())
            {
                AgentLastAction = Agent.GetCurrentAction(1).GetName();
                return;
            }

            // Если уже выпоняют кастомную анимацию моего мода
            if (Agent.IsInMasterStrikeAction() || Agent.IsInClinchAction()) 
            { 
                AgentLastAction = Agent.GetCurrentAction(0).GetName(); 
                return;
            }

            if (!(Agent.IsDodging() || Agent.СurrentActionTypeIsKickOrBash()) && AgentLastAction != null && (AgentLastAction.Contains("release") || AgentLastAction.Contains("masterstrike") || AgentLastAction.Contains("clinch")))
                AIDodgeLogic(enemy);
        }
        


        private void AIDodgeLogic(Agent enemy)
        {
            if (Agent.Position.Distance(enemy.Position) > 2.44f) return;
            if (!Agent.IsBeingAttacked(enemy)) return;

            int AgentCurrentNearbyEnemies = Agent.GetNearbyAgentsCountAtRadius(radius: 3.5f, enemy: true);
            int AgentCurrentNearbyAllies = Agent.GetNearbyAgentsCountAtRadius(radius: 3.5f, ally: true);

            // Проверка оружия
            bool isTwoHandedSword = Agent.UseWeaponClass(WeaponClass.TwoHandedSword);
            bool isOneHanded = Agent.HasOneHandedWeapon() || Agent.UseWeaponClass(WeaponClass.Javelin)       || Agent.UseWeaponClass(WeaponClass.ThrowingAxe)
                                                          || Agent.UseWeaponClass(WeaponClass.ThrowingKnife) || Agent.UseWeaponClass(WeaponClass.Bow);
            bool isOtherTwoHanded = Agent.HasTwoHandedWeapon() && !Agent.UseWeaponClass(WeaponClass.TwoHandedSword) || Agent.UseWeaponClass(WeaponClass.Crossbow);

            // Например 300 / 4 = 75% шанс уворота
            if ((Agent.Character.GetSkillValue(DefaultSkills.Athletics) / 4) * ModSettings.Instance.AIDodgeChanceMultiplier >= MBRandom.RandomInt(1, 100))
            {
                if (Agent.IsAttacking()) return;

                if (AgentCurrentNearbyEnemies >= 3 && AgentCurrentNearbyAllies < 3)
                {
                    string backDodgeName = (isTwoHandedSword) ? "act_2h_back_dodge" : (isOtherTwoHanded) ? "act_other_2h_back_dodge" : (isOneHanded) ? "act_1h_back_dodge" : null;
                    if (backDodgeName == null) return;

                    AgentLastAction = backDodgeName;

                    if (MasterStrikesBehavior.OnCanDodge != null && !MasterStrikesBehavior.OnCanDodge(Agent)) return;
                    MasterStrikesBehavior.OnDodgeStarted?.Invoke(Agent);

                    if (ModSettings.Instance.HighlightDodges)
                        Gags.HighlightAgent(Agent, Gags.Action.Dodge);

                    Agent.SetActionChannel(0, ActionIndexCache.Create(backDodgeName), false, 0UL, 0f, AgentExtensions.CalculateActionSpeed(Agent, AgentExtensions.MasterStrikesAction.Dodge), -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                    Agent.MakeVoice(SkinVoiceManager.VoiceType.Jump, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);

                    //InformationManager.DisplayMessage(new InformationMessage($"{Agent.Name} увернулся от толпы: {Agent.Position.Distance(Agent.GetTargetAgent()?.Position ?? Vec3.Invalid)} м"));
                }
                else
                {
                    if (!enemy.WieldedWeapon.IsEmpty && enemy.AttackDirection >= Agent.UsageDirection.AttackBegin && enemy.AttackDirection < Agent.UsageDirection.AttackEnd)
                    {
                        ActionIndexCache[] dodgeArray = (isTwoHandedSword) ? act_2h_dodges : (isOtherTwoHanded) ? act_other_2h_dodges : (isOneHanded) ? act_1h_dodges : null;
                        if (dodgeArray == null) return;                       

                        ActionIndexCache action;
                        

                        // В блоке выбора доджа
                        if (enemy.AttackDirection == Agent.UsageDirection.AttackRight)
                            action = Agent.Character.GetSkillValue(DefaultSkills.Athletics) / 4 >= MBRandom.RandomInt(1, 100) ? ActionIndexCache.Create(isTwoHandedSword ? "act_2h_left_dodge" 
                                                                                                                                                      : isOtherTwoHanded ? "act_other_2h_left_dodge" 
                                                                                                                                                                         : "act_1h_left_dodge")
                                                                                                                              : dodgeArray[MBRandom.RandomInt(dodgeArray.Length)];

                        else if (enemy.AttackDirection == Agent.UsageDirection.AttackLeft)
                            action = Agent.Character.GetSkillValue(DefaultSkills.Athletics) / 4 >= MBRandom.RandomInt(1, 100) ? ActionIndexCache.Create(isTwoHandedSword ? "act_2h_right_dodge"
                                                                                                                                                      : isOtherTwoHanded ? "act_other_2h_right_dodge"
                                                                                                                                                                         : "act_1h_right_dodge")
                                                                                                                              : dodgeArray[MBRandom.RandomInt(dodgeArray.Length)];

                        else // if (enemy.AttackDirection == Agent.UsageDirection.AttackDown || enemy.AttackDirection == Agent.UsageDirection.AttackUp)
                            action = Agent.Character.GetSkillValue(DefaultSkills.Athletics) / 4 >= MBRandom.RandomInt(1, 100) ? ActionIndexCache.Create(isTwoHandedSword ? "act_2h_back_dodge" 
                                                                                                                                                      : isOtherTwoHanded ? "act_other_2h_back_dodge"
                                                                                                                                                                         : "act_1h_back_dodge")
                                                                                                                              : dodgeArray[MBRandom.RandomInt(dodgeArray.Length)];
                        
                        AgentLastAction = action.GetName();

                        if (MasterStrikesBehavior.OnCanDodge != null && !MasterStrikesBehavior.OnCanDodge(Agent)) return;
                        MasterStrikesBehavior.OnDodgeStarted?.Invoke(Agent);

                        if (ModSettings.Instance.HighlightDodges)
                            Gags.HighlightAgent(Agent, Gags.Action.Dodge);

                        Agent.SetActionChannel(0, action, false, 0UL, 0f, AgentExtensions.CalculateActionSpeed(Agent, AgentExtensions.MasterStrikesAction.Dodge), -0.2f, 0.4f, 0f, false, -0.2f, 0, true);
                        Agent.MakeVoice(SkinVoiceManager.VoiceType.Jump, SkinVoiceManager.CombatVoiceNetworkPredictionType.OwnerPrediction);
                    }
                }
            }
        }
    }
}