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
            Traveling,
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
        private class RegionProfile
        {
            private string _name;
            private Map _map;
            private Rectangle2D _area;
            private int _min;
            private int _max;

            public string Name { get { return _name; } }
            public Map Map { get { return _map; } }
            public Rectangle2D Area { get { return _area; } }
            public int Min { get { return _min; } }
            public int Max { get { return _max; } }

            public RegionProfile(string name, Map map, Rectangle2D area, int min, int max)
            {
                _name = name;
                _map = map;
                _area = area;
                _min = min;
                _max = max;
            }
        }
        #endregion

        #region Singleton
        private static readonly PlayerBotDirector m_Instance = new PlayerBotDirector();
        public static PlayerBotDirector Instance { get { return m_Instance; } }
        #endregion

        #region Configuration Constants
        private const int GLOBAL_CAP = 200;            // Absolute maximum bots allowed shard-wide
        private const int POPULATION_TICK_SECONDS = 30; // How often to evaluate population
        private const int SPAWN_LOCATION_ATTEMPTS = 20; // Max attempts to find spawn location
        private const int STARTUP_DELAY_SECONDS = 10;   // Delay before first population tick
        
        // Phase 2: Travel & Behavior Constants
        private const int BEHAVIOR_TICK_SECONDS = 45;   // How often to evaluate bot behaviors
        private const int TRAVEL_CHANCE_PERCENT = 15;   // Chance per behavior tick that a bot will travel
        private const int INTERACTION_CHANCE_PERCENT = 25; // Chance per behavior tick that bots will interact
        private const int MIN_TRAVEL_DISTANCE = 10;     // Minimum tiles to travel
        private const int MAX_TRAVEL_DISTANCE = 50;     // Maximum tiles to travel
        private const int INTER_REGION_TRAVEL_CHANCE = 5; // Chance for long-distance travel between regions
        private const int SHOP_VISIT_CHANCE = 10;       // Chance to visit shops in cities
        private const int DYNAMIC_EVENT_CHANCE = 3;     // Chance for dynamic events per behavior tick
        
        // Debug toggle - set to false to disable verbose logging
        private static bool DEBUG_ENABLED = true;
        #endregion

        #region Private Fields
        private Timer m_Timer;
        private Timer m_BehaviorTimer; // Phase 2: Behavior management timer
        private List<RegionProfile> m_Regions;
        private List<PointOfInterest> m_PointsOfInterest; // Phase 2: Notable locations
        
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
            m_Regions = new List<RegionProfile>();
            m_RegisteredBots = new Dictionary<Serial, PlayerBotInfo>();
            m_PointsOfInterest = new List<PointOfInterest>();
            
            SeedDefaultRegions();
            SeedPointsOfInterest();

            // Phase 1: Population management timer
            m_Timer = Timer.DelayCall(TimeSpan.FromSeconds(STARTUP_DELAY_SECONDS), TimeSpan.FromSeconds(POPULATION_TICK_SECONDS), new TimerCallback(OnPopulationTick));
            
            // Phase 2: Behavior management timer (starts a bit later to let population stabilize)
            m_BehaviorTimer = Timer.DelayCall(TimeSpan.FromSeconds(STARTUP_DELAY_SECONDS + 15), TimeSpan.FromSeconds(BEHAVIOR_TICK_SECONDS), new TimerCallback(OnBehaviorTick));
            
            if (DEBUG_ENABLED)
            {
                Console.WriteLine("[{0}] [PlayerBotDirector] Constructor completed. Population timer starts in {1}s, behavior timer in {2}s", 
                    GetTimestamp(), STARTUP_DELAY_SECONDS, STARTUP_DELAY_SECONDS + 15);
            }
        }

        [CallPriority(int.MaxValue)]
        public static void Initialize()
        {
            // Accessing Instance forces the singleton to construct and start the timer.
            PlayerBotDirector unused = Instance;
            Console.WriteLine("[{0}] [PlayerBotDirector] Initialized (phase 1 + 2) - Debug mode: {1}", GetTimestamp(), DEBUG_ENABLED ? "ON" : "OFF");
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
                PlayerBotInfo info = new PlayerBotInfo(bot);
                m_RegisteredBots[bot.Serial] = info;
                
                if (DEBUG_ENABLED)
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
                    
                    if (DEBUG_ENABLED)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Unregistered bot '{1}' (Serial: {2}) - Lived for {3} - Total registered: {4}", 
                            GetTimestamp(), bot.Name, bot.Serial, FormatTimeSpan(lifespan), m_RegisteredBots.Count);
                }
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
        private void UpdateBotInfo(PlayerBot bot, BotBehaviorState newState, Point3D destination)
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
                    
                    if (DEBUG_ENABLED)
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
        /// Defines the regions of Britannia (Felucca/Trammel) we want to keep populated.
        /// For phase 1 we hard-code a handful. This will be moved to an external cfg later.
        /// </summary>
        private void SeedDefaultRegions()
        {
            if (DEBUG_ENABLED)
                Console.WriteLine("[{0}] [PlayerBotDirector] Seeding default regions...", GetTimestamp());

            // Britain city core (bank + surrounding streets)
            Server.Rectangle2D britainArea = new Server.Rectangle2D(1416, 1675, 60, 60);
            RegionProfile britainProfile = new RegionProfile("Britain", Map.Felucca, britainArea, 6, 15);
            m_Regions.Add(britainProfile);

            // Yew forest north of town gate
            Server.Rectangle2D yewArea = new Server.Rectangle2D(632, 752, 200, 200);
            RegionProfile yewProfile = new RegionProfile("Yew Woods", Map.Felucca, yewArea, 4, 12);
            m_Regions.Add(yewProfile);

            // Despise level 1 (rough rectangle around entrance area)
            Server.Rectangle2D despiseArea = new Server.Rectangle2D(553, 1459, 100, 100);
            RegionProfile despiseProfile = new RegionProfile("Despise L1", Map.Felucca, despiseArea, 8, 18);
            m_Regions.Add(despiseProfile);

            if (DEBUG_ENABLED)
            {
                Console.WriteLine("[{0}] [PlayerBotDirector] Seeded {1} regions:", GetTimestamp(), m_Regions.Count);
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
        /// Seed points of interest for bot behavior and travel destinations
        /// </summary>
        private void SeedPointsOfInterest()
        {
            if (DEBUG_ENABLED)
                Console.WriteLine("[{0}] [PlayerBotDirector] Seeding points of interest...", GetTimestamp());

            // Britain POIs
            m_PointsOfInterest.Add(new PointOfInterest("Britain Bank", Map.Felucca, new Point3D(1434, 1699, 0), POIType.Bank));
            m_PointsOfInterest.Add(new PointOfInterest("Britain Blacksmith", Map.Felucca, new Point3D(1420, 1634, 0), POIType.Shop));
            m_PointsOfInterest.Add(new PointOfInterest("Britain Tavern", Map.Felucca, new Point3D(1462, 1607, 0), POIType.Tavern));
            m_PointsOfInterest.Add(new PointOfInterest("Britain Healer", Map.Felucca, new Point3D(1482, 1612, 0), POIType.Healer));
            m_PointsOfInterest.Add(new PointOfInterest("Britain Mage Shop", Map.Felucca, new Point3D(1492, 1629, 0), POIType.Shop));

            // Yew POIs
            m_PointsOfInterest.Add(new PointOfInterest("Yew Bank", Map.Felucca, new Point3D(771, 752, 0), POIType.Bank));
            m_PointsOfInterest.Add(new PointOfInterest("Yew Winery", Map.Felucca, new Point3D(692, 858, 0), POIType.Tavern));
            m_PointsOfInterest.Add(new PointOfInterest("Yew Abbey", Map.Felucca, new Point3D(654, 858, 0), POIType.Landmark));

            // Despise POIs
            m_PointsOfInterest.Add(new PointOfInterest("Despise Entrance", Map.Felucca, new Point3D(514, 1560, 0), POIType.Dungeon));
            m_PointsOfInterest.Add(new PointOfInterest("Despise Level 1", Map.Felucca, new Point3D(553, 1459, 0), POIType.Dungeon));

            // Travel waypoints between regions
            m_PointsOfInterest.Add(new PointOfInterest("Britain-Yew Road", Map.Felucca, new Point3D(1200, 1000, 0), POIType.Waypoint));
            m_PointsOfInterest.Add(new PointOfInterest("Yew-Despise Path", Map.Felucca, new Point3D(600, 1200, 0), POIType.Waypoint));

            if (DEBUG_ENABLED)
            {
                Console.WriteLine("[{0}] [PlayerBotDirector] Seeded {1} points of interest:", GetTimestamp(), m_PointsOfInterest.Count);
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
                if (DEBUG_ENABLED)
                    Console.WriteLine("[{0}] [PlayerBotDirector] === Population Tick Started ===", GetTimestamp());

                // Clean up any stale registrations first
                CleanupStaleRegistrations();

                int totalRegistered = GetRegisteredBotCount();
                if (DEBUG_ENABLED)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Found {1} registered PlayerBots (global cap: {2})", GetTimestamp(), totalRegistered, GLOBAL_CAP);

                // Global cap guard
                if (totalRegistered >= GLOBAL_CAP)
                {
                    if (DEBUG_ENABLED)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Global cap reached! No new bots will be spawned.", GetTimestamp());
                    return; // nothing to do, shard is at capacity
                }

                int totalSpawned = 0;
                foreach (RegionProfile profile in m_Regions)
                {
                    int spawned = EnsureRegionPopulation(profile);
                    totalSpawned += spawned;
                }

                if (DEBUG_ENABLED)
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

            if (DEBUG_ENABLED)
                Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': {2}/{3} bots present (target: {4}-{5})", 
                    GetTimestamp(), profile.Name, count, profile.Max, profile.Min, profile.Max);

            if (count >= profile.Min)
            {
                if (DEBUG_ENABLED && count < profile.Max)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': Population satisfied (could spawn {2} more)", 
                        GetTimestamp(), profile.Name, profile.Max - count);
                else if (DEBUG_ENABLED)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': At maximum capacity", GetTimestamp(), profile.Name);
                return 0; // already satisfied
            }

            int toSpawn = Math.Min(profile.Min - count, profile.Max - count);

            // Respect global cap
            int totalRegistered = GetRegisteredBotCount();
            int globalRemaining = GLOBAL_CAP - totalRegistered;
            if (toSpawn > globalRemaining)
            {
                if (DEBUG_ENABLED)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': Wanted to spawn {2} bots, but global cap limits to {3}", 
                        GetTimestamp(), profile.Name, toSpawn, globalRemaining);
                toSpawn = globalRemaining;
            }

            if (DEBUG_ENABLED)
                Console.WriteLine("[{0}] [PlayerBotDirector] Region '{1}': Attempting to spawn {2} bots...", GetTimestamp(), profile.Name, toSpawn);

            int actuallySpawned = 0;
            for (int i = 0; i < toSpawn; i++)
            {
                if (SpawnBotInRegion(profile))
                    actuallySpawned++;
            }

            if (DEBUG_ENABLED)
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
                if (DEBUG_ENABLED)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Failed to find spawn location in region '{1}' after {2} attempts", GetTimestamp(), profile.Name, SPAWN_LOCATION_ATTEMPTS);
                return false; // failed to find safe spot
            }

            PlayerBot bot = new PlayerBot(); // Uses default random persona
            bot.MoveToWorld(loc, profile.Map);

            // Register the bot with the director
            RegisterBot(bot);

            if (DEBUG_ENABLED)
            {
                string persona = "Unknown";
                if (bot.PlayerBotProfile != null)
                    persona = bot.PlayerBotProfile.ToString();
                
                string experience = "Unknown";
                if (bot.PlayerBotExperience != null)
                    experience = bot.PlayerBotExperience.ToString();

                Console.WriteLine("[{0}] [PlayerBotDirector] Spawned bot '{1}' ({2} {3}) at {4} in region '{5}'", 
                    GetTimestamp(), bot.Name, experience, persona, loc, profile.Name);
            }

            return true;
        }

        private Point3D FindSpawnLocation(RegionProfile profile)
        {
            Map map = profile.Map;
            for (int i = 0; i < SPAWN_LOCATION_ATTEMPTS; i++) // try up to configured attempts
            {
                int x = Utility.RandomMinMax(profile.Area.Start.X, profile.Area.End.X);
                int y = Utility.RandomMinMax(profile.Area.Start.Y, profile.Area.End.Y);
                int z = map.GetAverageZ(x, y);
                Point3D p = new Point3D(x, y, z);

                if (CanSpawnAt(map, p))
                {
                    if (DEBUG_ENABLED)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Found spawn location {1} in region '{2}' (attempt {3}/{4})", 
                            GetTimestamp(), p, profile.Name, i + 1, SPAWN_LOCATION_ATTEMPTS);
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
                if (DEBUG_ENABLED)
                    Console.WriteLine("[{0}] [PlayerBotDirector] === Behavior Tick Started ===", GetTimestamp());

                List<PlayerBot> allBots = GetAllRegisteredBots();
                
                if (allBots.Count == 0)
                {
                    if (DEBUG_ENABLED)
                        Console.WriteLine("[{0}] [PlayerBotDirector] No bots registered for behavior management", GetTimestamp());
                    return;
                }

                if (DEBUG_ENABLED)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Managing behaviors for {1} bots", GetTimestamp(), allBots.Count);

                // Phase 2 sophisticated behaviors
                ProcessBotTravel(allBots);
                ProcessLocationSpecificBehaviors(allBots);
                ProcessBotInteractions(allBots);
                ProcessDynamicEvents(allBots);

                if (DEBUG_ENABLED)
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

        private void ProcessBotTravel(List<PlayerBot> allBots)
        {
            int localTravelers = 0;
            int interRegionTravelers = 0;
            
            foreach (PlayerBot bot in allBots)
            {
                // Skip bots that are in combat or controlled by players
                if (bot.Combatant != null || bot.Controled)
                    continue;

                PlayerBotInfo botInfo = GetBotInfo(bot);
                if (botInfo == null)
                    continue;

                // Check if bot is currently traveling
                if (botInfo.CurrentState == BotBehaviorState.Traveling)
                {
                    // Check if bot has reached destination or been traveling too long
                    if (bot.InRange(botInfo.Destination, 5) || 
                        DateTime.Now - botInfo.LastStateChange > TimeSpan.FromMinutes(10))
                    {
                        UpdateBotInfo(bot, BotBehaviorState.Idle, Point3D.Zero);
                        if (DEBUG_ENABLED)
                            Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' completed travel to {2}", 
                                GetTimestamp(), bot.Name, botInfo.Destination);
                    }
                    continue; // Skip other travel decisions while traveling
                }

                // Random chance for inter-region travel (long distance)
                if (Utility.Random(100) < INTER_REGION_TRAVEL_CHANCE)
                {
                    if (InitiateInterRegionTravel(bot))
                        interRegionTravelers++;
                }
                // Random chance for local travel
                else if (Utility.Random(100) < TRAVEL_CHANCE_PERCENT)
                {
                    if (InitiateLocalTravel(bot))
                        localTravelers++;
                }
            }

            if (DEBUG_ENABLED && (localTravelers > 0 || interRegionTravelers > 0))
                Console.WriteLine("[{0}] [PlayerBotDirector] Initiated travel: {1} local, {2} inter-region", 
                    GetTimestamp(), localTravelers, interRegionTravelers);
        }

        private bool InitiateInterRegionTravel(PlayerBot bot)
        {
            // Find a random region different from current location
            RegionProfile targetRegion = null;
            RegionProfile currentRegion = GetBotCurrentRegion(bot);
            
            List<RegionProfile> otherRegions = new List<RegionProfile>();
            foreach (RegionProfile region in m_Regions)
            {
                if (region != currentRegion)
                    otherRegions.Add(region);
            }

            if (otherRegions.Count == 0)
                return false;

            targetRegion = otherRegions[Utility.Random(otherRegions.Count)];

            // Find a point of interest in the target region
            List<PointOfInterest> targetPOIs = new List<PointOfInterest>();
            foreach (PointOfInterest poi in m_PointsOfInterest)
            {
                if (poi.Map == targetRegion.Map && targetRegion.Area.Contains(poi.Location))
                    targetPOIs.Add(poi);
            }

            Point3D destination;
            string destinationName;
            
            if (targetPOIs.Count > 0)
            {
                PointOfInterest targetPOI = targetPOIs[Utility.Random(targetPOIs.Count)];
                destination = targetPOI.Location;
                destinationName = targetPOI.Name;
            }
            else
            {
                // Fallback to random point in target region
                destination = new Point3D(
                    Utility.RandomMinMax(targetRegion.Area.Start.X, targetRegion.Area.End.X),
                    Utility.RandomMinMax(targetRegion.Area.Start.Y, targetRegion.Area.End.Y),
                    0
                );
                destinationName = targetRegion.Name;
            }

            UpdateBotInfo(bot, BotBehaviorState.Traveling, destination);

            if (DEBUG_ENABLED)
                Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' beginning inter-region travel from {2} to {3} ({4})", 
                    GetTimestamp(), bot.Name, GetBotCurrentRegionName(bot), destinationName, destination);

            // Set AI to wander towards destination
            if (bot.AIObject != null)
                bot.AIObject.Action = ActionType.Wander;

            return true;
        }

        private bool InitiateLocalTravel(PlayerBot bot)
        {
            // Choose a local point of interest or random nearby location
            Point3D currentLoc = bot.Location;
            Map map = bot.Map;
            
            if (map == null)
                return false;

            Point3D destination;
            string destinationName = "nearby area";

            // 60% chance to visit a nearby POI, 40% chance for random travel
            if (Utility.Random(100) < 60)
            {
                List<PointOfInterest> nearbyPOIs = new List<PointOfInterest>();
                foreach (PointOfInterest poi in m_PointsOfInterest)
                {
                    if (poi.Map == map && bot.GetDistanceToSqrt(poi.Location) < 100)
                        nearbyPOIs.Add(poi);
                }

                if (nearbyPOIs.Count > 0)
                {
                    PointOfInterest targetPOI = nearbyPOIs[Utility.Random(nearbyPOIs.Count)];
                    destination = targetPOI.Location;
                    destinationName = targetPOI.Name;
                }
                else
                {
                    // Fallback to random travel
                    return InitiateBotTravel(bot);
                }
            }
            else
            {
                // Random local travel
                return InitiateBotTravel(bot);
            }

            UpdateBotInfo(bot, BotBehaviorState.Traveling, destination);

            if (DEBUG_ENABLED)
                Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' traveling locally to {2} ({3})", 
                    GetTimestamp(), bot.Name, destinationName, destination);

            // Set AI to wander towards destination
            if (bot.AIObject != null)
                bot.AIObject.Action = ActionType.Wander;

            return true;
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
                if (botInfo == null || botInfo.CurrentState == BotBehaviorState.Traveling)
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

            if (DEBUG_ENABLED && (shoppingBots > 0 || socializing > 0))
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
                    if (Utility.Random(100) < SHOP_VISIT_CHANCE)
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

            if (DEBUG_ENABLED)
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

            if (DEBUG_ENABLED)
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

            if (DEBUG_ENABLED)
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

            if (DEBUG_ENABLED)
                Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' exploring at {2}", 
                    GetTimestamp(), bot.Name, poi.Name);
        }

        private void ProcessDynamicEvents(List<PlayerBot> allBots)
        {
            // Random chance for dynamic events
            if (Utility.Random(100) < DYNAMIC_EVENT_CHANCE)
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

                    if (DEBUG_ENABLED)
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

                    if (DEBUG_ENABLED)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Dynamic event: Trading between '{1}' and '{2}'", 
                            GetTimestamp(), crafter.Name, customer.Name);
                }
            }
        }

        private void CreateGroupActivity(List<PlayerBot> allBots)
        {
            // Find groups of bots near each other
            Dictionary<Point3D, List<PlayerBot>> proximityGroups = new Dictionary<Point3D, List<PlayerBot>>();
            
            foreach (PlayerBot bot in allBots)
            {
                if (bot.Combatant != null || bot.Controled)
                    continue;

                Point3D groupKey = new Point3D(
                    (bot.Location.X / 20) * 20,
                    (bot.Location.Y / 20) * 20,
                    0
                );
                
                if (!proximityGroups.ContainsKey(groupKey))
                    proximityGroups[groupKey] = new List<PlayerBot>();
                
                proximityGroups[groupKey].Add(bot);
            }

            foreach (List<PlayerBot> group in proximityGroups.Values)
            {
                if (group.Count >= 3) // Need at least 3 bots for group activity
                {
                    PlayerBot leader = group[Utility.Random(group.Count)];
                    
                    List<string> groupMessages = new List<string>();
                    groupMessages.Add("Anyone interested in a group venture?");
                    groupMessages.Add("There's safety in numbers.");
                    groupMessages.Add("Shall we travel together?");
                    
                    string message = groupMessages[Utility.Random(groupMessages.Count)];
                    leader.SayWithHue(message);
                    leader.LastSpeechTime = DateTime.Now;

                    if (DEBUG_ENABLED)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Dynamic event: Group activity initiated by '{1}' with {2} nearby bots", 
                            GetTimestamp(), leader.Name, group.Count - 1);
                    
                    break; // Only one group activity per tick
                }
            }
        }
        #endregion

        #region Helper Methods
        private bool InitiateBotTravel(PlayerBot bot)
        {
            // Choose a random travel destination within the same map (fallback method)
            Point3D currentLoc = bot.Location;
            Map map = bot.Map;
            
            if (map == null)
                return false;

            // Generate a random travel destination
            int distance = Utility.RandomMinMax(MIN_TRAVEL_DISTANCE, MAX_TRAVEL_DISTANCE);
            int angle = Utility.Random(360);
            
            double radians = angle * Math.PI / 180.0;
            int deltaX = (int)(Math.Cos(radians) * distance);
            int deltaY = (int)(Math.Sin(radians) * distance);
            
            Point3D destination = new Point3D(
                currentLoc.X + deltaX,
                currentLoc.Y + deltaY,
                map.GetAverageZ(currentLoc.X + deltaX, currentLoc.Y + deltaY)
            );

            // Validate the destination
            if (!CanSpawnAt(map, destination))
            {
                if (DEBUG_ENABLED)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' travel destination {2} is invalid", 
                        GetTimestamp(), bot.Name, destination);
                return false;
            }

            UpdateBotInfo(bot, BotBehaviorState.Traveling, destination);

            // Set the bot's destination (this will be handled by the AI)
            if (bot.AIObject != null)
            {
                bot.AIObject.Action = ActionType.Wander;
                
                if (DEBUG_ENABLED)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' beginning random travel from {2} towards {3} (distance: {4})", 
                        GetTimestamp(), bot.Name, currentLoc, destination, distance);
                
                return true;
            }

            return false;
        }

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

            if (DEBUG_ENABLED && interactions > 0)
                Console.WriteLine("[{0}] [PlayerBotDirector] Processed {1} bot interactions", GetTimestamp(), interactions);
        }

        private int ProcessGroupInteractions(List<PlayerBot> group)
        {
            int interactions = 0;
            
            foreach (PlayerBot bot in group)
            {
                // Random chance for this bot to initiate interaction
                if (Utility.Random(100) < INTERACTION_CHANCE_PERCENT)
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
                
                if (DEBUG_ENABLED)
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
            
            if (DEBUG_ENABLED)
                Console.WriteLine("[{0}] [PlayerBotDirector] Bot response: '{1}' -> '{2}': \"{3}\"", 
                    GetTimestamp(), responder.Name, initiator.Name, response);
        }
        #endregion
    }
} 