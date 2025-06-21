using System;
using System.Collections.Generic;
using System.Text;
using Server;
using Server.Spells;
using Server.Items;
using Server.Targeting;
using Server.Misc;

namespace Server.Mobiles
{
    public class PlayerBotAI : BaseAI
    {
        private DateTime m_NextCastTime;
        private Item m_SavedWeapon;
        private Item m_SavedShield;

        public PlayerBotAI(BaseCreature m) : base(m)
        {
            m_SavedWeapon = null;
            m_SavedShield = null;
        }

        public override bool Think()
        {
            if (m_Mobile.Deleted)
                return false;

            Target targ = m_Mobile.Target;

            if (targ != null)
            {
                ProcessTarget(targ);
                return true;
            }
            else
            {
                return base.Think();
            }
        }

        public virtual bool SmartAI
        {
            get { return (m_Mobile.Body.IsHuman || m_Mobile is BaseVendor || m_Mobile is BaseEscortable); }
        }

        public void RunFrom(Mobile m)
        {
            if (!MoveTo(m, true, m_Mobile.RangeFight))
                OnFailedMove();
        }

        private void ProcessTarget(Target targ)
        {
            bool isDispel = (targ is Server.Spells.Sixth.DispelSpell.InternalTarget);
            bool isParalyze = (targ is Server.Spells.Fifth.ParalyzeSpell.InternalTarget);

            Mobile toTarget;

            if (isDispel)
            {
                toTarget = FindDispelTarget(false);

                if (!SmartAI && toTarget != null)
                    RunTo(toTarget);
                else if (toTarget != null && m_Mobile.InRange(toTarget, 8))
                    RunFrom(toTarget);
            }
            else if (SmartAI && isParalyze)
            {
                toTarget = FindDispelTarget(true);

                if (toTarget == null)
                {
                    toTarget = m_Mobile.Combatant;

                    if (toTarget != null)
                        RunTo(toTarget);
                }
            }
            else
            {
                toTarget = m_Mobile.Combatant;

                if (toTarget != null)
                    RunTo(toTarget);
            }

            if ((targ.Flags & TargetFlags.Harmful) != 0 && toTarget != null)
            {
                if ((targ.Range == -1 || m_Mobile.InRange(toTarget, targ.Range)) && m_Mobile.CanSee(toTarget) && m_Mobile.InLOS(toTarget))
                {
                    targ.Invoke(m_Mobile, toTarget);
                }
                else if (isDispel)
                {
                    targ.Cancel(m_Mobile, TargetCancelType.Canceled);
                }
            }
            else if ((targ.Flags & TargetFlags.Beneficial) != 0)
            {
                targ.Invoke(m_Mobile, m_Mobile);
            }
            else
            {
                targ.Cancel(m_Mobile, TargetCancelType.Canceled);
            }
        }

        private Mobile FindDispelTarget(bool activeOnly)
        {
            if (m_Mobile.Deleted || m_Mobile.Int < 91)
                return null;

            if (activeOnly)
            {
                List<AggressorInfo> aggressed = m_Mobile.Aggressed;
                List<AggressorInfo> aggressors = m_Mobile.Aggressors;

                Mobile active = null;
                double activePrio = 0.0;

                Mobile comb = m_Mobile.Combatant;

                if (comb != null && !comb.Deleted && m_Mobile.InRange(comb, 12) && CanDispel(comb))
                {
                    active = comb;
                    activePrio = m_Mobile.GetDistanceToSqrt(comb);

                    if (activePrio <= 2)
                        return active;
                }

                for (int i = 0; i < aggressed.Count; ++i)
                {
                    AggressorInfo info = aggressed[i];
                    Mobile m = info.Defender;
                    if (m != comb && m.Combatant == m_Mobile && m_Mobile.InRange(m, 12) && CanDispel(m))
                    {
                        double prio = m_Mobile.GetDistanceToSqrt(m);
                        if (active == null || prio < activePrio)
                        {
                            active = m;
                            activePrio = prio;
                            if (activePrio <= 2)
                                return active;
                        }
                    }
                }

                for (int i = 0; i < aggressors.Count; ++i)
                {
                    AggressorInfo info = aggressors[i];
                    Mobile m = info.Attacker;
                    if (m != comb && m.Combatant == m_Mobile && m_Mobile.InRange(m, 12) && CanDispel(m))
                    {
                        double prio = m_Mobile.GetDistanceToSqrt(m);
                        if (active == null || prio < activePrio)
                        {
                            active = m;
                            activePrio = prio;
                            if (activePrio <= 2)
                                return active;
                        }
                    }
                }

                return active;
            }
            else
            {
                Map map = m_Mobile.Map;
                if (map != null)
                {
                    Mobile active = null, inactive = null;
                    double actPrio = 0.0, inactPrio = 0.0;

                    Mobile comb = m_Mobile.Combatant;
                    if (comb != null && !comb.Deleted && CanDispel(comb))
                    {
                        active = inactive = comb;
                        actPrio = inactPrio = m_Mobile.GetDistanceToSqrt(comb);
                    }

                    foreach (Mobile m in m_Mobile.GetMobilesInRange(12))
                    {
                        if (m != m_Mobile && CanDispel(m))
                        {
                            double prio = m_Mobile.GetDistanceToSqrt(m);
                            if (inactive == null || prio < inactPrio)
                            {
                                inactive = m;
                                inactPrio = prio;
                            }
                            if ((m_Mobile.Combatant == m || m.Combatant == m_Mobile) && (active == null || prio < actPrio))
                            {
                                active = m;
                                actPrio = prio;
                            }
                        }
                    }

                    return active ?? inactive;
                }
            }
            return null;
        }

