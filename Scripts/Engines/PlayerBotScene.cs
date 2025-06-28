using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Server.Engines
{
    /// <summary>
    /// Base class for all PlayerBot dynamic scenes
    /// Scenes are temporary scripted events that create emergent gameplay
    /// </summary>
    public abstract class PlayerBotScene
    {
        #region Enums
        public enum SceneType
        {
            War,
            BanditAmbush,
            PKPatrol,
            CrafterMarket
        }

        public enum SceneState
        {
            Preparing,
            Active,
            Concluding,
            Complete
        }
        #endregion

        #region Protected Fields
        protected List<PlayerBot> m_Participants;
        protected List<Mobile> m_Summons; // Pack animals, temporary spawns, etc.
        protected Point3D m_Center;
        protected Map m_Map;
        protected DateTime m_StartTime;
        protected TimeSpan m_Duration;
        protected SceneState m_State;
        protected SceneType m_Type;
        protected string m_Name;
        protected bool m_RequiresPlayers;
        protected static int s_NextSceneId = 1;
        protected int m_SceneId;
        #endregion

        #region Properties
        public SceneType Type { get { return m_Type; } }
        public SceneState CurrentState { get { return m_State; } }
        public Point3D CenterLocation { get { return m_Center; } }
        public Map Map { get { return m_Map; } }
        public DateTime StartTime { get { return m_StartTime; } }
        public TimeSpan Duration { get { return m_Duration; } }
        public string Name { get { return m_Name; } }
        public bool RequiresPlayers { get { return m_RequiresPlayers; } }
        public List<PlayerBot> Participants { get { return new List<PlayerBot>(m_Participants); } }
        public bool IsActive { get { return m_State == SceneState.Active; } }
        public bool IsComplete { get { return m_State == SceneState.Complete; } }
        public int SceneId { get { return m_SceneId; } }
        #endregion

        #region Constructor
        protected PlayerBotScene(SceneType type, string name, Point3D center, Map map, bool requiresPlayers)
        {
            m_Type = type;
            m_Name = name;
            m_Center = center;
            m_Map = map;
            m_RequiresPlayers = requiresPlayers;
            m_Participants = new List<PlayerBot>();
            m_Summons = new List<Mobile>();
            m_State = SceneState.Preparing;
            m_StartTime = DateTime.Now;
            m_SceneId = s_NextSceneId++;
        }
        #endregion

        #region Abstract Methods
        /// <summary>
        /// Check if this scene type can be triggered in the given region
        /// </summary>
        public abstract bool CanTrigger(PlayerBotDirector.RegionProfile region, List<Mobile> nearbyPlayers);

        /// <summary>
        /// Initialize the scene - spawn participants, set up scenario
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Called periodically while scene is active
        /// </summary>
        public abstract void OnTick();

        /// <summary>
        /// Clean up the scene - remove temporary spawns, reset participants
        /// </summary>
        public abstract void Cleanup();
        #endregion

        #region Virtual Methods
        /// <summary>
        /// Called when scene transitions to active state
        /// </summary>
        public virtual void OnActivate()
        {
            m_State = SceneState.Active;
            
            if (PlayerBotConfigurationManager.BehaviorSettings.EnableLogging && 
                PlayerBotConfigurationManager.BehaviorSettings.VerboseEvents)
            {
                Console.WriteLine("[{0}] [PlayerBotScene] Scene '{1}' activated at {2} with {3} participants", 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), m_Name, m_Center, m_Participants.Count);
            }
        }

        /// <summary>
        /// Called when scene is concluding
        /// </summary>
        public virtual void OnConclude()
        {
            m_State = SceneState.Concluding;
            
            if (PlayerBotConfigurationManager.BehaviorSettings.EnableLogging && 
                PlayerBotConfigurationManager.BehaviorSettings.VerboseEvents)
            {
                TimeSpan elapsed = DateTime.Now - m_StartTime;
                Console.WriteLine("[{0}] [PlayerBotScene] Scene '{1}' concluding after {2}", 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), m_Name, FormatTimeSpan(elapsed));
            }
        }

        /// <summary>
        /// Called when scene is complete
        /// </summary>
        public virtual void OnComplete()
        {
            m_State = SceneState.Complete;
            
            if (PlayerBotConfigurationManager.BehaviorSettings.EnableLogging && 
                PlayerBotConfigurationManager.BehaviorSettings.VerboseEvents)
            {
                TimeSpan elapsed = DateTime.Now - m_StartTime;
                Console.WriteLine("[{0}] [PlayerBotScene] Scene '{1}' completed after {2}", 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), m_Name, FormatTimeSpan(elapsed));
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Add a PlayerBot to this scene
        /// </summary>
        protected void AddParticipant(PlayerBot bot)
        {
            if (bot != null && !bot.Deleted && !m_Participants.Contains(bot))
            {
                m_Participants.Add(bot);
                
                // Update bot info in director
                PlayerBotDirector.Instance.UpdateBotInfo(bot, PlayerBotDirector.BotBehaviorState.Fighting, m_Center);
            }
        }

        /// <summary>
        /// Add a temporary spawn (pack animal, etc.) to this scene
        /// </summary>
        protected void AddSummon(Mobile mobile)
        {
            if (mobile != null && !mobile.Deleted && !m_Summons.Contains(mobile))
            {
                m_Summons.Add(mobile);
            }
        }

        /// <summary>
        /// Remove a participant from the scene
        /// </summary>
        protected void RemoveParticipant(PlayerBot bot)
        {
            if (bot != null && m_Participants.Contains(bot))
            {
                m_Participants.Remove(bot);
                
                // Unregister from director
                PlayerBotDirector.Instance.UnregisterBot(bot);
                
                // Delete the bot
                bot.Delete();
            }
        }

        /// <summary>
        /// Check if scene should end due to time or conditions
        /// </summary>
        protected bool ShouldEnd()
        {
            // Time limit exceeded
            if (DateTime.Now - m_StartTime > m_Duration)
                return true;

            // No living participants
            int aliveCount = 0;
            foreach (PlayerBot bot in m_Participants)
            {
                if (bot != null && !bot.Deleted && bot.Alive)
                    aliveCount++;
            }
            
            if (aliveCount == 0)
                return true;

            return false;
        }

        /// <summary>
        /// Spawn a PlayerBot for this scene
        /// </summary>
        protected PlayerBot SpawnBot(PlayerBotPersona.PlayerBotProfile profile, string namePrefix = null)
        {
            PlayerBot bot = new PlayerBot();
            bot.OverridePersona(profile);
            
            // Generate scene-appropriate name
            if (!string.IsNullOrEmpty(namePrefix))
            {
                bot.Name = namePrefix + " " + bot.Name;
            }
            
            // Find spawn location near center
            Point3D spawnLoc = FindSpawnLocation(m_Center, 10);
            if (spawnLoc != Point3D.Zero)
            {
                // Set large home range to allow scene movement
                bot.Home = spawnLoc;
                bot.RangeHome = 2000; // Large range for scene activities
                
                bot.MoveToWorld(spawnLoc, m_Map);
                AddParticipant(bot);
                
                // Register with director
                PlayerBotDirector.Instance.RegisterBot(bot);
                
                return bot;
            }
            else
            {
                bot.Delete();
                return null;
            }
        }

        /// <summary>
        /// Find a valid spawn location near the target point
        /// </summary>
        protected Point3D FindSpawnLocation(Point3D target, int range)
        {
            for (int i = 0; i < 20; i++) // Try up to 20 times
            {
                int x = target.X + Utility.RandomMinMax(-range, range);
                int y = target.Y + Utility.RandomMinMax(-range, range);
                int z = m_Map.GetAverageZ(x, y);
                Point3D p = new Point3D(x, y, z);

                if (m_Map.CanSpawnMobile(p.X, p.Y, p.Z) && !IsWaterTile(p))
                    return p;
            }

            return Point3D.Zero;
        }

        /// <summary>
        /// Check if a location is water or impassable terrain
        /// </summary>
        private bool IsWaterTile(Point3D location)
        {
            LandTile landTile = m_Map.Tiles.GetLandTile(location.X, location.Y);
            StaticTile[] staticTiles = m_Map.Tiles.GetStaticTiles(location.X, location.Y, true);

            // Check land tile
            if (landTile.ID >= 168 && landTile.ID <= 171) // Water tiles
                return true;
            if (landTile.ID >= 310 && landTile.ID <= 311) // More water tiles  
                return true;

            // Check static tiles for water
            foreach (StaticTile tile in staticTiles)
            {
                if (tile.ID >= 0x1796 && tile.ID <= 0x17B2) // Water static tiles
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Send a message to all players in the area
        /// </summary>
        protected void AnnounceToArea(string message, int range = 50)
        {
            Rectangle2D area = new Rectangle2D(
                m_Center.X - range, m_Center.Y - range,
                range * 2, range * 2
            );

            foreach (Server.Network.NetState ns in Server.Network.NetState.Instances)
            {
                if (ns.Mobile != null && ns.Mobile.Player && 
                    ns.Mobile.Map == m_Map && area.Contains(ns.Mobile.Location))
                {
                    ns.Mobile.SendMessage(0x35, message);
                }
            }
        }

        /// <summary>
        /// Set two groups of bots as mutual aggressors
        /// </summary>
        protected void SetMutualAggressors(List<PlayerBot> group1, List<PlayerBot> group2)
        {
            foreach (PlayerBot bot1 in group1)
            {
                if (bot1 == null || bot1.Deleted) continue;
                
                foreach (PlayerBot bot2 in group2)
                {
                    if (bot2 == null || bot2.Deleted) continue;
                    
                    bot1.Aggressors.Add(AggressorInfo.Create(bot2, bot1, false));
                    bot2.Aggressors.Add(AggressorInfo.Create(bot1, bot2, false));
                }
            }
        }

        /// <summary>
        /// Format timespan for logging
        /// </summary>
        protected string FormatTimeSpan(TimeSpan span)
        {
            if (span.TotalMinutes >= 1)
                return string.Format("{0}m {1}s", (int)span.TotalMinutes, span.Seconds);
            else
                return string.Format("{0}s", (int)span.TotalSeconds);
        }
        
        /// <summary>
        /// Get participant count for admin interface
        /// </summary>
        public int GetParticipantCount()
        {
            return m_Participants.Count;
        }
        
        /// <summary>
        /// Get participants list for admin interface
        /// </summary>
        public List<PlayerBot> GetParticipants()
        {
            return new List<PlayerBot>(m_Participants);
        }
        
        /// <summary>
        /// Start the scene (called by admin commands)
        /// </summary>
        public virtual void Start()
        {
            if (m_State == SceneState.Preparing)
            {
                Initialize();
                OnActivate();
            }
        }
        
        /// <summary>
        /// Force end the scene (called by admin commands)
        /// </summary>
        public virtual void ForceEnd()
        {
            if (m_State == SceneState.Active || m_State == SceneState.Preparing)
            {
                OnConclude();
            }
        }
        #endregion

        #region Update Logic
        /// <summary>
        /// Main update method called by scene manager
        /// </summary>
        public void Update()
        {
            try
            {
                switch (m_State)
                {
                    case SceneState.Preparing:
                        Initialize();
                        OnActivate();
                        break;

                    case SceneState.Active:
                        OnTick();
                        
                        if (ShouldEnd())
                        {
                            OnConclude();
                        }
                        break;

                    case SceneState.Concluding:
                        Cleanup();
                        OnComplete();
                        break;

                    case SceneState.Complete:
                        // Scene is done, will be removed by manager
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PlayerBotScene] Error updating scene '{0}': {1}", m_Name, ex.Message);
                
                // Force cleanup on error
                try
                {
                    Cleanup();
                    OnComplete();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        #endregion

        #region Serialization
        public virtual void Serialize(GenericWriter writer)
        {
            writer.Write((int)0); // version

            writer.Write((int)m_Type);
            writer.Write(m_Name);
            writer.Write(m_Center);
            writer.Write(m_Map);
            writer.Write(m_StartTime);
            writer.Write(m_Duration);
            writer.Write((int)m_State);
            writer.Write(m_RequiresPlayers);

            // Serialize participants
            writer.Write(m_Participants.Count);
            foreach (PlayerBot bot in m_Participants)
            {
                writer.Write(bot.Serial);
            }

            // Serialize summons
            writer.Write(m_Summons.Count);
            foreach (Mobile mobile in m_Summons)
            {
                writer.Write(mobile.Serial);
            }
        }

        public virtual void Deserialize(GenericReader reader)
        {
            int version = reader.ReadInt();

            m_Type = (SceneType)reader.ReadInt();
            m_Name = reader.ReadString();
            m_Center = reader.ReadPoint3D();
            m_Map = reader.ReadMap();
            m_StartTime = reader.ReadDateTime();
            m_Duration = reader.ReadTimeSpan();
            m_State = (SceneState)reader.ReadInt();
            m_RequiresPlayers = reader.ReadBool();

            // Deserialize participants
            int participantCount = reader.ReadInt();
            m_Participants = new List<PlayerBot>();
            for (int i = 0; i < participantCount; i++)
            {
                Serial serial = reader.ReadInt();
                Mobile mobile = World.FindMobile(serial);
                PlayerBot bot = mobile as PlayerBot;
                if (bot != null && !bot.Deleted)
                {
                    m_Participants.Add(bot);
                }
            }

            // Deserialize summons
            int summonCount = reader.ReadInt();
            m_Summons = new List<Mobile>();
            for (int i = 0; i < summonCount; i++)
            {
                Serial serial = reader.ReadInt();
                Mobile mobile = World.FindMobile(serial);
                if (mobile != null && !mobile.Deleted)
                {
                    m_Summons.Add(mobile);
                }
            }
        }
        #endregion
    }
} 