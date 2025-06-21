using System;
using System.Collections.Generic;
using System.Text;
using Server;

namespace Server.Mobiles
{
    public class PlayerBotAI : BaseAI
    {
        public PlayerBotAI(BaseCreature m) : base(m)
        {
        }

        public override bool DoActionWander()
        {
            m_Mobile.DebugSay("I have no combatant");

            if (AquireFocusMob(m_Mobile.RangePerception, m_Mobile.FightMode, false, false, true))
            {
                m_Mobile.DebugSay("I have detected {0}, attacking", m_Mobile.FocusMob.Name);
                m_Mobile.Combatant = m_Mobile.FocusMob;
                Action = ActionType.Combat;
            }
            else
            {
                base.DoActionWander();
            }

            return true;
        }

        public override bool DoActionCombat()
        {
            Mobile combatant = m_Mobile.Combatant;

            if (combatant == null || combatant.Deleted || combatant.Map != m_Mobile.Map || !combatant.Alive)
            {
                m_Mobile.DebugSay("My combatant is gone, so my guard is up");

                Action = ActionType.Wander;//.Guard;

                return true;
            }

            /*if ( !m_Mobile.InLOS( combatant ) )
			{
				if ( AquireFocusMob( m_Mobile.RangePerception, m_Mobile.FightMode, false, false, true ) )
				{
					m_Mobile.Combatant = combatant = m_Mobile.FocusMob;
					m_Mobile.FocusMob = null;
				}
			}*/

            if (MoveTo(combatant, true, m_Mobile.RangeFight))
            {
                if (Utility.RandomDouble() <= 0.25 || !m_Mobile.InRange(combatant, m_Mobile.RangeFight))
                    m_Mobile.Direction = m_Mobile.GetDirectionTo(combatant);
            }
            else if (AquireFocusMob(m_Mobile.RangePerception, m_Mobile.FightMode, false, false, true))
            {
                m_Mobile.DebugSay("My move is blocked, so I am going to attack {0}", m_Mobile.FocusMob.Name);

                m_Mobile.Combatant = m_Mobile.FocusMob;
                Action = ActionType.Combat;

                return true;
            }
            else if (m_Mobile.GetDistanceToSqrt(combatant) > m_Mobile.RangePerception + 1)
            {
                m_Mobile.DebugSay("I cannot find {0}, so my guard is up", combatant.Name);

                Action = ActionType.Wander;

                return true;
            }
            else
            {
                m_Mobile.DebugSay("I should be closer to {0}", combatant.Name);
            }

            if (m_Mobile.CheckFlee())
            {
                // We are low on health, should we flee?

                bool flee = false;

                if (m_Mobile.Hits < combatant.Hits)
                {
                    // We are more hurt than them

                    int diff = combatant.Hits - m_Mobile.Hits;

                    flee = (Utility.Random(0, 100) < (10 + diff)); // (10 + diff)% chance to flee
                }
                else
                {
                    flee = Utility.Random(0, 100) < 10; // 10% chance to flee
                }

                if (flee)
                {
                    m_Mobile.DebugSay("I am going to flee from {0}", combatant.Name);
                    Action = ActionType.Flee;
                }
            }

            return true;
        }

        public override bool DoActionGuard()
        {
            if (AquireFocusMob(m_Mobile.RangePerception, m_Mobile.FightMode, false, false, true))
            {
                m_Mobile.DebugSay("I have detected {0}, attacking", m_Mobile.FocusMob.Name);
                m_Mobile.Combatant = m_Mobile.FocusMob;
                Action = ActionType.Combat;
            }
            else
            {
                base.DoActionGuard();
            }

            return true;
        }
        public override bool DoActionFlee()
        {
            if (m_Mobile.Hits > m_Mobile.HitsMax / 2)
            {
                m_Mobile.DebugSay("I am stronger now, so I will wander");
                Action = ActionType.Wander;
            }
            else
            {
                m_Mobile.FocusMob = m_Mobile.Combatant;
                base.DoActionFlee();
            }

            return true;
        }