        private bool CanDispel(Mobile m)
        {
            if (m is BaseCreature)
            {
                BaseCreature bc = (BaseCreature)m;
                return bc.Summoned && m_Mobile.CanBeHarmful(m, false) && !bc.IsAnimatedDead;
            }

            return false;
        }

        public void RunTo(Mobile m)
        {
            if (!MoveTo(m, true, m_Mobile.RangeFight))
                OnFailedMove();
        }

        public void OnFailedMove()
        {
            if (AquireFocusMob(m_Mobile.RangePerception, m_Mobile.FightMode, false, false, true))
            {
                m_Mobile.DebugSay("My move is blocked, so I am going to attack {0}", m_Mobile.FocusMob.Name);
                m_Mobile.Combatant = m_Mobile.FocusMob;
                Action = ActionType.Combat;
            }
            else
            {
                m_Mobile.DebugSay("I am stuck");
            }
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
                Action = ActionType.Wander;
                return true;
            }

            // If we are casting a spell, stand still and wait for it to complete.
            // This prevents the AI from running towards the target while casting.
            if (m_Mobile.Spell != null && m_Mobile.Spell.IsCasting)
            {
                m_Mobile.DebugSay("Casting in progress, holding position.");
                return true; // Do nothing else this tick.
            }

            // Handle combat speech and emotes
            HandleCombatSpeech(combatant);

            // Check if we should flee
            if (m_Mobile.CheckFlee())
            {
                bool flee = false;

                if (m_Mobile.Hits < combatant.Hits)
                {
                    int diff = combatant.Hits - m_Mobile.Hits;
                    flee = (Utility.Random(0, 100) < (10 + diff));
                }
                else
                {
                    flee = Utility.Random(0, 100) < 10;
                }

                if (flee)
                {
                    m_Mobile.DebugSay("I am going to flee from {0}", combatant.Name);
                    Action = ActionType.Flee;
                    return true;
                }
            }

