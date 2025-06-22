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
        
        // Debug toggle - set to false to disable verbose logging
        private static bool DEBUG_ENABLED = true;
        #endregion

        #region Private Fields
        private Timer m_Timer;
        private Timer m_BehaviorTimer; // Phase 2: Behavior management timer
        private List<RegionProfile> m_Regions;
        
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
            SeedDefaultRegions();

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

        #region Nested types
        /// <summary>
        /// Information about a registered PlayerBot for lifecycle tracking
        /// </summary>
        private class PlayerBotInfo
        {
            public PlayerBot Bot { get; private set; }
            public DateTime SpawnTime { get; private set; }

            public PlayerBotInfo(PlayerBot bot)
            {
                Bot = bot;
                SpawnTime = DateTime.Now;
            }
        }

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

                // Phase 2 behaviors
                ProcessBotTravel(allBots);
                ProcessBotInteractions(allBots);

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
            int travelingBots = 0;
            
            foreach (PlayerBot bot in allBots)
            {
                // Skip bots that are in combat or controlled by players
                if (bot.Combatant != null || bot.Controled)
                    continue;

                // Random chance to travel
                if (Utility.Random(100) < TRAVEL_CHANCE_PERCENT)
                {
                    if (InitiateBotTravel(bot))
                        travelingBots++;
                }
            }

            if (DEBUG_ENABLED && travelingBots > 0)
                Console.WriteLine("[{0}] [PlayerBotDirector] Initiated travel for {1} bots", GetTimestamp(), travelingBots);
        }

        private bool InitiateBotTravel(PlayerBot bot)
        {
            // Choose a random travel destination within the same map
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

            // Set the bot's destination (this will be handled by the AI)
            if (bot.AIObject != null)
            {
                bot.AIObject.Action = ActionType.Wander;
                // In a more sophisticated implementation, we'd set a specific destination
                // For now, we'll just encourage wandering behavior
                
                if (DEBUG_ENABLED)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Bot '{1}' beginning travel from {2} towards {3} (distance: {4})", 
                        GetTimestamp(), bot.Name, currentLoc, destination, distance);
                
                return true;
            }

            return false;
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