        // Override AquireFocusMob to make PlayerKillers specifically target good-aligned players/NPCs
        // and good-aligned PlayerBots seek out PlayerKillers based on their relative power
        public override bool AquireFocusMob(int iRange, FightMode acqType, bool bPlayerOnly, bool bFacFriend, bool bFacFoe)
        {
            // Check if this is a PlayerBot
            PlayerBot playerBot = m_Mobile as PlayerBot;
            if (playerBot != null)
            {
                // PlayerKillers target good-aligned players/NPCs
                if (playerBot.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.PlayerKiller)
                {
                    return AquireGoodAlignedTarget(iRange, bPlayerOnly);
                }
                // Good-aligned PlayerBots seek out PlayerKillers
                else if (IsGoodAlignedPlayerBot(playerBot))
                {
                    return AquirePlayerKillerTarget(iRange, bPlayerOnly, playerBot);
                }
            }

            // Use default behavior for non-PlayerBots or neutral PlayerBots
            return base.AquireFocusMob(iRange, acqType, bPlayerOnly, bFacFriend, bFacFoe);
        }

        private bool IsGoodAlignedPlayerBot(PlayerBot playerBot)
        {
            // Good-aligned PlayerBots have positive karma
            return playerBot.Karma > 0;
        }

        private bool AquirePlayerKillerTarget(int iRange, bool bPlayerOnly, PlayerBot goodBot)
        {
            if (m_Mobile.Deleted)
                return false;

            if (m_Mobile.Controled)
            {
                if (m_Mobile.ControlTarget == null || m_Mobile.ControlTarget.Deleted || !m_Mobile.ControlTarget.Alive || m_Mobile.ControlTarget.IsDeadBondedPet || !m_Mobile.InRange(m_Mobile.ControlTarget, m_Mobile.RangePerception * 2))
                {
                    m_Mobile.FocusMob = null;
                    return false;
                }
                else
                {
                    m_Mobile.FocusMob = m_Mobile.ControlTarget;
                    return (m_Mobile.FocusMob != null);
                }
            }

            if (m_Mobile.ConstantFocus != null)
            {
                m_Mobile.FocusMob = m_Mobile.ConstantFocus;
                return true;
            }

            if (m_Mobile.NextReaquireTime > DateTime.Now)
            {
                m_Mobile.FocusMob = null;
                return false;
            }

            m_Mobile.NextReaquireTime = DateTime.Now + m_Mobile.ReaquireDelay;

            Map map = m_Mobile.Map;

            if (map != null)
            {
                Mobile newFocusMob = null;
                double val = double.MinValue;

                IPooledEnumerable eable = map.GetMobilesInRange(m_Mobile.Location, iRange);

                foreach (Mobile m in eable)
                {
                    bool bCheckIt = false;

                    // Basic check - must be alive, not blessed, not deleted, not self, and visible
                    if ((m.Player || !bPlayerOnly) && m.AccessLevel == AccessLevel.Player && m.Alive && !m.Blessed && !m.Deleted && m != m_Mobile && m_Mobile.CanSee(m))
                    {
                        // For good-aligned PlayerBots, specifically target PlayerKillers
                        if (IsPlayerKillerTarget(m, goodBot))
                        {
                            bCheckIt = true;
                        }
                    }

                    if (bCheckIt && !m.IsDeadBondedPet)
                    {
                        double theirVal = m_Mobile.GetValueFrom(m, FightMode.Closest, bPlayerOnly);

                        if (theirVal > val && m_Mobile.InLOS(m))
                        {
                            newFocusMob = m;
                            val = theirVal;
                        }
                    }
                }

                eable.Free();

                m_Mobile.FocusMob = newFocusMob;
            }

            return (m_Mobile.FocusMob != null);
        }

        private bool IsPlayerKillerTarget(Mobile target, PlayerBot goodBot)
        {
            // Check if target is a PlayerKiller (negative karma)
            if (target.Karma < 0)
            {
                // Calculate relative power between the good bot and the PK
                double relativePower = CalculateRelativePower(goodBot, target);
                
                // Determine aggression level based on relative power and bot's karma level
                double aggressionChance = CalculateAggressionChance(goodBot, target, relativePower);
                
                // Roll for aggression
                return Utility.RandomDouble() < aggressionChance;
            }

            return false;
        }

        private double CalculateRelativePower(PlayerBot goodBot, Mobile target)
        {
            // Calculate power based on stats, skills, and experience
            double goodBotPower = CalculateBotPower(goodBot);
            double targetPower = CalculateTargetPower(target);
            
            // Return relative power (positive = good bot is stronger, negative = target is stronger)
            return goodBotPower - targetPower;
        }