            // Handle spell casting - use proper MageAI pattern with damage entry checks
            if (m_Mobile.Spell == null && DateTime.Now > m_NextCastTime && m_Mobile.InRange(combatant, 12) && m_Mobile.InLOS(combatant))
            {
                // CRITICAL: Check for damage entries before casting spells (like MageAI does)
                DamageEntry de = m_Mobile.FindDamageEntryFor(combatant);
                if (de == null || de.HasExpired)
                {
                    de = combatant.FindDamageEntryFor(m_Mobile);
                    if ((de == null || de.HasExpired))
                    {
                        if (!NotorietyHandlers.CheckAggressor(m_Mobile.Aggressors, combatant) && !NotorietyHandlers.CheckAggressed(combatant.Aggressed, m_Mobile))
                        {
                            // We can't cast because we didn't give or take damage yet
                            // Move to target first to initiate combat
                            m_Mobile.DebugSay("No damage exchanged yet, moving to {0} to initiate combat", combatant.Name);
                            RunTo(combatant);
                            return true;
                        }
                    }
                }

                // Check if we can cast spells
                PlayerBot playerBot = m_Mobile as PlayerBot;
                if (playerBot != null && ShouldCastSpell(combatant))
                {
                    Spell spell = GetRandomCombatSpell(playerBot, combatant);

                    if (spell != null)
                    {
                        playerBot.DebugSay("Attempting to cast {0} on {1}", spell.GetType().Name, combatant.Name);
                        
                        // Ensure our hands are free so casting succeeds
                        EnsureHandsFree();
                        
                        if (spell.Cast())
                        {
                            // Set proper cooldown like MageAI
                            TimeSpan delay = spell.GetCastDelay() + TimeSpan.FromSeconds(Utility.Random(2, 5));
                            m_NextCastTime = DateTime.Now + delay;
                            
                            playerBot.DebugSay("SUCCESS: Casting {0} on {1}", spell.GetType().Name, combatant.Name);

                            // DO NOT re-equip here. The spell has just started.
                            // Re-equipping will happen on the next Think() tick when Spell == null.

                            return true;
                        }
                        else
                        {
                            playerBot.DebugSay("FAILED: Could not cast {0} on {1}", spell.GetType().Name, combatant.Name);
                        }
                    }
                    else
                    {
                        playerBot.DebugSay("No spell selected for casting");
                    }
                }
            }
            else if (m_Mobile.Spell == null)
            {
                PlayerBot bot = m_Mobile as PlayerBot;
                if (bot != null && !bot.PrefersMelee)
                {
                    HandleRangedCombat(combatant);
                }
                else
                {
                    // Melee logic: Move towards target if not casting
                    RunTo(combatant);
                }

                // Try to re-equip weapons while moving / not casting
                ReEquipWeapons();
            }

            return true;
        }

        private void HandleRangedCombat(Mobile combatant)
        {
            Item weapon = m_Mobile.Weapon as Item;

            // Ranged bots without a ranged weapon should fallback to melee behavior.
            if (!(weapon is BaseRanged))
            {
                RunTo(combatant);
                return;
            }

            BaseRanged rangedWeapon = (BaseRanged)weapon;
            int maxRange = rangedWeapon.MaxRange;
            int minIdealRange = 4; // Stay at least this far away.
            int idealRange = Math.Max(minIdealRange, maxRange - 2);

            int dist = (int)m_Mobile.GetDistanceToSqrt(combatant);

            if (dist > maxRange || !m_Mobile.InLOS(combatant))
            {
                m_Mobile.DebugSay("Ranged: Target is out of range or sight. Moving closer.");
                if (!MoveTo(combatant, true, idealRange))
                    OnFailedMove();
            }
            else if (dist < minIdealRange)
            {
                m_Mobile.DebugSay("Ranged: Target is too close. Backing away.");
                Direction dir = m_Mobile.GetDirectionTo(combatant);
                DoMove((Direction)(((int)dir + 4) & 0x7)); // Turn around and walk
            }
            else
            {
                m_Mobile.DebugSay("Ranged: Target in range. Holding position.");
                // In ideal range, stop moving and face the target to attack.
                m_Mobile.Direction = m_Mobile.GetDirectionTo(combatant);
            }
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
            {
                m_Mobile.DebugSay("ShouldCastSpell: Not a PlayerBot");
                return false;
            }

            // Check if we have sufficient Magery skill
            double magerySkill = playerBot.Skills[SkillName.Magery].Base;
            if (magerySkill < 10.0)
            {
                playerBot.DebugSay("ShouldCastSpell: Magery too low ({0:F1} < 10.0)", magerySkill);
                return false;
            }

            // Check if we're not already casting a spell
            if (playerBot.Spell != null)
                return false;

            // Check if we have enough mana
            if (playerBot.Mana < 8)
            {
                playerBot.DebugSay("ShouldCastSpell: Not enough mana ({0} < 8)", playerBot.Mana);
                return false;
            }

            // Check if we're not paralyzed or frozen
            if (playerBot.Paralyzed || playerBot.Frozen)
            {
                playerBot.DebugSay("ShouldCastSpell: Paralyzed or frozen");
                return false;
            }

            // Simple casting chance based on skill level
            double castChance = magerySkill / 100.0;
            
            // Increase chance if we're in danger
            if (playerBot.Hits < playerBot.HitsMax * 0.3)
                castChance *= 1.5;

            // Cap the chance
            castChance = Math.Min(castChance, 0.8);

            bool shouldCast = Utility.RandomDouble() < castChance;
            playerBot.DebugSay("ShouldCastSpell: Chance {0:F2}%, Result: {1}", castChance * 100, shouldCast);
            
            return shouldCast;
        }

