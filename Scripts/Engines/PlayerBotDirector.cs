using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;

namespace Server.Engines
{
    /// <summary>
    /// Phase-1 implementation of the PlayerBotDirector.
    /// Keeps overall bot population inside defined region targets.
    /// Later phases (travel, scenes, etc.) will extend this.
    /// </summary>
    public class PlayerBotDirector
    {
        #region Nested Types
        /// <summary>
        /// Bot behavior states for tracking what bots are currently doing
        /// </summary>
        public enum BotBehaviorState
        {
            Idle,
            Shopping,
            Socializing,
            Banking,
            Exploring,
            Fighting
        }

        /// <summary>
        /// Types of points of interest for bot behavior
        /// </summary>
        public enum POIType
        {
            Bank,
            Shop,
            Tavern,
            Healer,
            Dungeon,
            Landmark,
            Waypoint
        }

        /// <summary>
        /// Information about a registered PlayerBot for lifecycle tracking
        /// </summary>
        private class PlayerBotInfo
        {
            public PlayerBot Bot { get; private set; }
            public DateTime SpawnTime { get; private set; }
            public BotBehaviorState CurrentState { get; set; }
            public Point3D Destination { get; set; }
            public DateTime LastStateChange { get; set; }

            public PlayerBotInfo(PlayerBot bot)
            {
                Bot = bot;
                SpawnTime = DateTime.Now;
                CurrentState = BotBehaviorState.Idle;
                Destination = Point3D.Zero;
                LastStateChange = DateTime.Now;
            }
        }

        /// <summary>
        /// Points of interest for bot navigation and behavior
        /// </summary>
        private class PointOfInterest
        {
            public string Name { get; private set; }
            public Map Map { get; private set; }
            public Point3D Location { get; private set; }
            public POIType Type { get; private set; }

            public PointOfInterest(string name, Map map, Point3D location, POIType type)
            {
                Name = name;
                Map = map;
                Location = location;
                Type = type;
            }
        }

        /// <summary>
        /// Region profile for population management
        /// </summary>
        public class RegionProfile
        {
            private string _name;
            private Map _map;
            private Rectangle2D _area;
            private int _min;
            private int _max;
            private PlayerBotConfigurationManager.SafetyLevel _safetyLevel;

            public string Name { get { return _name; } }
            public Map Map { get { return _map; } }
            public Rectangle2D Area { get { return _area; } }
            public int Min { get { return _min; } }
            public int Max { get { return _max; } }
            public PlayerBotConfigurationManager.SafetyLevel SafetyLevel { get { return _safetyLevel; } }

            public RegionProfile(string name, Map map, Rectangle2D area, int min, int max, PlayerBotConfigurationManager.SafetyLevel safetyLevel)
            {
                _name = name;
                _map = map;
                _area = area;
                _min = min;
                _max = max;
                _safetyLevel = safetyLevel;
            }
        }
        #endregion

        #region Singleton
        private static readonly PlayerBotDirector m_Instance = new PlayerBotDirector();
        public static PlayerBotDirector Instance { get { return m_Instance; } }
        #endregion

        #region Configuration Properties
        // Configuration is now loaded from files via PlayerBotConfigurationManager
        // These properties provide easy access to current settings
        private static PlayerBotConfigurationManager.BehaviorConfig Config
        {
            get { return PlayerBotConfigurationManager.BehaviorSettings; }
        }
        #endregion

        #region Private Fields
        private Timer m_Timer;
        private Timer m_BehaviorTimer; // Phase 2: Behavior management timer
        private Timer m_SceneTimer; // Phase 2: Scene management timer
        private List<RegionProfile> m_Regions;
        private List<PointOfInterest> m_PointsOfInterest; // Phase 2: Notable locations
        private List<PlayerBotScene> m_ActiveScenes; // Phase 2: Dynamic scenes
        
        // Bot registry for efficient tracking
        private Dictionary<Serial, PlayerBotInfo> m_RegisteredBots;
        private object m_RegistryLock = new object();
        #endregion

        // Helper method to get formatted timestamp for logging
        private static string GetTimestamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private PlayerBotDirector()
        {
            // Initialize configuration system first
            PlayerBotConfigurationManager.Initialize();
            
            m_Regions = new List<RegionProfile>();
            m_RegisteredBots = new Dictionary<Serial, PlayerBotInfo>();
            m_PointsOfInterest = new List<PointOfInterest>();
            m_ActiveScenes = new List<PlayerBotScene>();
            
            // Load regions and POIs from configuration files
            LoadRegionsFromConfig();
            LoadPointsOfInterestFromConfig();

            // Phase 1: Population management timer (using config values)
            m_Timer = Timer.DelayCall(TimeSpan.FromSeconds(Config.StartupDelaySeconds), TimeSpan.FromSeconds(Config.PopulationTickSeconds), new TimerCallback(OnPopulationTick));
            
            // Phase 2: Behavior management timer (starts a bit later to let population stabilize)
            m_BehaviorTimer = Timer.DelayCall(TimeSpan.FromSeconds(Config.StartupDelaySeconds + 15), TimeSpan.FromSeconds(Config.BehaviorTickSeconds), new TimerCallback(OnBehaviorTick));
            
            // Phase 2: Scene management timer (starts even later to allow behaviors to establish)
            m_SceneTimer = Timer.DelayCall(TimeSpan.FromSeconds(Config.StartupDelaySeconds + 30), TimeSpan.FromSeconds(60), new TimerCallback(OnSceneTick));
            
            if (Config.EnableLogging)
            {
                Console.WriteLine("[{0}] [PlayerBotDirector] Constructor completed. Population timer starts in {1}s, behavior timer in {2}s", 
                    GetTimestamp(), Config.StartupDelaySeconds, Config.StartupDelaySeconds + 15);
            }
        }

        [CallPriority(int.MaxValue)]
        public static void Initialize()
        {
            // Accessing Instance forces the singleton to construct and start the timer.
            PlayerBotDirector unused = Instance;
            
            // CRITICAL: Register any PlayerBots that were deserialized before the director was initialized
            Instance.RegisterExistingPlayerBots();
            
            Console.WriteLine("[{0}] [PlayerBotDirector] Initialized (phase 1 + 2) - Debug mode: {1}", GetTimestamp(), Config.EnableLogging ? "ON" : "OFF");
        }