        private double CalculateBotPower(PlayerBot bot)
        {
            double power = 0;
            
            // Base stats
            power += bot.Str * 0.5;
            power += bot.Dex * 0.3;
            power += bot.Int * 0.2;
            
            // Combat skills
            power += bot.Skills[SkillName.Tactics].Base * 0.8;
            power += bot.Skills[SkillName.MagicResist].Base * 0.6;
            power += bot.Skills[SkillName.Parry].Base * 0.4;
            
            // Primary combat skill
            if (bot.PreferedCombatSkill != SkillName.Wrestling)
            {
                power += bot.Skills[bot.PreferedCombatSkill].Base * 1.0;
            }
            
            // Experience multiplier
            switch (bot.PlayerBotExperience)
            {
                case PlayerBotPersona.PlayerBotExperience.Newbie:
                    power *= 0.6;
                    break;
                case PlayerBotPersona.PlayerBotExperience.Average:
                    power *= 0.8;
                    break;
                case PlayerBotPersona.PlayerBotExperience.Proficient:
                    power *= 1.0;
                    break;
                case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                    power *= 1.3;
                    break;
            }
            
            // Karma bonus (more virtuous = more courageous)
            if (bot.Karma > 50)
                power *= 1.2;
            else if (bot.Karma > 20)
                power *= 1.1;
            
            return power;
        }

        private double CalculateTargetPower(Mobile target)
        {
            double power = 0;
            
            // Base stats
            power += target.Str * 0.5;
            power += target.Dex * 0.3;
            power += target.Int * 0.2;
            
            // If it's a PlayerBot, use more detailed calculation
            PlayerBot targetBot = target as PlayerBot;
            if (targetBot != null)
            {
                // Combat skills
                power += targetBot.Skills[SkillName.Tactics].Base * 0.8;
                power += targetBot.Skills[SkillName.MagicResist].Base * 0.6;
                power += targetBot.Skills[SkillName.Parry].Base * 0.4;
                
                // Primary combat skill
                if (targetBot.PreferedCombatSkill != SkillName.Wrestling)
                {
                    power += targetBot.Skills[targetBot.PreferedCombatSkill].Base * 1.0;
                }
                
                // Experience multiplier
                switch (targetBot.PlayerBotExperience)
                {
                    case PlayerBotPersona.PlayerBotExperience.Newbie:
                        power *= 0.6;
                        break;
                    case PlayerBotPersona.PlayerBotExperience.Average:
                        power *= 0.8;
                        break;
                    case PlayerBotPersona.PlayerBotExperience.Proficient:
                        power *= 1.0;
                        break;
                    case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                        power *= 1.3;
                        break;
                }
                
                // PK experience bonus (PKs are more dangerous)
                if (targetBot.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.PlayerKiller)
                    power *= 1.2;
            }
            else
            {
                // For regular players, estimate based on visible stats
                power += target.Hits * 0.1;
                power += target.Mana * 0.05;
                power += target.Stam * 0.05;
            }
            
            return power;
        }

        private double CalculateAggressionChance(PlayerBot goodBot, Mobile target, double relativePower)
        {
            double baseChance = 0.3; // 30% base chance to engage
            
            // Power difference modifier
            if (relativePower > 50)
            {
                // Much stronger than target - very aggressive
                baseChance = 0.9;
            }
            else if (relativePower > 20)
            {
                // Stronger than target - aggressive
                baseChance = 0.7;
            }
            else if (relativePower > 0)
            {
                // Slightly stronger - moderately aggressive
                baseChance = 0.5;
            }
            else if (relativePower > -20)
            {
                // Slightly weaker - cautious
                baseChance = 0.3;
            }
            else if (relativePower > -50)
            {
                // Weaker - very cautious
                baseChance = 0.1;
            }
            else
            {
                // Much weaker - extremely cautious
                baseChance = 0.05;
            }
            
            // Karma modifier (more virtuous = more courageous)
            if (goodBot.Karma > 80)
                baseChance *= 1.3; // Very honorable - more aggressive
            else if (goodBot.Karma > 50)
                baseChance *= 1.2; // Honorable - more aggressive
            else if (goodBot.Karma > 20)
                baseChance *= 1.1; // Somewhat honorable - slightly more aggressive
            
            // Experience modifier
            switch (goodBot.PlayerBotExperience)
            {
                case PlayerBotPersona.PlayerBotExperience.Newbie:
                    baseChance *= 0.7; // Newbies are more cautious
                    break;
                case PlayerBotPersona.PlayerBotExperience.Average:
                    baseChance *= 0.85; // Average players are somewhat cautious
                    break;
                case PlayerBotPersona.PlayerBotExperience.Proficient:
                    baseChance *= 1.0; // Proficient players are balanced
                    break;
                case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                    baseChance *= 1.2; // Grandmasters are more confident
                    break;
            }
            
            // Profile modifier
            switch (goodBot.PlayerBotProfile)
            {
                case PlayerBotPersona.PlayerBotProfile.Adventurer:
                    baseChance *= 1.1; // Adventurers are more likely to engage
                    break;
                case PlayerBotPersona.PlayerBotProfile.Crafter:
                    baseChance *= 0.8; // Crafters are more cautious
                    break;
                case PlayerBotPersona.PlayerBotProfile.PlayerKiller:
                    baseChance *= 0.0; // This shouldn't happen, but just in case
                    break;
            }
            
            // Cap the chance between 0.05 and 0.95
            return Math.Max(0.05, Math.Min(0.95, baseChance));
        }