        private Spell GetRandomCombatSpell(PlayerBot playerBot, Mobile target)
        {
            if (playerBot.Mana < 8)
            {
                playerBot.DebugSay("GetRandomCombatSpell: Not enough mana ({0} < 8)", playerBot.Mana);
                return null;
            }

            double magerySkill = playerBot.Skills[SkillName.Magery].Base;
            int maxCircle = (int)(magerySkill / 87.5 * 8.0);
            
            if (maxCircle > 8)
                maxCircle = 8;

            while (maxCircle > 1 && Spell.m_ManaTable[maxCircle - 1] > (playerBot.Mana / 2))
                maxCircle--;

            if (maxCircle < 1)
                maxCircle = 1;

            playerBot.DebugSay("GetRandomCombatSpell: Magery {0:F1}, Max Circle {1}, Mana {2}", magerySkill, maxCircle, playerBot.Mana);

            int spell;
            if (maxCircle < 3)
            {
                spell = maxCircle;
            }
            else
            {
                spell = Utility.Random(maxCircle) + 1;
            }

            Spell selectedSpell = null;

            switch (spell)
            {
                default:
                case 1:
                    {
                        switch (Utility.Random(4))
                        {
                            case 0: selectedSpell = new Server.Spells.First.ClumsySpell(playerBot, null); break;
                            case 1: selectedSpell = new Server.Spells.First.FeeblemindSpell(playerBot, null); break;
                            case 2: selectedSpell = new Server.Spells.First.WeakenSpell(playerBot, null); break;
                            default: selectedSpell = new Server.Spells.First.MagicArrowSpell(playerBot, null); break;
                        }
                    }
                    break;

                case 2:
                    selectedSpell = new Server.Spells.Second.HarmSpell(playerBot, null);
                    break;

                case 3:
                    {
                        switch (Utility.Random(2))
                        {
                            case 0: selectedSpell = new Server.Spells.Third.PoisonSpell(playerBot, null); break;
                            default: selectedSpell = new Server.Spells.Third.FireballSpell(playerBot, null); break;
                        }
                    }
                    break;

                case 4:
                    selectedSpell = new Server.Spells.Fourth.LightningSpell(playerBot, null);
                    break;

                case 5:
                    {
                        if (playerBot.Body.IsHuman)
                        {
                            switch (Utility.Random(2))
                            {
                                case 0: selectedSpell = new Server.Spells.Fifth.ParalyzeSpell(playerBot, null); break;
                                default: selectedSpell = new Server.Spells.Fifth.MindBlastSpell(playerBot, null); break;
                            }
                        }
                        else
                        {
                            selectedSpell = new Server.Spells.Fourth.LightningSpell(playerBot, null);
                        }
                    }
                    break;

                case 6:
                    {
                        switch (Utility.Random(2))
                        {
                            case 0: selectedSpell = new Server.Spells.Sixth.EnergyBoltSpell(playerBot, null); break;
                            default: selectedSpell = new Server.Spells.Sixth.ExplosionSpell(playerBot, null); break;
                        }
                    }
                    break;

                case 7:
                case 8:
                    selectedSpell = new Server.Spells.Seventh.FlameStrikeSpell(playerBot, null);
                    break;
            }

            if (selectedSpell != null)
            {
                playerBot.DebugSay("GetRandomCombatSpell: Selected {0}", selectedSpell.GetType().Name);
            }
            else
            {
                playerBot.DebugSay("GetRandomCombatSpell: No spell selected");
            }

            return selectedSpell;
        }

        public static int[] m_ManaTable = new int[]{ 4, 6, 9, 11, 14, 20, 40, 50 };