        #region Bot Registration System
        /// <summary>
        /// Called when a PlayerBot is created/spawned to register it with the director
        /// </summary>
        public void RegisterBot(PlayerBot bot)
        {
            if (bot == null || bot.Deleted)
                return;

            lock (m_RegistryLock)
            {
                // Avoid duplicate registrations
                if (m_RegisteredBots.ContainsKey(bot.Serial))
                {
                    if (Config.EnableLogging && Config.VerboseSpawning)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' (Serial: {2}) already registered, skipping", 
                            GetTimestamp(), bot.Name, bot.Serial);
                    return;
                }

                PlayerBotInfo info = new PlayerBotInfo(bot);
                m_RegisteredBots[bot.Serial] = info;
                
                if (Config.EnableLogging && Config.VerboseSpawning)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Registered bot '{1}' (Serial: {2}) - Total registered: {3}", 
                        GetTimestamp(), bot.Name, bot.Serial, m_RegisteredBots.Count);
            }
        }

        /// <summary>
        /// Called when a PlayerBot is deleted/dies to unregister it from the director
        /// </summary>
        public void UnregisterBot(PlayerBot bot)
        {
            if (bot == null)
                return;

            lock (m_RegistryLock)
            {
                if (m_RegisteredBots.ContainsKey(bot.Serial))
                {
                    PlayerBotInfo info = m_RegisteredBots[bot.Serial];
                    TimeSpan lifespan = DateTime.Now - info.SpawnTime;
                    m_RegisteredBots.Remove(bot.Serial);
                    
                    if (Config.EnableLogging && Config.VerboseSpawning)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Unregistered bot '{1}' (Serial: {2}) - Lived for {3} - Total registered: {4}", 
                            GetTimestamp(), bot.Name, bot.Serial, FormatTimeSpan(lifespan), m_RegisteredBots.Count);
                }
            }
        }

        /// <summary>
        /// Register any existing PlayerBots that were deserialized before the director was initialized
        /// This fixes the "unmanaged bots" issue during server startup
        /// </summary>
        private void RegisterExistingPlayerBots()
        {
            int registeredCount = 0;
            int alreadyRegisteredCount = 0;
            
            foreach (Mobile mobile in World.Mobiles.Values)
            {
                PlayerBot bot = mobile as PlayerBot;
                if (bot != null && !bot.Deleted)
                {
                    lock (m_RegistryLock)
                    {
                        if (!m_RegisteredBots.ContainsKey(bot.Serial))
                        {
                            PlayerBotInfo info = new PlayerBotInfo(bot);
                            m_RegisteredBots[bot.Serial] = info;
                            registeredCount++;
                        }
                        else
                        {
                            alreadyRegisteredCount++;
                        }
                    }
                }
            }
            
            if (Config.EnableLogging)
            {
                Console.WriteLine("[{0}] [PlayerBotDirector] Post-deserialization registration sweep: {1} newly registered, {2} already registered", 
                    GetTimestamp(), registeredCount, alreadyRegisteredCount);
            }
        }

        /// <summary>
        /// Get current registered bot count (thread-safe)
        /// </summary>
        public int GetRegisteredBotCount()
        {
            lock (m_RegistryLock)
            {
                return m_RegisteredBots.Count;
            }
        }

        /// <summary>
        /// Get total PlayerBot count in the world (for comparison with registered count)
        /// </summary>
        public int GetWorldPlayerBotCount()
        {
            int count = 0;
            foreach (Mobile mobile in World.Mobiles.Values)
            {
                if (mobile is PlayerBot && !mobile.Deleted)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Diagnostic method to identify unmanaged PlayerBots
        /// </summary>
        public List<PlayerBot> GetUnmanagedPlayerBots()
        {
            List<PlayerBot> unmanaged = new List<PlayerBot>();
            
            foreach (Mobile mobile in World.Mobiles.Values)
            {
                PlayerBot bot = mobile as PlayerBot;
                if (bot != null && !bot.Deleted)
                {
                    lock (m_RegistryLock)
                    {
                        if (!m_RegisteredBots.ContainsKey(bot.Serial))
                        {
                            unmanaged.Add(bot);
                        }
                    }
                }
            }
            
            return unmanaged;
        }

        /// <summary>
        /// Force registration of all unmanaged PlayerBots (admin command helper)
        /// </summary>
        public int ForceRegisterUnmanagedBots()
        {
            List<PlayerBot> unmanaged = GetUnmanagedPlayerBots();
            
            foreach (PlayerBot bot in unmanaged)
            {
                RegisterBot(bot);
            }
            
            return unmanaged.Count;
        }

        /// <summary>
        /// Get registered bots in a specific region (thread-safe)
        /// </summary>
        private List<PlayerBot> GetBotsInRegion(RegionProfile profile)
        {
            List<PlayerBot> result = new List<PlayerBot>();
            
            lock (m_RegistryLock)
            {
                foreach (PlayerBotInfo info in m_RegisteredBots.Values)
                {
                    PlayerBot bot = info.Bot;
                    if (bot != null && !bot.Deleted && bot.Map == profile.Map && profile.Area.Contains(bot.Location))
                    {
                        result.Add(bot);
                    }
                }
            }
            
            return result;
        }

        /// <summary>
        /// Get bot info for a specific bot (thread-safe)
        /// </summary>
        private PlayerBotInfo GetBotInfo(PlayerBot bot)
        {
            lock (m_RegistryLock)
            {
                if (m_RegisteredBots.ContainsKey(bot.Serial))
                    return m_RegisteredBots[bot.Serial];
            }
            return null;
        }

        /// <summary>
        /// Update bot info (thread-safe)
        /// </summary>
        public void UpdateBotInfo(PlayerBot bot, BotBehaviorState newState, Point3D destination)
        {
            lock (m_RegistryLock)
            {
                if (m_RegisteredBots.ContainsKey(bot.Serial))
                {
                    PlayerBotInfo info = m_RegisteredBots[bot.Serial];
                    info.CurrentState = newState;
                    info.Destination = destination;
                    info.LastStateChange = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// Clean up any stale bot registrations (bots that were deleted without proper unregistration)
        /// </summary>
        private void CleanupStaleRegistrations()
        {
            List<Serial> toRemove = new List<Serial>();
            
            lock (m_RegistryLock)
            {
                foreach (KeyValuePair<Serial, PlayerBotInfo> kvp in m_RegisteredBots)
                {
                    if (kvp.Value.Bot == null || kvp.Value.Bot.Deleted)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                
                foreach (Serial serial in toRemove)
                {
                    PlayerBotInfo info = m_RegisteredBots[serial];
                    TimeSpan lifespan = DateTime.Now - info.SpawnTime;
                    m_RegisteredBots.Remove(serial);
                    
                    if (Config.EnableLogging && Config.VerboseSpawning)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Cleaned up stale registration (Serial: {1}) - Lived for {2} - Total registered: {3}", 
                            GetTimestamp(), serial, FormatTimeSpan(lifespan), m_RegisteredBots.Count);
                }
            }
        }

        private string FormatTimeSpan(TimeSpan span)
        {
            if (span.TotalDays >= 1)
                return string.Format("{0}d {1}h {2}m", (int)span.TotalDays, span.Hours, span.Minutes);
            else if (span.TotalHours >= 1)
                return string.Format("{0}h {1}m {2}s", span.Hours, span.Minutes, span.Seconds);
            else if (span.TotalMinutes >= 1)
                return string.Format("{0}m {1}s", span.Minutes, span.Seconds);
            else
                return string.Format("{0}s", span.Seconds);
        }
        #endregion

        /// <summary>
        /// Load regions from configuration files instead of hardcoded values
        /// </summary>
        private void LoadRegionsFromConfig()
        {
            if (Config.EnableLogging && Config.VerboseSpawning)
                Console.WriteLine("[{0}] [PlayerBotDirector] Loading regions from configuration...", GetTimestamp());

            foreach (PlayerBotConfigurationManager.RegionConfig regionConfig in PlayerBotConfigurationManager.Regions.Values)
            {
                if (regionConfig.Active)
                {
                    RegionProfile profile = new RegionProfile(regionConfig.Name, regionConfig.Map, regionConfig.Bounds, regionConfig.MinBots, regionConfig.MaxBots, regionConfig.SafetyLevel);
                    m_Regions.Add(profile);
                }
            }

            if (Config.EnableLogging && Config.VerboseSpawning)
            {
                Console.WriteLine("[{0}] [PlayerBotDirector] Loaded {1} active regions:", GetTimestamp(), m_Regions.Count);
                foreach (RegionProfile profile in m_Regions)
                {
                    Console.WriteLine("[{0}]   - {1} on {2}: {3}x{4} area, target {5}-{6} bots", 
                        GetTimestamp(), profile.Name, profile.Map, 
                        profile.Area.Width, profile.Area.Height, 
                        profile.Min, profile.Max);
                }
            }
        }

        /// <summary>
        /// Load points of interest from configuration files instead of hardcoded values
        /// </summary>
        private void LoadPointsOfInterestFromConfig()
        {
            if (Config.EnableLogging && Config.VerboseBehaviors)
                Console.WriteLine("[{0}] [PlayerBotDirector] Loading points of interest from configuration...", GetTimestamp());

            foreach (PlayerBotConfigurationManager.POIConfig poiConfig in PlayerBotConfigurationManager.PointsOfInterest.Values)
            {
                // Only load POIs for active regions
                bool regionActive = false;
                foreach (PlayerBotConfigurationManager.RegionConfig regionConfig in PlayerBotConfigurationManager.Regions.Values)
                {
                    if (regionConfig.Name == poiConfig.Region && regionConfig.Active)
                    {
                        regionActive = true;
                        break;
                    }
                }

                if (regionActive || string.IsNullOrEmpty(poiConfig.Region)) // Include regionless POIs (waypoints)
                {
                    PointOfInterest poi = new PointOfInterest(poiConfig.Name, poiConfig.Map, poiConfig.Location, poiConfig.Type);
                    m_PointsOfInterest.Add(poi);
                }
            }

            if (Config.EnableLogging && Config.VerboseBehaviors)
            {
                Console.WriteLine("[{0}] [PlayerBotDirector] Loaded {1} points of interest:", GetTimestamp(), m_PointsOfInterest.Count);
                foreach (PointOfInterest poi in m_PointsOfInterest)
                {
                    Console.WriteLine("[{0}]   - {1} ({2}) at {3}", 
                        GetTimestamp(), poi.Name, poi.Type, poi.Location);
                }
            }
        }

        #region Population tick
        private void OnPopulationTick()
        {
            try
            {
                if (Config.EnableLogging)
                    Console.WriteLine("[{0}] [PlayerBotDirector] === Population Tick Started ===", GetTimestamp());

                // Clean up any stale registrations first
                CleanupStaleRegistrations();

                int totalRegistered = GetRegisteredBotCount();
                if (Config.EnableLogging)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Found {1} registered PlayerBots (global cap: {2})", GetTimestamp(), totalRegistered, Config.GlobalCap);

                // Global cap guard
                if (totalRegistered >= Config.GlobalCap)
                {
                    if (Config.EnableLogging)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Global cap reached! No new bots will be spawned.", GetTimestamp());
                    return; // nothing to do, shard is at capacity
                }

                int totalSpawned = 0;
                foreach (RegionProfile profile in m_Regions)
                {
                    int spawned = EnsureRegionPopulation(profile);
                    totalSpawned += spawned;
                }

                if (Config.EnableLogging)
                {
                    if (totalSpawned > 0)
                        Console.WriteLine("[{0}] [PlayerBotDirector] === Population Tick Complete: {1} new bots spawned ===", GetTimestamp(), totalSpawned);
                    else
                        Console.WriteLine("[{0}] [PlayerBotDirector] === Population Tick Complete: All regions satisfied ===", GetTimestamp());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[{0}] [PlayerBotDirector] Exception in population tick: {1}", GetTimestamp(), ex);
            }
        }

        private int EnsureRegionPopulation(RegionProfile profile)
        {
            // Get bots in this region using the efficient registry
            List<PlayerBot> botsInRegion = GetBotsInRegion(profile);
            int count = botsInRegion.Count;

            if (Config.EnableLogging && Config.VerboseSpawning)
                Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': {2}/{3} bots present (target: {4}-{5})", 
                    GetTimestamp(), profile.Name, count, profile.Max, profile.Min, profile.Max);

            if (count >= profile.Min)
            {
                if (Config.EnableLogging && Config.VerboseSpawning && count < profile.Max)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': Population satisfied (could spawn {2} more)", 
                        GetTimestamp(), profile.Name, profile.Max - count);
                else if (Config.EnableLogging && Config.VerboseSpawning)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': At maximum capacity", GetTimestamp(), profile.Name);
                return 0; // already satisfied
            }

            int toSpawn = Math.Min(profile.Min - count, profile.Max - count);

            // Respect global cap
            int totalRegistered = GetRegisteredBotCount();
            int globalRemaining = Config.GlobalCap - totalRegistered;
            if (toSpawn > globalRemaining)
            {
                if (Config.EnableLogging && Config.VerboseSpawning)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': Wanted to spawn {2} bots, but global cap limits to {3}", 
                        GetTimestamp(), profile.Name, toSpawn, globalRemaining);
                toSpawn = globalRemaining;
            }

            if (Config.EnableLogging && Config.VerboseSpawning)
                Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': Attempting to spawn {2} bots...", GetTimestamp(), profile.Name, toSpawn);

            int actuallySpawned = 0;
            for (int i = 0; i < toSpawn; i++)
            {
                if (SpawnBotInRegion(profile))
                    actuallySpawned++;
            }

            if (Config.EnableLogging && Config.VerboseSpawning)
            {
                if (actuallySpawned == toSpawn)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': Successfully spawned {2} bots", GetTimestamp(), profile.Name, actuallySpawned);
                else
                    Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': Only spawned {2}/{3} bots (spawn location failures)", 
                        GetTimestamp(), profile.Name, actuallySpawned, toSpawn);
            }

            return actuallySpawned;
        }
        #endregion

        #region Spawning helpers
        private bool SpawnBotInRegion(RegionProfile profile)
        {
            Point3D loc = FindSpawnLocation(profile);
            if (loc == Point3D.Zero)
            {
                if (Config.EnableLogging && Config.VerboseSpawning)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Failed to find spawn location in region '{1}' after {2} attempts", GetTimestamp(), profile.Name, Config.SpawnLocationAttempts);
                return false; // failed to find safe spot
            }

            PlayerBot bot = new PlayerBot(); // Uses default random persona
            
            // Override the persona based on region safety level
            PlayerBotPersona.PlayerBotProfile assignedProfile = DeterminePersonaForRegion(profile.SafetyLevel);
            bot.OverridePersona(assignedProfile);
            
            // Set home and MUCH higher wander range for natural-looking movement
            bot.Home = loc;
            
            // Set wander range based on safety level - higher ranges for more natural movement
            switch (profile.SafetyLevel)
            {
                case PlayerBotConfigurationManager.SafetyLevel.Safe:
                    bot.RangeHome = Utility.RandomMinMax(150, 250); // Towns/safe areas
                    break;
                case PlayerBotConfigurationManager.SafetyLevel.Dangerous:
                    bot.RangeHome = Utility.RandomMinMax(200, 300); // Dangerous areas
                    break;
                case PlayerBotConfigurationManager.SafetyLevel.Wilderness:
                    bot.RangeHome = Utility.RandomMinMax(300, 500); // Wilderness areas
                    break;
                default:
                    bot.RangeHome = 200;
                    break;
            }
            
            // Set AI to wander mode - no destinations, just natural wandering
            if (bot.AIObject != null)
            {
                bot.AIObject.Action = ActionType.Wander;
            }
            
            bot.MoveToWorld(loc, profile.Map);

            // Register the bot with the director
            RegisterBot(bot);

            if (Config.EnableLogging && Config.VerboseSpawning)
            {
                string persona = "Unknown";
                if (bot.PlayerBotProfile != null)
                    persona = bot.PlayerBotProfile.ToString();
                
                string experience = "Unknown";
                if (bot.PlayerBotExperience != null)
                    experience = bot.PlayerBotExperience.ToString();

                Console.WriteLine("[{0}] [PlayerBotDirector] Spawned running bot '{1}' ({2} {3}) at {4} in region '{5}' (Safety: {6}, WanderRange: {7})", 
                    GetTimestamp(), bot.Name, experience, persona, loc, profile.Name, profile.SafetyLevel, bot.RangeHome);
            }

            return true;
        }

        /// <summary>
        /// Determines the appropriate persona for a bot based on the region's safety level
        /// </summary>
        private PlayerBotPersona.PlayerBotProfile DeterminePersonaForRegion(PlayerBotConfigurationManager.SafetyLevel safetyLevel)
        {
            int adventurerPercent, crafterPercent, pkPercent;
            
            // Get percentages based on safety level
            switch (safetyLevel)
            {
                case PlayerBotConfigurationManager.SafetyLevel.Safe:
                    adventurerPercent = Config.SafeAdventurerPercent;
                    crafterPercent = Config.SafeCrafterPercent;
                    pkPercent = Config.SafePlayerKillerPercent;
                    break;
                    
                case PlayerBotConfigurationManager.SafetyLevel.Dangerous:
                    adventurerPercent = Config.DangerousAdventurerPercent;
                    crafterPercent = Config.DangerousCrafterPercent;
                    pkPercent = Config.DangerousPlayerKillerPercent;
                    break;
                    
                case PlayerBotConfigurationManager.SafetyLevel.Wilderness:
                default:
                    adventurerPercent = Config.WildernessAdventurerPercent;
                    crafterPercent = Config.WildernessCrafterPercent;
                    pkPercent = Config.WildernessPlayerKillerPercent;
                    break;
            }
            
            // Generate random number and determine persona
            int roll = Utility.Random(100);
            
            if (roll < adventurerPercent)
                return PlayerBotPersona.PlayerBotProfile.Adventurer;
            else if (roll < adventurerPercent + crafterPercent)
                return PlayerBotPersona.PlayerBotProfile.Crafter;
            else
                return PlayerBotPersona.PlayerBotProfile.PlayerKiller;
        }

        private Point3D FindSpawnLocation(RegionProfile profile)
        {
            Map map = profile.Map;
            for (int i = 0; i < Config.SpawnLocationAttempts; i++) // try up to configured attempts
            {
                int x = Utility.RandomMinMax(profile.Area.Start.X, profile.Area.End.X);
                int y = Utility.RandomMinMax(profile.Area.Start.Y, profile.Area.End.Y);
                int z = map.GetAverageZ(x, y);
                Point3D p = new Point3D(x, y, z);

                if (CanSpawnAt(map, p))
                {
                    if (Config.EnableLogging && Config.VerboseSpawning)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Found spawn location {1} in region '{2}' (attempt {3}/{4})", 
                            GetTimestamp(), p, profile.Name, i + 1, Config.SpawnLocationAttempts);
                    return p;
                }
            }

            return Point3D.Zero;
        }

        private bool CanSpawnAt(Map map, Point3D p)
        {
            if (map == null)
                return false;

            if (!map.CanSpawnMobile(p.X, p.Y, p.Z))
                return false;

            return true;
        }
        #endregion

        #region Phase 2: Behavior Management
        private void OnBehaviorTick()
        {
            try
            {
                if (Config.EnableLogging && Config.VerboseBehaviors)
                    Console.WriteLine("[{0}] [PlayerBotDirector] === Behavior Tick Started ===", GetTimestamp());

                List<PlayerBot> allBots = GetAllRegisteredBots();
                
                if (allBots.Count == 0)
                {
                    if (Config.EnableLogging && Config.VerboseBehaviors)
                        Console.WriteLine("[{0}] [PlayerBotDirector] No bots registered for behavior management", GetTimestamp());
                    return;
                }

                if (Config.EnableLogging && Config.VerboseBehaviors)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Managing behaviors for {1} bots", GetTimestamp(), allBots.Count);

                // Behavior management: ensure proper wandering, location behaviors, interactions, and events
                ProcessBotWandering(allBots); // PlayerBots now run automatically during wandering!
                ProcessLocationSpecificBehaviors(allBots);
                ProcessBotInteractions(allBots);
                ProcessDynamicEvents(allBots);

                if (Config.EnableLogging && Config.VerboseBehaviors)
                    Console.WriteLine("[{0}] [PlayerBotDirector] === Behavior Tick Complete ===", GetTimestamp());
            }
            catch (Exception ex)
            {
                Console.WriteLine("[{0}] [PlayerBotDirector] Exception in behavior tick: {1}", GetTimestamp(), ex);
            }
        }

        private List<PlayerBot> GetAllRegisteredBots()
        {
            List<PlayerBot> result = new List<PlayerBot>();
            
            lock (m_RegistryLock)
            {
                foreach (PlayerBotInfo info in m_RegisteredBots.Values)
                {
                    if (info.Bot != null && !info.Bot.Deleted && info.Bot.Alive)
                    {
                        result.Add(info.Bot);
                    }
                }
            }
            
            return result;
        }

        private void ProcessBotWandering(List<PlayerBot> allBots)
        {
            int wanderingBots = 0;
            
            foreach (PlayerBot bot in allBots)
            {
                // Skip bots that are in combat or controlled by players
                if (bot.Combatant != null || bot.Controled)
                    continue;

                PlayerBotInfo botInfo = GetBotInfo(bot);
                if (botInfo == null)
                    continue;

                // Ensure bot is in wander mode - the enhanced PlayerBotAI will handle running movement
                if (bot.AIObject != null && bot.AIObject.Action != ActionType.Wander)
                {
                    bot.AIObject.Action = ActionType.Wander;
                    wanderingBots++;
                }
            }

            if (Config.EnableLogging && Config.VerboseTravel && wanderingBots > 0)
                Console.WriteLine("[{0}] [PlayerBotDirector] Set {1} bots to enhanced running wander mode", 
                    GetTimestamp(), wanderingBots);
        }

        private void ProcessLocationSpecificBehaviors(List<PlayerBot> allBots)
        {
            int shoppingBots = 0;
            int socializing = 0;
            
            foreach (PlayerBot bot in allBots)
            {
                if (bot.Combatant != null || bot.Controled)
                    continue;

                PlayerBotInfo botInfo = GetBotInfo(bot);
                if (botInfo == null)
                    continue;

                // Find nearby POIs
                PointOfInterest nearestPOI = GetNearestPOI(bot);
                if (nearestPOI != null && bot.GetDistanceToSqrt(nearestPOI.Location) < 10)
                {
                    // Bot is near a POI, engage in location-specific behavior
                    if (ProcessLocationBehavior(bot, nearestPOI))
                    {
                        if (nearestPOI.Type == POIType.Shop)
                            shoppingBots++;
                        else if (nearestPOI.Type == POIType.Tavern || nearestPOI.Type == POIType.Bank)
                            socializing++;
                    }
                }
            }

            if (Config.EnableLogging && Config.VerboseBehaviors && (shoppingBots > 0 || socializing > 0))
                Console.WriteLine("[{0}] [PlayerBotDirector] Location behaviors: {1} shopping, {2} socializing", 
                    GetTimestamp(), shoppingBots, socializing);
        }

        private bool ProcessLocationBehavior(PlayerBot bot, PointOfInterest poi)
        {
            PlayerBotInfo botInfo = GetBotInfo(bot);
            if (botInfo == null)
                return false;

            // Check if bot just performed this behavior
            if (DateTime.Now - botInfo.LastStateChange < TimeSpan.FromMinutes(5))
                return false;

            switch (poi.Type)
            {
                case POIType.Shop:
                    if (Utility.Random(100) < Config.ShopVisitChance)
                    {
                        PerformShoppingBehavior(bot, poi);
                        return true;
                    }
                    break;

                case POIType.Tavern:
                    if (Utility.Random(100) < 20)
                    {
                        PerformTavernBehavior(bot, poi);
                        return true;
                    }
                    break;

                case POIType.Bank:
                    if (Utility.Random(100) < 15)
                    {
                        PerformBankBehavior(bot, poi);
                        return true;
                    }
                    break;

                case POIType.Dungeon:
                    if (bot.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.Adventurer && Utility.Random(100) < 25)
                    {
                        PerformDungeonBehavior(bot, poi);
                        return true;
                    }
                    break;
            }

            return false;
        }

        private void PerformShoppingBehavior(PlayerBot bot, PointOfInterest poi)
        {
            UpdateBotInfo(bot, BotBehaviorState.Shopping, poi.Location);
            
            List<string> shopMessages = new List<string>();
            
            if (bot.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.Crafter)
            {
                shopMessages.Add("I need some quality materials.");
                shopMessages.Add("These tools look well-crafted.");
                shopMessages.Add("The prices seem fair today.");
            }
            else
            {
                shopMessages.Add("I should stock up on supplies.");
                shopMessages.Add("This shop has good wares.");
                shopMessages.Add("Time to resupply.");
            }

            if (shopMessages.Count > 0)
            {
                string message = shopMessages[Utility.Random(shopMessages.Count)];
                bot.SayWithHue(message);
                bot.LastSpeechTime = DateTime.Now;
            }

            if (Config.EnableLogging && Config.VerboseBehaviors)
                Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' shopping at {2}", 
                    GetTimestamp(), bot.Name, poi.Name);
        }

        private void PerformTavernBehavior(PlayerBot bot, PointOfInterest poi)
        {
            UpdateBotInfo(bot, BotBehaviorState.Socializing, poi.Location);
            
            List<string> tavernMessages = new List<string>();
            tavernMessages.Add("A drink sounds good right about now.");
            tavernMessages.Add("I hear interesting tales in places like this.");
            tavernMessages.Add("The atmosphere here is quite welcoming.");
            
            if (bot.PlayerBotExperience == PlayerBotPersona.PlayerBotExperience.Grandmaster)
            {
                tavernMessages.Add("I've seen many adventures begin in taverns like this.");
                tavernMessages.Add("The stories these walls could tell...");
            }

            string message = tavernMessages[Utility.Random(tavernMessages.Count)];
            bot.SayWithHue(message);
            bot.LastSpeechTime = DateTime.Now;

            if (Config.EnableLogging && Config.VerboseBehaviors)
                Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' socializing at {2}", 
                    GetTimestamp(), bot.Name, poi.Name);
        }

        private void PerformBankBehavior(PlayerBot bot, PointOfInterest poi)
        {
            UpdateBotInfo(bot, BotBehaviorState.Banking, poi.Location);
            
            List<string> bankMessages = new List<string>();
            bankMessages.Add("I should secure my valuables.");
            bankMessages.Add("The bank vaults are quite secure here.");
            
            if (bot.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.Crafter)
            {
                bankMessages.Add("Time to deposit my earnings.");
                bankMessages.Add("I need to organize my materials.");
            }

            string message = bankMessages[Utility.Random(bankMessages.Count)];
            bot.SayWithHue(message);
            bot.LastSpeechTime = DateTime.Now;

            if (Config.EnableLogging && Config.VerboseBehaviors)
                Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' banking at {2}", 
                    GetTimestamp(), bot.Name, poi.Name);
        }

        private void PerformDungeonBehavior(PlayerBot bot, PointOfInterest poi)
        {
            UpdateBotInfo(bot, BotBehaviorState.Exploring, poi.Location);
            
            List<string> dungeonMessages = new List<string>();
            dungeonMessages.Add("Adventure awaits in the depths below.");
            dungeonMessages.Add("I can sense danger ahead.");
            dungeonMessages.Add("Fortune favors the bold.");
            
            if (bot.PlayerBotExperience == PlayerBotPersona.PlayerBotExperience.Newbie)
            {
                dungeonMessages.Add("This place looks quite dangerous...");
                dungeonMessages.Add("Perhaps I should find some companions.");
            }

            string message = dungeonMessages[Utility.Random(dungeonMessages.Count)];
            bot.SayWithHue(message);
            bot.LastSpeechTime = DateTime.Now;

            if (Config.EnableLogging && Config.VerboseBehaviors)
                Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' exploring at {2}", 
                    GetTimestamp(), bot.Name, poi.Name);
        }

        private void ProcessDynamicEvents(List<PlayerBot> allBots)
        {
            // Random chance for dynamic events
            if (Utility.Random(100) < Config.DynamicEventChance)
            {
                if (allBots.Count >= 4) // Need minimum bots for events
                {
                    int eventType = Utility.Random(3);
                    switch (eventType)
                    {
                        case 0:
                            CreateBotConflict(allBots);
                            break;
                        case 1:
                            CreateTradingEvent(allBots);
                            break;
                        case 2:
                            CreateGroupActivity(allBots);
                            break;
                    }
                }
            }
        }

        private void CreateBotConflict(List<PlayerBot> allBots)
        {
            // Find PKs and potential targets
            List<PlayerBot> pks = new List<PlayerBot>();
            List<PlayerBot> targets = new List<PlayerBot>();
            
            foreach (PlayerBot bot in allBots)
            {
                if (bot.Combatant != null || bot.Controled)
                    continue;
                    
                if (bot.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.PlayerKiller)
                    pks.Add(bot);
                else
                    targets.Add(bot);
            }

            if (pks.Count > 0 && targets.Count > 0)
            {
                PlayerBot pk = pks[Utility.Random(pks.Count)];
                PlayerBot target = targets[Utility.Random(targets.Count)];
                
                // Only if they're reasonably close
                if (pk.GetDistanceToSqrt(target) < 50)
                {
                    pk.SayWithHue("Your gold or your life!");
                    pk.LastSpeechTime = DateTime.Now;
                    
                    Timer.DelayCall(TimeSpan.FromSeconds(2), new TimerCallback(delegate()
                    {
                        if (target != null && !target.Deleted)
                        {
                            target.SayWithHue("Help! Murderer!");
                            target.LastSpeechTime = DateTime.Now;
                        }
                    }));

                    if (Config.EnableLogging && Config.VerboseEvents)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Dynamic event: Bot conflict between '{1}' and '{2}'", 
                            GetTimestamp(), pk.Name, target.Name);
                }
            }
        }

        private void CreateTradingEvent(List<PlayerBot> allBots)
        {
            List<PlayerBot> crafters = new List<PlayerBot>();
            List<PlayerBot> others = new List<PlayerBot>();
            
            foreach (PlayerBot bot in allBots)
            {
                if (bot.Combatant != null || bot.Controled)
                    continue;
                    
                if (bot.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.Crafter)
                    crafters.Add(bot);
                else
                    others.Add(bot);
            }

            if (crafters.Count > 0 && others.Count > 0)
            {
                PlayerBot crafter = crafters[Utility.Random(crafters.Count)];
                PlayerBot customer = others[Utility.Random(others.Count)];
                
                if (crafter.GetDistanceToSqrt(customer) < 30)
                {
                    crafter.SayWithHue("Quality goods for sale!");
                    crafter.LastSpeechTime = DateTime.Now;
                    
                    Timer.DelayCall(TimeSpan.FromSeconds(3), new TimerCallback(delegate()
                    {
                        if (customer != null && !customer.Deleted)
                        {
                            customer.SayWithHue("What do you have available?");
                            customer.LastSpeechTime = DateTime.Now;
                        }
                    }));

                    if (Config.EnableLogging && Config.VerboseEvents)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Dynamic event: Trading between '{1}' and '{2}'", 
                            GetTimestamp(), crafter.Name, customer.Name);
                }
            }
        }

        private void CreateGroupActivity(List<PlayerBot> allBots)
        {
            // Select 3-6 bots for a group activity
            int groupSize = Utility.RandomMinMax(3, Math.Min(6, allBots.Count));
            List<PlayerBot> selectedBots = new List<PlayerBot>();
            List<PlayerBot> availableBots = new List<PlayerBot>(allBots);

            for (int i = 0; i < groupSize && availableBots.Count > 0; i++)
            {
                PlayerBot bot = availableBots[Utility.Random(availableBots.Count)];
                selectedBots.Add(bot);
                availableBots.Remove(bot);
            }

            if (selectedBots.Count < 3)
                return; // Need minimum group size

            // Find a central meeting point
            Point3D centerPoint = selectedBots[0].Location;
            foreach (PlayerBot bot in selectedBots)
            {
                UpdateBotInfo(bot, BotBehaviorState.Socializing, centerPoint);
                
                List<string> groupMessages = new List<string>();
                groupMessages.Add("Shall we travel together for safety?");
                groupMessages.Add("It's good to have companions on the road.");
                groupMessages.Add("There's strength in numbers.");
                
                string message = groupMessages[Utility.Random(groupMessages.Count)];
                bot.SayWithHue(message);
                bot.LastSpeechTime = DateTime.Now;
            }

            if (Config.EnableLogging && Config.VerboseBehaviors)
                Console.WriteLine("[{0}] [PlayerBotDirector] Created group activity with {1} bots at {2}", 
                    GetTimestamp(), selectedBots.Count, centerPoint);
        }
        #endregion

        #region Helper Methods
        private RegionProfile GetBotCurrentRegion(PlayerBot bot)
        {
            foreach (RegionProfile region in m_Regions)
            {
                if (region.Map == bot.Map && region.Area.Contains(bot.Location))
                    return region;
            }
            return null;
        }

        private string GetBotCurrentRegionName(PlayerBot bot)
        {
            RegionProfile region = GetBotCurrentRegion(bot);
            return region != null ? region.Name : "Unknown";
        }

        private PointOfInterest GetNearestPOI(PlayerBot bot)
        {
            PointOfInterest nearest = null;
            double nearestDistance = double.MaxValue;

            foreach (PointOfInterest poi in m_PointsOfInterest)
            {
                if (poi.Map == bot.Map)
                {
                    double distance = bot.GetDistanceToSqrt(poi.Location);
                    if (distance < nearestDistance)
                    {
                        nearest = poi;
                        nearestDistance = distance;
                    }
                }
            }

            return nearest;
        }

        private void ProcessBotInteractions(List<PlayerBot> allBots)
        {
            int interactions = 0;
            
            // Group bots by proximity for potential interactions
            Dictionary<Point3D, List<PlayerBot>> proximityGroups = new Dictionary<Point3D, List<PlayerBot>>();
            
            foreach (PlayerBot bot in allBots)
            {
                if (bot.Combatant != null || bot.Controled)
                    continue;

                // Group by approximate location (10x10 tile groups)
                Point3D groupKey = new Point3D(
                    (bot.Location.X / 10) * 10,
                    (bot.Location.Y / 10) * 10,
                    0
                );
                
                if (!proximityGroups.ContainsKey(groupKey))
                    proximityGroups[groupKey] = new List<PlayerBot>();
                
                proximityGroups[groupKey].Add(bot);
            }

            // Process interactions within each proximity group
            foreach (List<PlayerBot> group in proximityGroups.Values)
            {
                if (group.Count >= 2)
                {
                    interactions += ProcessGroupInteractions(group);
                }
            }

            if (Config.EnableLogging && Config.VerboseInteractions && interactions > 0)
                Console.WriteLine("[{0}] [PlayerBotDirector] Processed {1} bot interactions", GetTimestamp(), interactions);
        }

        private int ProcessGroupInteractions(List<PlayerBot> group)
        {
            int interactions = 0;
            
            foreach (PlayerBot bot in group)
            {
                // Random chance for this bot to initiate interaction
                if (Utility.Random(100) < Config.InteractionChancePercent)
                {
                    // Find nearby bots within interaction range
                    List<PlayerBot> nearbyBots = new List<PlayerBot>();
                    
                    foreach (PlayerBot otherBot in group)
                    {
                        if (otherBot != bot && bot.InRange(otherBot, 5))
                        {
                            nearbyBots.Add(otherBot);
                        }
                    }

                    if (nearbyBots.Count > 0)
                    {
                        PlayerBot target = nearbyBots[Utility.Random(nearbyBots.Count)];
                        
                        if (InitiateBotInteraction(bot, target))
                            interactions++;
                    }
                }
            }
            
            return interactions;
        }

        private bool InitiateBotInteraction(PlayerBot initiator, PlayerBot target)
        {
            // Check if both bots can interact
            if (initiator.Deleted || target.Deleted || 
                initiator.Combatant != null || target.Combatant != null ||
                initiator.Controled || target.Controled)
                return false;

            // Check interaction cooldown (prevent spam)
            if (DateTime.Now - initiator.LastSpeechTime < TimeSpan.FromSeconds(30))
                return false;

            // Generate interaction based on bot personas
            string message = GenerateInteractionMessage(initiator, target);
            
            if (!string.IsNullOrEmpty(message))
            {
                initiator.SayWithHue(message);
                initiator.LastSpeechTime = DateTime.Now;
                
                // Face each other
                initiator.Direction = initiator.GetDirectionTo(target);
                target.Direction = target.GetDirectionTo(initiator);
                
                if (Config.EnableLogging && Config.VerboseInteractions)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Bot interaction: '{1}' -> '{2}': \"{3}\"", 
                        GetTimestamp(), initiator.Name, target.Name, message);
                
                // Schedule a response from the target
                Timer.DelayCall(TimeSpan.FromSeconds(Utility.RandomMinMax(2, 5)), 
                    new TimerCallback(delegate() { GenerateInteractionResponse(target, initiator); }));
                
                return true;
            }
            
            return false;
        }

        private string GenerateInteractionMessage(PlayerBot initiator, PlayerBot target)
        {
            // Generate messages based on bot personas and experience levels
            List<string> messages = new List<string>();
            
            // Friendly greetings
            messages.Add("Hail, " + target.Name + "!");
            messages.Add("Well met, friend.");
            messages.Add("Good day to you.");
            messages.Add("Greetings, traveler.");
            
            // Experience-based interactions
            if (initiator.PlayerBotExperience == PlayerBotPersona.PlayerBotExperience.Newbie)
            {
                messages.Add("I'm still learning the ways of this land.");
                messages.Add("Have you seen any good hunting spots?");
                messages.Add("This place is quite overwhelming.");
            }
            else if (initiator.PlayerBotExperience == PlayerBotPersona.PlayerBotExperience.Grandmaster)
            {
                messages.Add("The roads have been dangerous lately.");
                messages.Add("I've seen better days in these lands.");
                messages.Add("The young ones need guidance these days.");
            }
            
            // Profile-based interactions
            if (initiator.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.Crafter)
            {
                messages.Add("I've been working on my craft all morning.");
                messages.Add("The market prices have been fair lately.");
                messages.Add("Have you any materials to trade?");
            }
            else if (initiator.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.Adventurer)
            {
                messages.Add("I hear there's treasure to be found in the dungeons.");
                messages.Add("The monsters grow stronger each day.");
                messages.Add("Adventure calls to those brave enough.");
            }
            else if (initiator.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.PlayerKiller)
            {
                messages.Add("These lands belong to the strong.");
                messages.Add("Watch your back, stranger.");
                messages.Add("Not all who wander are friendly.");
            }
            
            if (messages.Count > 0)
                return messages[Utility.Random(messages.Count)];
            
            return "Hello there.";
        }

        private void GenerateInteractionResponse(PlayerBot responder, PlayerBot initiator)
        {
            if (responder == null || responder.Deleted || initiator == null || initiator.Deleted)
                return;
                
            if (!responder.InRange(initiator, 8))
                return;

            List<string> responses = new List<string>();
            
            // Generic responses
            responses.Add("Indeed, " + initiator.Name + ".");
            responses.Add("Aye, that's true.");
            responses.Add("I agree completely.");
            responses.Add("Well said.");
            responses.Add("Farewell for now.");
            
            // Profile-specific responses
            if (responder.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.Crafter)
            {
                responses.Add("My workshop keeps me busy.");
                responses.Add("Quality work takes time.");
            }
            else if (responder.PlayerBotProfile == PlayerBotPersona.PlayerBotProfile.Adventurer)
            {
                responses.Add("The path ahead is uncertain.");
                responses.Add("May fortune favor your journey.");
            }
            
            string response = responses[Utility.Random(responses.Count)];
            responder.SayWithHue(response);
            responder.LastSpeechTime = DateTime.Now;
            
            if (Config.EnableLogging && Config.VerboseInteractions)
                Console.WriteLine("[{0}] [PlayerBotDirector] Bot response: '{1}' -> '{2}': \"{3}\"", 
                    GetTimestamp(), responder.Name, initiator.Name, response);
        }
        #endregion

        #region Scene Management (Phase 2)
        /// <summary>
        /// Scene management tick - creates and manages dynamic scenes
        /// </summary>
        private void OnSceneTick()
        {
            try
            {
                // Update existing scenes
                UpdateActiveScenes();
                
                // Clean up completed scenes
                CleanupCompletedScenes();
                
                // Consider creating new scenes
                ConsiderNewScenes();
                
                if (Config.EnableLogging && Config.VerboseEvents && m_ActiveScenes.Count > 0)
                {
                    Console.WriteLine("[{0}] [PlayerBotDirector] Scene tick: {1} active scenes", 
                        GetTimestamp(), m_ActiveScenes.Count);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[{0}] [PlayerBotDirector] Error in scene tick: {1}", GetTimestamp(), ex.Message);
            }
        }

        /// <summary>
        /// Update all active scenes
        /// </summary>
        private void UpdateActiveScenes()
        {
            foreach (PlayerBotScene scene in m_ActiveScenes)
            {
                if (scene != null && !scene.IsComplete)
                {
                    scene.Update();
                }
            }
        }

        /// <summary>
        /// Remove completed scenes from the active list
        /// </summary>
        private void CleanupCompletedScenes()
        {
            for (int i = m_ActiveScenes.Count - 1; i >= 0; i--)
            {
                PlayerBotScene scene = m_ActiveScenes[i];
                if (scene == null || scene.IsComplete)
                {
                    m_ActiveScenes.RemoveAt(i);
                    
                    if (Config.EnableLogging && Config.VerboseEvents && scene != null)
                    {
                        Console.WriteLine("[{0}] [PlayerBotDirector] Scene '{1}' completed and removed", 
                            GetTimestamp(), scene.Name);
                    }
                }
            }
        }

        /// <summary>
        /// Consider creating new scenes based on current conditions
        /// </summary>
        private void ConsiderNewScenes()
        {
            // Don't create too many scenes at once
            if (m_ActiveScenes.Count >= Config.MaxActiveScenes)
                return;

            // Random chance to create a scene
            if (Utility.Random(100) >= Config.DynamicEventChance)
                return;

            // Try to create a scene in a random region
            if (m_Regions.Count == 0)
                return;

            RegionProfile region = m_Regions[Utility.Random(m_Regions.Count)];
            if (region == null)
                return;

            // Get nearby players to determine if scene should require them
            List<Mobile> nearbyPlayers = GetPlayersInRegion(region);
            
            // Try different scene types
            PlayerBotScene newScene = TryCreateScene(region, nearbyPlayers);
            if (newScene != null)
            {
                // Use AddScene to properly initialize and start the scene
                AddScene(newScene);
                
                if (Config.EnableLogging && Config.VerboseEvents)
                {
                    Console.WriteLine("[{0}] [PlayerBotDirector] Created new scene: '{1}' in {2}", 
                        GetTimestamp(), newScene.Name, region.Name);
                }
            }
        }

        /// <summary>
        /// Try to create a scene of a random type
        /// </summary>
        private PlayerBotScene TryCreateScene(RegionProfile region, List<Mobile> nearbyPlayers)
        {
            // Determine scene type based on region and conditions
            PlayerBotScene.SceneType[] possibleScenes = GetPossibleSceneTypes(region, nearbyPlayers);
            
            if (possibleScenes.Length == 0)
                return null;

            PlayerBotScene.SceneType sceneType = possibleScenes[Utility.Random(possibleScenes.Length)];
            
            // Create scene based on type
            switch (sceneType)
            {
                case PlayerBotScene.SceneType.War:
                    return TryCreateWarScene(region, nearbyPlayers);
                    
                // Add other scene types here as they're implemented
                
                default:
                    return null;
            }
        }

        /// <summary>
        /// Get possible scene types for a region
        /// </summary>
        private PlayerBotScene.SceneType[] GetPossibleSceneTypes(RegionProfile region, List<Mobile> nearbyPlayers)
        {
            List<PlayerBotScene.SceneType> possible = new List<PlayerBotScene.SceneType>();
            
            // War scenes - only in wilderness/dangerous areas (no player requirement)
            if (region.SafetyLevel != PlayerBotConfigurationManager.SafetyLevel.Safe)
            {
                possible.Add(PlayerBotScene.SceneType.War);
            }
            
            return possible.ToArray();
        }

        /// <summary>
        /// Try to create a war scene
        /// </summary>
        private PlayerBotScene TryCreateWarScene(RegionProfile region, List<Mobile> nearbyPlayers)
        {
            // Find a good location for the battle
            Point3D center = FindSceneLocation(region);
            if (center == Point3D.Zero)
                return null;

            try
            {
                Server.Engines.Scenes.WarScene warScene = new Server.Engines.Scenes.WarScene(center, region.Map);
                
                if (warScene.CanTrigger(region, nearbyPlayers))
                {
                    return warScene;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[PlayerBotDirector] Error creating war scene: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Find a suitable location for a scene within a region
        /// </summary>
        private Point3D FindSceneLocation(RegionProfile region)
        {
            for (int i = 0; i < 10; i++) // Try up to 10 times
            {
                int x = Utility.RandomMinMax(region.Area.X, region.Area.X + region.Area.Width);
                int y = Utility.RandomMinMax(region.Area.Y, region.Area.Y + region.Area.Height);
                int z = region.Map.GetAverageZ(x, y);
                Point3D p = new Point3D(x, y, z);

                if (region.Map.CanSpawnMobile(p.X, p.Y, p.Z))
                    return p;
            }

            return Point3D.Zero;
        }

        /// <summary>
        /// Check if two points are on the same landmass (can be reached without crossing water)
        /// </summary>
        private bool AreOnSameLandmass(Point3D start, Point3D end, Map map)
        {
            // For now, use predefined landmass boundaries for Trammel/Felucca
            if (map == Map.Trammel || map == Map.Felucca)
            {
                return GetLandmassId(start, map) == GetLandmassId(end, map);
            }
            
            // For other maps, assume same landmass (Ilshenar, Malas are single landmasses)
            return true;
        }

        /// <summary>
        /// Get landmass ID for a point on Trammel/Felucca
        /// </summary>
        private int GetLandmassId(Point3D point, Map map)
        {
            if (map != Map.Trammel && map != Map.Felucca)
                return 1; // Single landmass
                
            // Main continent (Britain, Minoc, Vesper, Trinsic, etc.)
            if (point.X >= 0 && point.X <= 5120 && point.Y >= 0 && point.Y <= 4096)
            {
                // Check for major islands within the main map
                
                // Nujel'm island
                if (point.X >= 3600 && point.X <= 4000 && point.Y >= 1000 && point.Y <= 1400)
                    return 2;
                    
                // Moonglow island  
                if (point.X >= 4400 && point.X <= 4700 && point.Y >= 900 && point.Y <= 1200)
                    return 3;
                    
                // Magincia island
                if (point.X >= 3650 && point.X <= 3950 && point.Y >= 2000 && point.Y <= 2300)
                    return 4;
                    
                // Fire Island (near Buc's Den)
                if (point.X >= 2200 && point.X <= 2600 && point.Y >= 1000 && point.Y <= 1400)
                    return 5;
                    
                // Ice Island (Dagger Isle area)
                if (point.X >= 3900 && point.X <= 4200 && point.Y >= 3700 && point.Y <= 4000)
                    return 6;
                    
                // Skara Brae island
                if (point.X >= 550 && point.X <= 750 && point.Y >= 2050 && point.Y <= 2250)
                    return 7;
                    
                // Occllo island
                if (point.X >= 3600 && point.X <= 3900 && point.Y >= 2600 && point.Y <= 2900)
                    return 8;
                    
                // Main continent (everything else)
                return 1;
            }
            
            // Lost Lands (T2A area)
            if (point.X >= 5120 && point.X <= 6144 && point.Y >= 2304 && point.Y <= 4096)
                return 9; // Lost Lands
                
            return 1; // Default to main continent
        }

        /// <summary>
        /// Get all players in a region
        /// </summary>
        private List<Mobile> GetPlayersInRegion(RegionProfile region)
        {
            List<Mobile> players = new List<Mobile>();
            
            foreach (Server.Network.NetState ns in Server.Network.NetState.Instances)
            {
                if (ns.Mobile != null && ns.Mobile.Player && ns.Mobile.Map == region.Map)
                {
                    if (region.Area.Contains(ns.Mobile.Location))
                    {
                        players.Add(ns.Mobile);
                    }
                }
            }
            
            return players;
        }

        /// <summary>
        /// Get count of active scenes by type
        /// </summary>
        public int GetActiveSceneCount(PlayerBotScene.SceneType? type = null)
        {
            if (type == null)
                return m_ActiveScenes.Count;
                
            int count = 0;
            foreach (PlayerBotScene scene in m_ActiveScenes)
            {
                if (scene != null && scene.Type == type.Value)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Get list of active scenes (for admin commands)
        /// </summary>
        public List<PlayerBotScene> GetActiveScenes()
        {
            return new List<PlayerBotScene>(m_ActiveScenes);
        }

        /// <summary>
        /// Get a specific scene by ID
        /// </summary>
        public PlayerBotScene GetSceneById(int sceneId)
        {
            lock (m_ActiveScenes)
            {
                foreach (PlayerBotScene scene in m_ActiveScenes)
                {
                    if (scene.SceneId == sceneId)
                        return scene;
                }
                return null;
            }
        }

        /// <summary>
        /// Add a scene to the active scenes list and start it
        /// </summary>
        public void AddScene(PlayerBotScene scene)
        {
            if (scene == null)
                return;

            lock (m_ActiveScenes)
            {
                m_ActiveScenes.Add(scene);
            }

            try
            {
                scene.Start();
                if (PlayerBotConfigurationManager.BehaviorSettings.EnableLogging)
                {
                    Console.WriteLine("[{0}] [PlayerBotDirector] Scene {1} ({2}) started at {3}", 
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), scene.SceneId, scene.GetType().Name.Replace("Scene", ""), scene.CenterLocation);
                }
            }
            catch (Exception ex)
            {
                if (PlayerBotConfigurationManager.BehaviorSettings.EnableLogging)
                {
                    Console.WriteLine("[{0}] [PlayerBotDirector] Error starting scene {1}: {2}", 
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), scene.SceneId, ex.Message);
                }
                lock (m_ActiveScenes)
                {
                    m_ActiveScenes.Remove(scene);
                }
            }
        }

        /// <summary>
        /// Auto-scene creation enabled/disabled
        /// </summary>
        public bool AutoSceneCreation
        {
            get { return Config.AutoSceneCreation; }
            set { Config.AutoSceneCreation = value; }
        }

        #endregion
    }
} 