        private bool AquireGoodAlignedTarget(int iRange, bool bPlayerOnly)
        {
            if (m_Mobile.Deleted)
                return false;

            if (m_Mobile.Controled)
            {
                if (m_Mobile.ControlTarget == null || m_Mobile.ControlTarget.Deleted || !m_Mobile.ControlTarget.Alive || m_Mobile.ControlTarget.IsDeadBondedPet || !m_Mobile.InRange(m_Mobile.ControlTarget, m_Mobile.RangePerception * 2))
                {
                    m_Mobile.FocusMob = null;
                    return false;
                }
                else
                {
                    m_Mobile.FocusMob = m_Mobile.ControlTarget;
                    return (m_Mobile.FocusMob != null);
                }
            }

            if (m_Mobile.ConstantFocus != null)
            {
                m_Mobile.FocusMob = m_Mobile.ConstantFocus;
                return true;
            }

            if (m_Mobile.NextReaquireTime > DateTime.Now)
            {
                m_Mobile.FocusMob = null;
                return false;
            }

            m_Mobile.NextReaquireTime = DateTime.Now + m_Mobile.ReaquireDelay;

            Map map = m_Mobile.Map;

            if (map != null)
            {
                Mobile newFocusMob = null;
                double val = double.MinValue;

                IPooledEnumerable eable = map.GetMobilesInRange(m_Mobile.Location, iRange);

                foreach (Mobile m in eable)
                {
                    bool bCheckIt = false;

                    // Basic check - must be alive, not blessed, not deleted, not self, and visible
                    if ((m.Player || !bPlayerOnly) && m.AccessLevel == AccessLevel.Player && m.Alive && !m.Blessed && !m.Deleted && m != m_Mobile && m_Mobile.CanSee(m))
                    {
                        // For PlayerKillers, specifically target good-aligned targets
                        if (IsGoodAlignedTarget(m))
                        {
                            bCheckIt = true;
                        }
                    }

                    if (bCheckIt && !m.IsDeadBondedPet)
                    {
                        double theirVal = m_Mobile.GetValueFrom(m, FightMode.Closest, bPlayerOnly);

                        if (theirVal > val && m_Mobile.InLOS(m))
                        {
                            newFocusMob = m;
                            val = theirVal;
                        }
                    }
                }

                eable.Free();

                m_Mobile.FocusMob = newFocusMob;
            }

            return (m_Mobile.FocusMob != null);
        }

        private bool IsGoodAlignedTarget(Mobile target)
        {
            // Check if target is good-aligned based on karma
            // Good-aligned targets have positive karma (Honorable, Noble, Lord/Lady, etc.)
            if (target.Karma > 0)
            {
                // Prioritize higher karma targets (more "good" = more valuable prey)
                return true;
            }

            // Also target neutral players (karma = 0) as they're not evil
            if (target.Karma == 0)
            {
                return true;
            }

            // Don't target other evil players (negative karma) unless they're much less evil
            // This prevents PlayerKillers from fighting each other unless there's a significant karma difference
            if (target.Karma < 0 && m_Mobile.Karma < target.Karma)
            {
                // Only target if the target is less evil than the PlayerKiller
                // This creates a hierarchy where more evil PKs hunt less evil ones
                return true;
            }

            return false;
        }
    }
}