        private void HandleCombatSpeech(Mobile target)
        {
            PlayerBot playerBot = m_Mobile as PlayerBot;
            if (playerBot == null)
                return;

            // Don't spam speech - only talk occasionally
            if (Utility.RandomDouble() > 0.05) // Reduced from 15% to 5% chance per combat tick
                return;

            // Check if we're too far to be heard
            if (m_Mobile.GetDistanceToSqrt(target) > 8)
                return;

            // Add additional cooldown - don't speak too frequently
            if (playerBot.LastSpeechTime > DateTime.Now.AddSeconds(-8)) // 8 second cooldown between speeches
                return;

            // Determine what type of speech to make based on situation
            double healthPercentage = (double)m_Mobile.Hits / m_Mobile.HitsMax;
            double targetHealthPercentage = (double)target.Hits / target.HitsMax;
            bool isWinning = healthPercentage > targetHealthPercentage;
            bool isLosing = healthPercentage < targetHealthPercentage * 0.7;

            // Choose speech type based on situation
            if (isLosing && Utility.RandomDouble() < 0.4)
            {
                // 40% chance to taunt when losing
                SayCombatTaunt(target, playerBot);
            }
            else if (isWinning && Utility.RandomDouble() < 0.3)
            {
                // 30% chance to boast when winning
                SayVictoryBoast(target, playerBot);
            }
            else if (Utility.RandomDouble() < 0.25)
            {
                // 25% chance for general battle cry
                SayBattleCry(playerBot);
            }
            else if (Utility.RandomDouble() < 0.2)
            {
                // 20% chance for contextual emote
                PerformCombatEmote(target, playerBot);
            }

            // Update last speech time
            playerBot.LastSpeechTime = DateTime.Now;
        }

        private void SayCombatTaunt(Mobile target, PlayerBot playerBot)
        {
            string[] taunts = GetTauntsForProfile(playerBot.PlayerBotProfile);
            if (taunts.Length > 0)
            {
                string taunt = taunts[Utility.Random(taunts.Length)];
                taunt = taunt.Replace("{target}", target.Name);
                taunt = taunt.Replace("{self}", playerBot.Name);
                
                playerBot.SayWithHue(taunt);
            }
        }

        private void SayVictoryBoast(Mobile target, PlayerBot playerBot)
        {
            string[] boasts = GetBoastsForProfile(playerBot.PlayerBotProfile);
            if (boasts.Length > 0)
            {
                string boast = boasts[Utility.Random(boasts.Length)];
                boast = boast.Replace("{target}", target.Name);
                boast = boast.Replace("{self}", playerBot.Name);
                
                playerBot.SayWithHue(boast);
            }
        }

        private void SayBattleCry(PlayerBot playerBot)
        {
            string[] battleCries = GetBattleCriesForProfile(playerBot.PlayerBotProfile);
            if (battleCries.Length > 0)
            {
                string battleCry = battleCries[Utility.Random(battleCries.Length)];
                battleCry = battleCry.Replace("{self}", playerBot.Name);
                
                playerBot.SayWithHue(battleCry);
            }
        }

        private void PerformCombatEmote(Mobile target, PlayerBot playerBot)
        {
            double healthPercentage = (double)playerBot.Hits / playerBot.HitsMax;
            
            if (healthPercentage < 0.3)
            {
                // Low health - desperate emotes
                string[] desperateEmotes = {
                    "*clutches wounds*",
                    "*staggers from pain*",
                    "*grits teeth in determination*",
                    "*wipes blood from face*"
                };
                playerBot.EmoteWithHue(desperateEmotes[Utility.Random(desperateEmotes.Length)]);
            }
            else if (healthPercentage > 0.8)
            {
                // High health - confident emotes
                string[] confidentEmotes = {
                    "*cracks knuckles*",
                    "*adjusts stance*",
                    "*smirks confidently*",
                    "*rolls shoulders*"
                };
                playerBot.EmoteWithHue(confidentEmotes[Utility.Random(confidentEmotes.Length)]);
            }
            else
            {
                // Medium health - focused emotes
                string[] focusedEmotes = {
                    "*focuses on {target}*",
                    "*circles warily*",
                    "*maintains guard*",
                    "*studies opponent*"
                };
                string emote = focusedEmotes[Utility.Random(focusedEmotes.Length)];
                emote = emote.Replace("{target}", target.Name);
                playerBot.EmoteWithHue(emote);
            }
        }

