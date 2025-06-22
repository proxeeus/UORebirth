using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Server.Engines.Scenes
{
    /// <summary>
    /// War Scene - Creates faction battles between opposing PlayerBot groups
    /// Players can stumble upon these battles and choose to participate or observe
    /// </summary>
    public class WarScene : PlayerBotScene
    {
        #region Private Fields
        private List<PlayerBot> m_Faction1;
        private List<PlayerBot> m_Faction2;
        private string m_Faction1Name;
        private string m_Faction2Name;
        private int m_MinParticipants;
        private int m_MaxParticipants;
        #endregion

        #region Constructor
        public WarScene(Point3D center, Map map) 
            : base(SceneType.War, "Faction War", center, map, false)
        {
            m_Faction1 = new List<PlayerBot>();
            m_Faction2 = new List<PlayerBot>();
            
            // Configuration from settings
            m_MinParticipants = 4; // Minimum 2 per faction
            m_MaxParticipants = 8; // Maximum 4 per faction
            
            // Random duration between 5-10 minutes
            m_Duration = TimeSpan.FromMinutes(Utility.RandomMinMax(5, 10));
            
            // Determine faction types
            DetermineFactionTypes();
        }

        public WarScene(Point3D center, Map map, int maxParticipants) 
            : base(SceneType.War, "Faction War", center, map, false)
        {
            m_Faction1 = new List<PlayerBot>();
            m_Faction2 = new List<PlayerBot>();
            
            // Configuration from settings or parameters
            m_MinParticipants = Math.Max(4, maxParticipants / 2); // Minimum 2 per faction
            m_MaxParticipants = Math.Max(4, maxParticipants); // Use provided max or minimum 4
            
            // Random duration between 5-10 minutes
            m_Duration = TimeSpan.FromMinutes(Utility.RandomMinMax(5, 10));
            
            // Determine faction types
            DetermineFactionTypes();
        }
        #endregion

        #region Faction Setup
        private void DetermineFactionTypes()
        {
            // Different types of wars based on random chance
            int warType = Utility.Random(3);
            
            switch (warType)
            {
                case 0: // PKs vs Good
                    m_Faction1Name = "Murderers";
                    m_Faction2Name = "Defenders";
                    break;
                    
                case 1: // Adventurers vs PKs
                    m_Faction1Name = "Adventurers";
                    m_Faction2Name = "Bandits";
                    break;
                    
                case 2: // Guild War (simulated)
                    m_Faction1Name = "Red Guild";
                    m_Faction2Name = "Blue Guild";
                    break;
            }
            
            m_Name = string.Format("{0} vs {1} Battle", m_Faction1Name, m_Faction2Name);
        }
        #endregion

        #region Abstract Implementation
        public override bool CanTrigger(PlayerBotDirector.RegionProfile region, List<Mobile> nearbyPlayers)
        {
            // Only trigger in wilderness or dangerous areas
            if (region.SafetyLevel == PlayerBotConfigurationManager.SafetyLevel.Safe)
                return false;
            
            // Don't require players (background activity like caravans)
            // Wars can happen in remote areas without witnesses
            
            // Check if area is suitable for combat
            if (!IsAreaSuitableForCombat(region))
                return false;
            
            return true;
        }

        public override void Initialize()
        {
            if (m_State != SceneState.Preparing)
                return;

            try
            {
                // Determine participant count
                int totalParticipants = Utility.RandomMinMax(m_MinParticipants, m_MaxParticipants);
                int faction1Count = totalParticipants / 2;
                int faction2Count = totalParticipants - faction1Count;

                // Spawn faction 1
                SpawnFaction(m_Faction1, faction1Count, PlayerBotPersona.PlayerBotProfile.PlayerKiller, m_Faction1Name);
                
                // Spawn faction 2  
                SpawnFaction(m_Faction2, faction2Count, PlayerBotPersona.PlayerBotProfile.Adventurer, m_Faction2Name);

                // Position factions apart
                PositionFactions();

                // Set them as enemies
                SetMutualAggressors(m_Faction1, m_Faction2);

                // Announce to nearby players
                AnnounceWarStart();

                if (PlayerBotConfigurationManager.BehaviorSettings.EnableLogging && 
                    PlayerBotConfigurationManager.BehaviorSettings.VerboseEvents)
                {
                    Console.WriteLine("[{0}] [WarScene] War initiated: {1} ({2} vs {3}) at {4}", 
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), m_Name, 
                        m_Faction1.Count, m_Faction2.Count, m_Center);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WarScene] Error initializing war scene: {0}", ex.Message);
                m_State = SceneState.Complete; // Force cleanup
            }
        }

        public override void OnTick()
        {
            if (m_State != SceneState.Active)
                return;

            try
            {
                // Check if one faction has won
                CheckVictoryConditions();
                
                // Ensure bots are still fighting
                MaintainCombat();
                
                // Periodic announcements
                if (Utility.Random(100) < 10) // 10% chance per tick
                {
                    AnnounceWarProgress();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WarScene] Error during war tick: {0}", ex.Message);
            }
        }

        public override void Cleanup()
        {
            try
            {
                // Announce war end
                AnnounceWarEnd();

                // Clean up any remaining aggressors
                ClearAggressors();

                // Remove scene participants (they'll be managed normally now)
                foreach (PlayerBot bot in m_Faction1)
                {
                    if (bot != null && !bot.Deleted)
                    {
                        RemoveParticipant(bot);
                    }
                }

                foreach (PlayerBot bot in m_Faction2)
                {
                    if (bot != null && !bot.Deleted)
                    {
                        RemoveParticipant(bot);
                    }
                }

                // Clean up any summons
                foreach (Mobile summon in m_Summons)
                {
                    if (summon != null && !summon.Deleted)
                    {
                        summon.Delete();
                    }
                }

                if (PlayerBotConfigurationManager.BehaviorSettings.EnableLogging && 
                    PlayerBotConfigurationManager.BehaviorSettings.VerboseEvents)
                {
                    Console.WriteLine("[{0}] [WarScene] War scene '{1}' cleaned up", 
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), m_Name);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WarScene] Error during cleanup: {0}", ex.Message);
            }
        }
        #endregion

        #region Helper Methods
        private bool IsAreaSuitableForCombat(PlayerBotDirector.RegionProfile region)
        {
            // Check if area has enough space for combat
            if (region.Area.Width < 40 || region.Area.Height < 40)
                return false;

            // Could add more checks here (not in towns, etc.)
            return true;
        }

        private void SpawnFaction(List<PlayerBot> faction, int count, PlayerBotPersona.PlayerBotProfile profile, string factionName)
        {
            for (int i = 0; i < count; i++)
            {
                PlayerBot bot = SpawnBot(profile, factionName);
                if (bot != null)
                {
                    faction.Add(bot);
                    
                    // Give war-appropriate speech
                    Timer.DelayCall(TimeSpan.FromSeconds(Utility.RandomDouble() * 5), new TimerCallback(delegate()
                    {
                        if (bot != null && !bot.Deleted)
                        {
                            string[] warCries = GetWarCries(factionName);
                            if (warCries.Length > 0)
                            {
                                bot.SayWithHue(warCries[Utility.Random(warCries.Length)]);
                            }
                        }
                    }));
                }
            }
        }

        private void PositionFactions()
        {
            // Position faction 1 to the west
            Point3D faction1Center = new Point3D(m_Center.X - 20, m_Center.Y, m_Center.Z);
            PositionGroup(m_Faction1, faction1Center, 8);

            // Position faction 2 to the east
            Point3D faction2Center = new Point3D(m_Center.X + 20, m_Center.Y, m_Center.Z);
            PositionGroup(m_Faction2, faction2Center, 8);
        }

        private void PositionGroup(List<PlayerBot> group, Point3D center, int spread)
        {
            foreach (PlayerBot bot in group)
            {
                if (bot == null || bot.Deleted) continue;

                Point3D newPos = FindSpawnLocation(center, spread);
                if (newPos != Point3D.Zero)
                {
                    bot.MoveToWorld(newPos, m_Map);
                    
                    // Face towards the enemy
                    if (group == m_Faction1)
                        bot.Direction = Direction.East;
                    else
                        bot.Direction = Direction.West;
                }
            }
        }

        private void CheckVictoryConditions()
        {
            int faction1Alive = CountAliveBots(m_Faction1);
            int faction2Alive = CountAliveBots(m_Faction2);

            if (faction1Alive == 0 && faction2Alive > 0)
            {
                AnnounceVictory(m_Faction2Name);
                OnConclude();
            }
            else if (faction2Alive == 0 && faction1Alive > 0)
            {
                AnnounceVictory(m_Faction1Name);
                OnConclude();
            }
            else if (faction1Alive == 0 && faction2Alive == 0)
            {
                AnnounceToArea("The battle ends with no survivors!");
                OnConclude();
            }
        }

        private int CountAliveBots(List<PlayerBot> faction)
        {
            int count = 0;
            foreach (PlayerBot bot in faction)
            {
                if (bot != null && !bot.Deleted && bot.Alive)
                    count++;
            }
            return count;
        }

        private void MaintainCombat()
        {
            // Ensure bots are still aggressive towards each other
            foreach (PlayerBot bot1 in m_Faction1)
            {
                if (bot1 == null || bot1.Deleted || !bot1.Alive) continue;
                
                if (bot1.Combatant == null)
                {
                    // Find nearest enemy to attack
                    PlayerBot nearestEnemy = FindNearestEnemy(bot1, m_Faction2);
                    if (nearestEnemy != null)
                    {
                        bot1.Combatant = nearestEnemy;
                    }
                }
            }

            foreach (PlayerBot bot2 in m_Faction2)
            {
                if (bot2 == null || bot2.Deleted || !bot2.Alive) continue;
                
                if (bot2.Combatant == null)
                {
                    // Find nearest enemy to attack
                    PlayerBot nearestEnemy = FindNearestEnemy(bot2, m_Faction1);
                    if (nearestEnemy != null)
                    {
                        bot2.Combatant = nearestEnemy;
                    }
                }
            }
        }

        private PlayerBot FindNearestEnemy(PlayerBot bot, List<PlayerBot> enemies)
        {
            PlayerBot nearest = null;
            double nearestDistance = double.MaxValue;

            foreach (PlayerBot enemy in enemies)
            {
                if (enemy == null || enemy.Deleted || !enemy.Alive) continue;

                double distance = bot.GetDistanceToSqrt(enemy);
                if (distance < nearestDistance && distance <= 12) // Within combat range
                {
                    nearest = enemy;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private void ClearAggressors()
        {
            List<PlayerBot> allBots = new List<PlayerBot>();
            allBots.AddRange(m_Faction1);
            allBots.AddRange(m_Faction2);

            foreach (PlayerBot bot in allBots)
            {
                if (bot == null || bot.Deleted) continue;

                bot.Aggressors.Clear();
                bot.Combatant = null;
            }
        }

        private string[] GetWarCries(string factionName)
        {
            switch (factionName.ToLower())
            {
                case "murderers":
                case "bandits":
                    return new string[] 
                    {
                        "Death to all!",
                        "Your blood will stain the ground!",
                        "No mercy!",
                        "Kill them all!"
                    };

                case "defenders":
                case "adventurers":
                    return new string[] 
                    {
                        "For justice!",
                        "We will not yield!",
                        "Protect the innocent!",
                        "Stand and fight!"
                    };

                case "red guild":
                    return new string[] 
                    {
                        "Red guild forever!",
                        "Our enemies fall!",
                        "Victory is ours!"
                    };

                case "blue guild":
                    return new string[] 
                    {
                        "Blue guild stands strong!",
                        "We fight as one!",
                        "Honor above all!"
                    };

                default:
                    return new string[] 
                    {
                        "To battle!",
                        "Fight!",
                        "Charge!"
                    };
            }
        }
        #endregion

        #region Announcements
        private void AnnounceWarStart()
        {
            string[] startMessages = new string[]
            {
                "You hear the clash of steel nearby!",
                "The sound of battle echoes through the area!",
                "War cries ring out from nearby!",
                "You hear the sounds of a fierce battle!"
            };

            AnnounceToArea(startMessages[Utility.Random(startMessages.Length)]);
        }

        private void AnnounceWarProgress()
        {
            string[] progressMessages = new string[]
            {
                "The battle rages on!",
                "Steel rings against steel!",
                "The fight continues!",
                "Neither side gives ground!"
            };

            AnnounceToArea(progressMessages[Utility.Random(progressMessages.Length)]);
        }

        private void AnnounceVictory(string winnerName)
        {
            AnnounceToArea(string.Format("The {0} have emerged victorious!", winnerName));
        }

        private void AnnounceWarEnd()
        {
            AnnounceToArea("The sounds of battle fade away...");
        }
        #endregion

        #region Serialization
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            
            writer.Write((int)0); // version
            
            writer.Write(m_Faction1Name);
            writer.Write(m_Faction2Name);
            writer.Write(m_MinParticipants);
            writer.Write(m_MaxParticipants);
            
            // Serialize faction 1
            writer.Write(m_Faction1.Count);
            foreach (PlayerBot bot in m_Faction1)
            {
                writer.Write(bot.Serial);
            }
            
            // Serialize faction 2
            writer.Write(m_Faction2.Count);
            foreach (PlayerBot bot in m_Faction2)
            {
                writer.Write(bot.Serial);
            }
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            
            int version = reader.ReadInt();
            
            m_Faction1Name = reader.ReadString();
            m_Faction2Name = reader.ReadString();
            m_MinParticipants = reader.ReadInt();
            m_MaxParticipants = reader.ReadInt();
            
            // Deserialize faction 1
            int faction1Count = reader.ReadInt();
            m_Faction1 = new List<PlayerBot>();
            for (int i = 0; i < faction1Count; i++)
            {
                Serial serial = reader.ReadInt();
                Mobile mobile = World.FindMobile(serial);
                PlayerBot bot = mobile as PlayerBot;
                if (bot != null && !bot.Deleted)
                {
                    m_Faction1.Add(bot);
                }
            }
            
            // Deserialize faction 2
            int faction2Count = reader.ReadInt();
            m_Faction2 = new List<PlayerBot>();
            for (int i = 0; i < faction2Count; i++)
            {
                Serial serial = reader.ReadInt();
                Mobile mobile = World.FindMobile(serial);
                PlayerBot bot = mobile as PlayerBot;
                if (bot != null && !bot.Deleted)
                {
                    m_Faction2.Add(bot);
                }
            }
        }
        #endregion

        #region Admin Methods
        /// <summary>
        /// Get count of faction A members
        /// </summary>
        public int GetFactionACount()
        {
            int count = 0;
            foreach (PlayerBot bot in m_Faction1)
            {
                if (bot != null && !bot.Deleted)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Get count of faction B members
        /// </summary>
        public int GetFactionBCount()
        {
            int count = 0;
            foreach (PlayerBot bot in m_Faction2)
            {
                if (bot != null && !bot.Deleted)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Get the war type for this scene
        /// </summary>
        public string WarType
        {
            get { return string.Format("{0} vs {1}", m_Faction1Name, m_Faction2Name); }
        }
        #endregion
    }
} 