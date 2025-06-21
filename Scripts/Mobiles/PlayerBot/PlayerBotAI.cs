using System;
using System.Collections.Generic;
using System.Text;
using Server;
using Server.Spells;

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

            // Check if we should cast a spell
            if (ShouldCastSpell(combatant))
            {
                if (CastCombatSpell(combatant))
                {
                    return true; // Spell was cast, continue next tick
                }
            }

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
                // First, check if there's a combat situation we should assist with
                Mobile assistTarget = CheckForCombatAssistance(iRange, playerBot);
                if (assistTarget != null)
                {
                    m_Mobile.FocusMob = assistTarget;
                    return true;
                }

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

        private Mobile CheckForCombatAssistance(int iRange, PlayerBot observerBot)
        {
            if (m_Mobile.Deleted || m_Mobile.Controled || m_Mobile.ConstantFocus != null)
                return null;

            Map map = m_Mobile.Map;
            if (map == null)
                return null;

            Mobile bestAssistTarget = null;
            double bestAssistValue = 0;

            IPooledEnumerable eable = map.GetMobilesInRange(m_Mobile.Location, iRange);

            foreach (Mobile m in eable)
            {
                // Skip if not a valid target for assistance
                if (!IsValidAssistanceTarget(m, observerBot))
                    continue;

                // Check if this mobile is in combat
                if (IsInCombat(m))
                {
                    double assistValue = CalculateAssistanceValue(m, observerBot);
                    
                    if (assistValue > bestAssistValue)
                    {
                        bestAssistValue = assistValue;
                        bestAssistTarget = m;
                    }
                }
            }

            eable.Free();

            // Only assist if the value is high enough (don't assist in minor skirmishes)
            if (bestAssistValue > 50)
            {
                // Find the enemy of the ally we want to assist
                Mobile enemyToTarget = FindEnemyOfAlly(bestAssistTarget, observerBot);
                if (enemyToTarget != null)
                {
                    m_Mobile.DebugSay("I see combat! I will assist {0} by attacking {1}!", bestAssistTarget.Name, enemyToTarget.Name);
                    return enemyToTarget; // Return the enemy to attack, not the ally
                }
            }

            return null;
        }

        private Mobile FindEnemyOfAlly(Mobile ally, PlayerBot observerBot)
        {
            // Priority 1: Current combatant
            if (ally.Combatant != null && ally.Combatant.Alive && !ally.Combatant.Deleted && 
                IsValidEnemyTarget(ally.Combatant, observerBot))
            {
                return ally.Combatant;
            }

            // Priority 2: Recent aggressors
            foreach (AggressorInfo info in ally.Aggressors)
            {
                if (info.Attacker != null && info.Attacker.Alive && !info.Attacker.Deleted && 
                    IsValidEnemyTarget(info.Attacker, observerBot))
                {
                    return info.Attacker;
                }
            }

            // Priority 3: If ally is a PlayerBot, check its combatant
            PlayerBot allyBot = ally as PlayerBot;
            if (allyBot != null && allyBot.Combatant != null && allyBot.Combatant.Alive && 
                !allyBot.Combatant.Deleted && IsValidEnemyTarget(allyBot.Combatant, observerBot))
            {
                return allyBot.Combatant;
            }

            return null;
        }

        private bool IsValidEnemyTarget(Mobile target, PlayerBot observerBot)
        {
            // Basic checks
            if (target == null || target.Deleted || target == m_Mobile || 
                target.AccessLevel != AccessLevel.Player || !target.Alive || 
                target.Blessed || !m_Mobile.CanSee(target) || target.IsDeadBondedPet)
                return false;

            // Must be a PlayerBot or Player
            if (!(target is PlayerBot) && !target.Player)
                return false;

            // Check if this target is an enemy based on alignment
            bool targetIsGood = IsGoodAligned(target);
            bool observerIsGood = IsGoodAlignedPlayerBot(observerBot);

            // Good bots target evil enemies, PKs target good enemies
            return (targetIsGood && !observerIsGood) || (!targetIsGood && observerIsGood);
        }

        private bool IsValidAssistanceTarget(Mobile target, PlayerBot observerBot)
        {
            // Basic checks
            if (target == null || target.Deleted || target == m_Mobile || 
                target.AccessLevel != AccessLevel.Player || !target.Alive || 
                target.Blessed || !m_Mobile.CanSee(target) || target.IsDeadBondedPet)
                return false;

            // Must be a PlayerBot or Player for assistance
            if (!(target is PlayerBot) && !target.Player)
                return false;

            // Check if we should assist this target based on alignment
            return ShouldAssistTarget(target, observerBot);
        }

        private bool ShouldAssistTarget(Mobile target, PlayerBot observerBot)
        {
            // Determine target alignment
            bool targetIsGood = IsGoodAligned(target);
            bool observerIsGood = IsGoodAlignedPlayerBot(observerBot);

            // Good bots assist good targets, PKs assist PKs
            return (targetIsGood && observerIsGood) || (!targetIsGood && !observerIsGood);
        }

        private bool IsGoodAligned(Mobile mobile)
        {
            // Check if mobile is good-aligned (positive karma)
            return mobile.Karma > 0;
        }

        private bool IsInCombat(Mobile mobile)
        {
            // Check if mobile is currently in combat
            if (mobile.Combatant != null && mobile.Combatant.Alive && !mobile.Combatant.Deleted)
                return true;

            // Check if mobile has recent aggressors or is aggressed
            if (mobile.Aggressors.Count > 0 || mobile.Aggressed.Count > 0)
                return true;

            // Check if mobile is a PlayerBot and has a combatant
            PlayerBot bot = mobile as PlayerBot;
            if (bot != null && bot.Combatant != null && bot.Combatant.Alive && !bot.Combatant.Deleted)
                return true;

            return false;
        }

        private double CalculateAssistanceValue(Mobile target, PlayerBot observerBot)
        {
            double value = 0;

            // Base value for being in combat
            value += 30;

            // Distance modifier (closer = higher priority)
            double distance = m_Mobile.GetDistanceToSqrt(target);
            if (distance <= 3)
                value += 40; // Very close - high priority
            else if (distance <= 6)
                value += 25; // Close - medium priority
            else if (distance <= 10)
                value += 10; // Medium distance - lower priority

            // Check if target is losing the fight
            if (IsTargetLosing(target))
                value += 30; // Higher priority to help losing allies

            // Check if target is fighting multiple enemies
            int enemyCount = CountEnemies(target);
            if (enemyCount > 1)
                value += (enemyCount * 15); // More enemies = higher priority

            // Alignment strength modifier
            if (IsGoodAlignedPlayerBot(observerBot))
            {
                // Good bots are more likely to help other good bots
                if (target.Karma > 50)
                    value += 20; // Very honorable - worth helping
                else if (target.Karma > 20)
                    value += 15; // Honorable - worth helping
            }
            else
            {
                // PKs are more likely to help other PKs
                if (target.Karma < -100)
                    value += 20; // Very evil - worth helping
                else if (target.Karma < -50)
                    value += 15; // Evil - worth helping
            }

            // Experience modifier
            PlayerBot targetBot = target as PlayerBot;
            if (targetBot != null)
            {
                switch (targetBot.PlayerBotExperience)
                {
                    case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                        value += 25; // Grandmasters are valuable allies
                        break;
                    case PlayerBotPersona.PlayerBotExperience.Proficient:
                        value += 15; // Proficient bots are good allies
                        break;
                    case PlayerBotPersona.PlayerBotExperience.Average:
                        value += 10; // Average bots are decent allies
                        break;
                    case PlayerBotPersona.PlayerBotExperience.Newbie:
                        value += 5; // Newbies are still worth helping
                        break;
                }
            }

            // Observer's personality modifier
            switch (observerBot.PlayerBotProfile)
            {
                case PlayerBotPersona.PlayerBotProfile.Adventurer:
                    value += 15; // Adventurers are more likely to help
                    break;
                case PlayerBotPersona.PlayerBotProfile.Crafter:
                    value += 5; // Crafters are less likely to help
                    break;
                case PlayerBotPersona.PlayerBotProfile.PlayerKiller:
                    value += 20; // PKs are very likely to help other PKs
                    break;
            }

            // Observer's experience modifier
            switch (observerBot.PlayerBotExperience)
            {
                case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                    value += 10; // Grandmasters are more confident to help
                    break;
                case PlayerBotPersona.PlayerBotExperience.Proficient:
                    value += 5; // Proficient bots are confident to help
                    break;
                case PlayerBotPersona.PlayerBotExperience.Average:
                    value += 0; // Average bots are neutral about helping
                    break;
                case PlayerBotPersona.PlayerBotExperience.Newbie:
                    value -= 10; // Newbies are less likely to help
                    break;
            }

            return value;
        }

        private bool IsTargetLosing(Mobile target)
        {
            // Check if target is losing the fight
            double healthPercentage = (double)target.Hits / target.HitsMax;
            
            if (healthPercentage < 0.3)
                return true; // Below 30% health - definitely losing

            // Check if target is fighting someone with much more health
            if (target.Combatant != null && target.Combatant.Alive)
            {
                double enemyHealthPercentage = (double)target.Combatant.Hits / target.Combatant.HitsMax;
                if (healthPercentage < enemyHealthPercentage * 0.7)
                    return true; // Target has significantly less health than enemy
            }

            return false;
        }

        private int CountEnemies(Mobile target)
        {
            int enemyCount = 0;

            // Count current combatant
            if (target.Combatant != null && target.Combatant.Alive && !target.Combatant.Deleted)
                enemyCount++;

            // Count aggressors
            foreach (AggressorInfo info in target.Aggressors)
            {
                if (info.Attacker != null && info.Attacker.Alive && !info.Attacker.Deleted)
                    enemyCount++;
            }

            // Count if target is a PlayerBot with multiple enemies
            PlayerBot targetBot = target as PlayerBot;
            if (targetBot != null && targetBot.Combatant != null && targetBot.Combatant.Alive && !targetBot.Combatant.Deleted)
            {
                // Don't double-count the combatant
                if (targetBot.Combatant != target.Combatant)
                    enemyCount++;
            }

            return enemyCount;
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

        private bool ShouldCastSpell(Mobile target)
        {
            // Check if this is a PlayerBot with magic skills
            PlayerBot playerBot = m_Mobile as PlayerBot;
            if (playerBot == null)
                return false;

            // Check if we have sufficient Magery skill
            double magerySkill = playerBot.Skills[SkillName.Magery].Base;
            if (magerySkill < 20.0) // Minimum skill to cast spells
                return false;

            // Check if we're not already casting a spell
            if (playerBot.Spell != null && playerBot.Spell.IsCasting)
                return false;

            // Check if we have enough mana
            if (playerBot.Mana < 10) // Minimum mana requirement
                return false;

            // Check if we're not paralyzed or frozen
            if (playerBot.Paralyzed || playerBot.Frozen)
                return false;

            // Determine spell casting frequency based on skill level
            double castChance = 0.0;
            
            if (magerySkill >= 80.0)
                castChance = 0.4; // 40% chance for grandmasters
            else if (magerySkill >= 60.0)
                castChance = 0.3; // 30% chance for proficient mages
            else if (magerySkill >= 40.0)
                castChance = 0.2; // 20% chance for average mages
            else if (magerySkill >= 20.0)
                castChance = 0.1; // 10% chance for novice mages

            // Increase chance if we're in danger
            if (playerBot.Hits < playerBot.HitsMax * 0.3)
                castChance *= 1.5; // More likely to cast when hurt

            // Increase chance if target is strong
            if (target.Hits > playerBot.Hits)
                castChance *= 1.3; // More likely to cast against stronger enemies

            return Utility.RandomDouble() < castChance;
        }

        private bool CastCombatSpell(Mobile target)
        {
            PlayerBot playerBot = m_Mobile as PlayerBot;
            if (playerBot == null)
                return false;

            double magerySkill = playerBot.Skills[SkillName.Magery].Base;
            
            // Determine what type of spell to cast based on situation
            SpellType spellType = DetermineSpellType(playerBot, target);
            
            // Cast the appropriate spell
            switch (spellType)
            {
                case SpellType.Offensive:
                    return CastOffensiveSpell(playerBot, target, magerySkill);
                case SpellType.Defensive:
                    return CastDefensiveSpell(playerBot, magerySkill);
                case SpellType.Utility:
                    return CastUtilitySpell(playerBot, target, magerySkill);
                default:
                    return false;
            }
        }

        private enum SpellType
        {
            Offensive,
            Defensive,
            Utility
        }

        private SpellType DetermineSpellType(PlayerBot playerBot, Mobile target)
        {
            double healthPercentage = (double)playerBot.Hits / playerBot.HitsMax;
            double targetHealthPercentage = (double)target.Hits / target.HitsMax;
            double distance = playerBot.GetDistanceToSqrt(target);

            // Defensive spells when health is low
            if (healthPercentage < 0.3)
            {
                if (Utility.RandomDouble() < 0.7) // 70% chance for defensive when hurt
                    return SpellType.Defensive;
            }

            // Utility spells for positioning or escape
            if (distance > 8 && healthPercentage < 0.5)
            {
                if (Utility.RandomDouble() < 0.4) // 40% chance for utility when far and hurt
                    return SpellType.Utility;
            }

            // Default to offensive
            return SpellType.Offensive;
        }

        private bool CastOffensiveSpell(PlayerBot playerBot, Mobile target, double magerySkill)
        {
            // Determine spell circle based on skill level
            SpellCircle maxCircle = DetermineMaxSpellCircle(magerySkill);
            
            // Select appropriate offensive spell
            Spell spell = null;
            
            if (maxCircle >= SpellCircle.Eighth)
            {
                // Grandmaster spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Eighth.EnergyVortexSpell(playerBot, null),
                    new Server.Spells.Eighth.EarthquakeSpell(playerBot, null),
                    new Server.Spells.Eighth.FireElementalSpell(playerBot, null),
                    new Server.Spells.Eighth.SummonDaemonSpell(playerBot, null)
                });
            }
            else if (maxCircle >= SpellCircle.Seventh)
            {
                // Seventh circle spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Seventh.FlameStrikeSpell(playerBot, null),
                    new Server.Spells.Seventh.MeteorSwarmSpell(playerBot, null),
                    new Server.Spells.Seventh.ChainLightningSpell(playerBot, null),
                    new Server.Spells.Seventh.ManaVampireSpell(playerBot, null)
                });
            }
            else if (maxCircle >= SpellCircle.Sixth)
            {
                // Sixth circle spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Sixth.DispelSpell(playerBot, null),
                    new Server.Spells.Sixth.ExplosionSpell(playerBot, null),
                    new Server.Spells.Sixth.EnergyBoltSpell(playerBot, null),
                    new Server.Spells.Sixth.ParalyzeFieldSpell(playerBot, null)
                });
            }
            else if (maxCircle >= SpellCircle.Fifth)
            {
                // Fifth circle spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Fifth.BladeSpiritsSpell(playerBot, null),
                    new Server.Spells.Fifth.DispelFieldSpell(playerBot, null),
                    new Server.Spells.Fifth.MindBlastSpell(playerBot, null),
                    new Server.Spells.Fifth.PoisonFieldSpell(playerBot, null)
                });
            }
            else if (maxCircle >= SpellCircle.Fourth)
            {
                // Fourth circle spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Fourth.LightningSpell(playerBot, null),
                    new Server.Spells.Fourth.FireFieldSpell(playerBot, null),
                    new Server.Spells.Fourth.CurseSpell(playerBot, null),
                    new Server.Spells.Fourth.ManaDrainSpell(playerBot, null)
                });
            }
            else if (maxCircle >= SpellCircle.Third)
            {
                // Third circle spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Third.FireballSpell(playerBot, null),
                    new Server.Spells.Third.PoisonSpell(playerBot, null),
                    new Server.Spells.Third.WallOfStoneSpell(playerBot, null),
                    new Server.Spells.Third.TelekinesisSpell(playerBot, null)
                });
            }
            else if (maxCircle >= SpellCircle.Second)
            {
                // Second circle spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Second.HarmSpell(playerBot, null),
                    new Server.Spells.Second.MagicTrapSpell(playerBot, null),
                    new Server.Spells.Second.ProtectionSpell(playerBot, null),
                    new Server.Spells.Second.StrengthSpell(playerBot, null)
                });
            }
            else
            {
                // First circle spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.First.ClumsySpell(playerBot, null),
                    new Server.Spells.First.FeeblemindSpell(playerBot, null),
                    new Server.Spells.First.MagicArrowSpell(playerBot, null),
                    new Server.Spells.First.WeakenSpell(playerBot, null)
                });
            }

            if (spell != null)
            {
                // Start casting the spell
                if (spell.Cast())
                {
                    // For offensive spells that need targeting, we need to call their Target method
                    // This is a simplified approach - in a full implementation, you'd need to handle each spell type
                    if (spell is Server.Spells.First.MagicArrowSpell)
                    {
                        ((Server.Spells.First.MagicArrowSpell)spell).Target(target);
                    }
                    else if (spell is Server.Spells.Second.HarmSpell)
                    {
                        ((Server.Spells.Second.HarmSpell)spell).Target(target);
                    }
                    else if (spell is Server.Spells.Third.FireballSpell)
                    {
                        ((Server.Spells.Third.FireballSpell)spell).Target(target);
                    }
                    else if (spell is Server.Spells.Fourth.LightningSpell)
                    {
                        ((Server.Spells.Fourth.LightningSpell)spell).Target(target);
                    }
                    else if (spell is Server.Spells.Fifth.MindBlastSpell)
                    {
                        ((Server.Spells.Fifth.MindBlastSpell)spell).Target(target);
                    }
                    else if (spell is Server.Spells.Sixth.EnergyBoltSpell)
                    {
                        ((Server.Spells.Sixth.EnergyBoltSpell)spell).Target(target);
                    }
                    else if (spell is Server.Spells.Seventh.FlameStrikeSpell)
                    {
                        ((Server.Spells.Seventh.FlameStrikeSpell)spell).Target(target);
                    }
                    else if (spell is Server.Spells.Seventh.MeteorSwarmSpell)
                    {
                        ((Server.Spells.Seventh.MeteorSwarmSpell)spell).Target(target);
                    }
                    else if (spell is Server.Spells.Seventh.ChainLightningSpell)
                    {
                        ((Server.Spells.Seventh.ChainLightningSpell)spell).Target(target);
                    }
                    else if (spell is Server.Spells.Eighth.EarthquakeSpell)
                    {
                        // Earthquake is self-cast - no Target method needed
                        // The OnCast method will handle everything automatically
                    }
                    else if (spell is Server.Spells.Eighth.EnergyVortexSpell)
                    {
                        // Energy Vortex is self-cast - no Target method needed
                    }
                    else if (spell is Server.Spells.Eighth.FireElementalSpell)
                    {
                        // Fire Elemental is self-cast - no Target method needed
                    }
                    else if (spell is Server.Spells.Eighth.SummonDaemonSpell)
                    {
                        // Summon Daemon is self-cast - no Target method needed
                    }
                    else
                    {
                        // For spells that don't need targeting, just let them complete normally
                        // The spell's OnCast method will handle the rest
                    }
                    
                    playerBot.DebugSay("I cast {0}!", spell.GetType().Name);
                    return true;
                }
            }

            return false;
        }

        private bool CastDefensiveSpell(PlayerBot playerBot, double magerySkill)
        {
            SpellCircle maxCircle = DetermineMaxSpellCircle(magerySkill);
            Spell spell = null;

            if (maxCircle >= SpellCircle.Fifth)
            {
                // High-level defensive spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Fifth.MagicReflectSpell(playerBot, null),
                    new Server.Spells.Fourth.ArchProtectionSpell(playerBot, null),
                    new Server.Spells.Third.BlessSpell(playerBot, null),
                    new Server.Spells.Fourth.GreaterHealSpell(playerBot, null)
                });
            }
            else if (maxCircle >= SpellCircle.Fourth)
            {
                // Mid-level defensive spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Fourth.ArchProtectionSpell(playerBot, null),
                    new Server.Spells.Third.BlessSpell(playerBot, null),
                    new Server.Spells.Fourth.GreaterHealSpell(playerBot, null),
                    new Server.Spells.Second.AgilitySpell(playerBot, null)
                });
            }
            else
            {
                // Low-level defensive spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Third.BlessSpell(playerBot, null),
                    new Server.Spells.Second.AgilitySpell(playerBot, null),
                    new Server.Spells.First.HealSpell(playerBot, null),
                    new Server.Spells.Second.CureSpell(playerBot, null)
                });
            }

            if (spell != null)
            {
                // Start casting the spell
                if (spell.Cast())
                {
                    // For defensive spells that target self, call their Target method
                    if (spell is Server.Spells.First.HealSpell)
                    {
                        ((Server.Spells.First.HealSpell)spell).Target(playerBot);
                    }
                    else if (spell is Server.Spells.Fourth.GreaterHealSpell)
                    {
                        ((Server.Spells.Fourth.GreaterHealSpell)spell).Target(playerBot);
                    }
                    else if (spell is Server.Spells.Second.CureSpell)
                    {
                        ((Server.Spells.Second.CureSpell)spell).Target(playerBot);
                    }
                    else if (spell is Server.Spells.Third.BlessSpell)
                    {
                        ((Server.Spells.Third.BlessSpell)spell).Target(playerBot);
                    }
                    else if (spell is Server.Spells.Second.AgilitySpell)
                    {
                        ((Server.Spells.Second.AgilitySpell)spell).Target(playerBot);
                    }
                    else if (spell is Server.Spells.Fourth.ArchProtectionSpell)
                    {
                        ((Server.Spells.Fourth.ArchProtectionSpell)spell).Target(playerBot);
                    }
                    else if (spell is Server.Spells.Fifth.MagicReflectSpell)
                    {
                        // Magic Reflect is self-cast - no Target method needed
                    }
                    else
                    {
                        // For spells that don't need targeting, just let them complete normally
                    }
                    
                    playerBot.DebugSay("I cast defensive spell {0}!", spell.GetType().Name);
                    return true;
                }
            }

            return false;
        }

        private bool CastUtilitySpell(PlayerBot playerBot, Mobile target, double magerySkill)
        {
            SpellCircle maxCircle = DetermineMaxSpellCircle(magerySkill);
            Spell spell = null;

            if (maxCircle >= SpellCircle.Sixth)
            {
                // High-level utility spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Sixth.InvisibilitySpell(playerBot, null),
                    new Server.Spells.Sixth.MarkSpell(playerBot, null),
                    new Server.Spells.Seventh.GateTravelSpell(playerBot, null),
                    new Server.Spells.Fifth.IncognitoSpell(playerBot, null)
                });
            }
            else if (maxCircle >= SpellCircle.Fifth)
            {
                // Mid-level utility spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Fifth.IncognitoSpell(playerBot, null),
                    new Server.Spells.Fourth.ArchCureSpell(playerBot, null),
                    new Server.Spells.Third.MagicLockSpell(playerBot, null),
                    new Server.Spells.Second.CunningSpell(playerBot, null)
                });
            }
            else
            {
                // Low-level utility spells
                spell = SelectRandomSpell(new Spell[] {
                    new Server.Spells.Second.CunningSpell(playerBot, null),
                    new Server.Spells.First.CreateFoodSpell(playerBot, null),
                    new Server.Spells.Second.CureSpell(playerBot, null),
                    new Server.Spells.First.ClumsySpell(playerBot, null)
                });
            }

            if (spell != null)
            {
                // Start casting the spell
                if (spell.Cast())
                {
                    // For utility spells that need targeting
                    if (spell is Server.Spells.First.ClumsySpell)
                    {
                        ((Server.Spells.First.ClumsySpell)spell).Target(target);
                    }
                    else if (spell is Server.Spells.Second.CureSpell)
                    {
                        ((Server.Spells.Second.CureSpell)spell).Target(playerBot);
                    }
                    else if (spell is Server.Spells.Second.CunningSpell)
                    {
                        ((Server.Spells.Second.CunningSpell)spell).Target(playerBot);
                    }
                    else if (spell is Server.Spells.Fourth.ArchCureSpell)
                    {
                        ((Server.Spells.Fourth.ArchCureSpell)spell).Target(playerBot);
                    }
                    else if (spell is Server.Spells.Fifth.IncognitoSpell)
                    {
                        // Incognito is self-cast - no Target method needed
                    }
                    else if (spell is Server.Spells.Sixth.InvisibilitySpell)
                    {
                        // Invisibility is self-cast - no Target method needed
                    }
                    else if (spell is Server.Spells.Seventh.GateTravelSpell)
                    {
                        // Gate Travel is self-cast - no Target method needed
                    }
                    else if (spell is Server.Spells.Sixth.MarkSpell)
                    {
                        // Mark is self-cast - no Target method needed
                    }
                    else if (spell is Server.Spells.First.CreateFoodSpell)
                    {
                        // Create Food is self-cast - no Target method needed
                    }
                    else
                    {
                        // For spells that don't need targeting, just let them complete normally
                    }
                    
                    playerBot.DebugSay("I cast utility spell {0}!", spell.GetType().Name);
                    return true;
                }
            }

            return false;
        }

        private SpellCircle DetermineMaxSpellCircle(double magerySkill)
        {
            if (magerySkill >= 80.0)
                return SpellCircle.Eighth;
            else if (magerySkill >= 70.0)
                return SpellCircle.Seventh;
            else if (magerySkill >= 60.0)
                return SpellCircle.Sixth;
            else if (magerySkill >= 50.0)
                return SpellCircle.Fifth;
            else if (magerySkill >= 40.0)
                return SpellCircle.Fourth;
            else if (magerySkill >= 30.0)
                return SpellCircle.Third;
            else if (magerySkill >= 20.0)
                return SpellCircle.Second;
            else
                return SpellCircle.First;
        }

        private Spell SelectRandomSpell(Spell[] spells)
        {
            if (spells.Length == 0)
                return null;

            // Filter out spells that require reagents we don't have
            var availableSpells = new List<Spell>();
            foreach (Spell spell in spells)
            {
                if (HasRequiredReagents(m_Mobile as PlayerBot, spell))
                {
                    availableSpells.Add(spell);
                }
            }

            if (availableSpells.Count == 0)
                return null;

            return availableSpells[Utility.Random(availableSpells.Count)];
        }

        private bool HasRequiredReagents(PlayerBot playerBot, Spell spell)
        {
            // Check if we have the required reagents for the spell
            // This is a simplified check - in a full implementation, you'd check specific reagents
            // For now, we'll assume PlayerBots have basic reagents available
            
            // Check mana requirement using the correct method
            if (playerBot.Mana < spell.GetMana())
                return false;

            // For now, assume we have reagents (PlayerBots should be given basic reagents)
            return true;
        }

        public static int[] m_ManaTable = new int[]{ 4, 6, 9, 11, 14, 20, 40, 50 };
    }
}