        private string[] GetTauntsForProfile(PlayerBotPersona.PlayerBotProfile profile)
        {
            switch (profile)
            {
                case PlayerBotPersona.PlayerBotProfile.PlayerKiller:
                    return new string[] {
                        "You're just another victim, {target}!",
                        "Your death will be swift, {target}!",
                        "I've killed better than you!",
                        "You're not worth the effort, {target}!",
                        "Another fool to add to my collection!",
                        "Your screams will be music to my ears!",
                        "Die like the weakling you are!",
                        "I'll make this quick... for me!",
                        "You picked the wrong fight, {target}!",
                        "Your blood will stain the ground!"
                    };
                    
                case PlayerBotPersona.PlayerBotProfile.Adventurer:
                    return new string[] {
                        "You're no match for my skills, {target}!",
                        "I've faced worse than you!",
                        "Your technique is sloppy!",
                        "Is that the best you can do?",
                        "I expected more of a challenge!",
                        "You fight like a novice!",
                        "My training will be your undoing!",
                        "You're outclassed, {target}!",
                        "This will be over quickly!",
                        "You should have stayed home!"
                    };
                    
                case PlayerBotPersona.PlayerBotProfile.Crafter:
                    return new string[] {
                        "I may be a crafter, but I can fight!",
                        "Don't underestimate a skilled worker!",
                        "My hands are strong from years of crafting!",
                        "You think crafters can't fight?",
                        "I've built things tougher than you!",
                        "My tools can be weapons too!",
                        "You'll regret attacking a crafter!",
                        "I work with my hands every day!",
                        "This is what happens when you mess with a crafter!",
                        "My craft has made me strong!"
                    };
                    
                default:
                    return new string[] {
                        "You're going down, {target}!",
                        "This is your last mistake!",
                        "You picked the wrong opponent!",
                        "I'll show you what I'm made of!",
                        "You're not ready for this fight!"
                    };
            }
        }

        private string[] GetBoastsForProfile(PlayerBotPersona.PlayerBotProfile profile)
        {
            switch (profile)
            {
                case PlayerBotPersona.PlayerBotProfile.PlayerKiller:
                    return new string[] {
                        "Another kill to my name!",
                        "You were too weak to survive!",
                        "Death comes for all who oppose me!",
                        "Your life ends here, {target}!",
                        "I am the reaper of souls!",
                        "Your death feeds my power!",
                        "Another victim falls before me!",
                        "You should have run when you had the chance!",
                        "I am unstoppable!",
                        "Your blood strengthens me!"
                    };
                    
                case PlayerBotPersona.PlayerBotProfile.Adventurer:
                    return new string[] {
                        "My experience shows, {target}!",
                        "Years of training pay off!",
                        "You can't match my skills!",
                        "I've survived worse than you!",
                        "My adventures have made me strong!",
                        "You're no match for a true adventurer!",
                        "I've seen things that would break you!",
                        "My journey has prepared me for this!",
                        "You fight like an amateur!",
                        "Experience always wins!"
                    };
                    
                case PlayerBotPersona.PlayerBotProfile.Crafter:
                    return new string[] {
                        "Crafters are tougher than you think!",
                        "My work has made me strong!",
                        "You underestimated a crafter!",
                        "I build and I destroy!",
                        "My hands are my weapons!",
                        "Crafting builds character and strength!",
                        "You thought crafters were weak?",
                        "My trade has taught me discipline!",
                        "I create and I can destroy!",
                        "Crafters are not to be trifled with!"
                    };
                    
                default:
                    return new string[] {
                        "I am victorious!",
                        "You were no match for me!",
                        "My strength prevails!",
                        "I am the better fighter!",
                        "Victory is mine!"
                    };
            }
        }

        private string[] GetBattleCriesForProfile(PlayerBotPersona.PlayerBotProfile profile)
        {
            switch (profile)
            {
                case PlayerBotPersona.PlayerBotProfile.PlayerKiller:
                    return new string[] {
                        "Death to all who oppose me!",
                        "Blood will flow!",
                        "I am the bringer of death!",
                        "Your souls are mine!",
                        "Fear my wrath!",
                        "I am the nightmare!",
                        "Death is my ally!",
                        "I am unstoppable!",
                        "Your doom approaches!",
                        "I am the darkness!"
                    };
                    
                case PlayerBotPersona.PlayerBotProfile.Adventurer:
                    return new string[] {
                        "For glory and adventure!",
                        "My blade serves justice!",
                        "I fight for honor!",
                        "Adventure calls!",
                        "My skills will prevail!",
                        "For the thrill of battle!",
                        "I am a warrior!",
                        "My experience guides me!",
                        "I fight for what's right!",
                        "Adventure never ends!"
                    };
                    
                case PlayerBotPersona.PlayerBotProfile.Crafter:
                    return new string[] {
                        "My craft is my strength!",
                        "I build and I fight!",
                        "My hands are my weapons!",
                        "Crafters are not weak!",
                        "I work and I fight!",
                        "My trade has made me strong!",
                        "I create and I destroy!",
                        "Crafters have pride too!",
                        "My skills serve me well!",
                        "I am a worker and a warrior!"
                    };
                    
                default:
                    return new string[] {
                        "For honor!",
                        "I will not fall!",
                        "My strength is my weapon!",
                        "I fight for what I believe!",
                        "Victory or death!"
                    };
            }
        }

        /// <summary>
        /// Moves any non-spell-channeling weapons/shields from the hands into the
        /// backpack so magery can be cast.
        /// </summary>
        private void EnsureHandsFree()
        {
            if (m_Mobile == null || m_Mobile.Deleted)
                return;

            Container pack = m_Mobile.Backpack;
            if (pack == null)
                return;

            // Clear any previously saved items to prevent stale references.
            m_SavedWeapon = null;
            m_SavedShield = null;

            Item one = m_Mobile.FindItemOnLayer(Layer.OneHanded);
            Item two = m_Mobile.FindItemOnLayer(Layer.TwoHanded);

            // The item on Layer.TwoHanded could be a 2H weapon OR a shield.
            // Check it first.
            if (two != null && !two.AllowEquipedCast(m_Mobile))
            {
                if (two is BaseShield)
                {
                    m_SavedShield = two;
                    pack.DropItem(two);
                }
                else // It must be a two-handed weapon
                {
                    m_SavedWeapon = two;
                    pack.DropItem(two);
                }
            }

            // Now check the one-handed layer.
            if (one != null && !one.AllowEquipedCast(m_Mobile))
            {
                // A 1H weapon cannot be equipped with a 2H weapon.
                // In a valid state, m_SavedWeapon should be null here.
                // This prevents the overwrite bug.
                if (m_SavedWeapon == null)
                {
                    m_SavedWeapon = one;
                    pack.DropItem(one);
                }
            }
        }

        /// <summary>
        /// Attempts to re-equip previously saved weapons if the bot is not currently casting.
        /// </summary>
        private void ReEquipWeapons()
        {
            // Wait until the spell is fully finished (mobile.Spell becomes null)
            if (m_Mobile == null || m_Mobile.Deleted || m_Mobile.Spell != null)
                return;

            // If hands are already full, do nothing. This prevents interrupting other actions.
            if (m_Mobile.FindItemOnLayer(Layer.OneHanded) != null || m_Mobile.FindItemOnLayer(Layer.TwoHanded) != null)
                return;

            Container pack = m_Mobile.Backpack;
            if (pack == null)
                return;

            PlayerBot bot = m_Mobile as PlayerBot;
            if (bot == null) return; // This AI is only for PlayerBots

            // If the bot is an archer, its top priority is to re-equip its bow.
            if (!bot.PrefersMelee && bot.PreferedCombatSkill == SkillName.Archery)
            {
                // Did we save a ranged weapon? If so, equip it.
                if (m_SavedWeapon != null && m_SavedWeapon is BaseRanged && m_SavedWeapon.IsChildOf(pack))
                {
                    m_Mobile.EquipItem(m_SavedWeapon);
                    m_SavedWeapon = null;
                    m_SavedShield = null; // Can't use a shield, so clear the reference.
                    return; // Job done.
                }
            }

            // For all other bots, or if the archer's bow wasn't the saved weapon,
            // use the standard equip logic.
            if (m_SavedWeapon != null && m_SavedWeapon.IsChildOf(pack))
            {
                m_Mobile.EquipItem(m_SavedWeapon);
            }

            if (m_SavedShield != null && m_SavedShield.IsChildOf(pack))
            {
                m_Mobile.EquipItem(m_SavedShield);
            }

            // Clear saved references after attempting to equip.
            m_SavedWeapon = null;
            m_SavedShield = null;
        }
    }
}
