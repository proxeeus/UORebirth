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

        private const int GLOBAL_CAP = 200;            // Absolute maximum bots allowed shard-wide
        private const int POPULATION_TICK_SECONDS = 30; // How often to evaluate population

        // Debug toggle - set to false to disable verbose logging
        private static bool DEBUG_ENABLED = true;

        private Timer m_Timer;
        private List<RegionProfile> m_Regions;

        // Helper method to get formatted timestamp for logging
        private static string GetTimestamp()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private PlayerBotDirector()
        {
            m_Regions = new List<RegionProfile>();
            SeedDefaultRegions();

            // Delay a few seconds after server start, then tick regularly.
            m_Timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(POPULATION_TICK_SECONDS), new TimerCallback(OnPopulationTick));
            
            if (DEBUG_ENABLED)
                Console.WriteLine("[{0}] [PlayerBotDirector] Constructor completed. Timer will start in 10 seconds, then tick every {1} seconds.", GetTimestamp(), POPULATION_TICK_SECONDS);
        }

        [CallPriority(int.MaxValue)]
        public static void Initialize()
        {
            // Accessing Instance forces the singleton to construct and start the timer.
            PlayerBotDirector unused = Instance;
            Console.WriteLine("[{0}] [PlayerBotDirector] Initialized (phase 1) - Debug mode: {1}", GetTimestamp(), DEBUG_ENABLED ? "ON" : "OFF");
        }

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

                // Gather current live bots
                List<PlayerBot> allBots = new List<PlayerBot>();
                foreach (Mobile m in World.Mobiles.Values)
                {
                    PlayerBot bot = m as PlayerBot;
                    if (bot != null && !bot.Deleted)
                        allBots.Add(bot);
                }

                if (DEBUG_ENABLED)
                    Console.WriteLine("[{0}] [PlayerBotDirector] Found {1} total PlayerBots in world (global cap: {2})", GetTimestamp(), allBots.Count, GLOBAL_CAP);

                // Global cap guard
                if (allBots.Count >= GLOBAL_CAP)
                {
                    if (DEBUG_ENABLED)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Global cap reached! No new bots will be spawned.", GetTimestamp());
                    return; // nothing to do, shard is at capacity
                }

                int totalSpawned = 0;
                foreach (RegionProfile profile in m_Regions)
                {
                    int spawned = EnsureRegionPopulation(profile, allBots);
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

        private int EnsureRegionPopulation(RegionProfile profile, List<PlayerBot> allBots)
        {
            // Count existing bots inside region rectangle & map
            int count = 0;
            foreach (PlayerBot bot in allBots)
            {
                if (bot.Map == profile.Map && profile.Area.Contains(bot.Location))
                    count++;
            }

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
            int globalRemaining = GLOBAL_CAP - allBots.Count;
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
                    Console.WriteLine("[{0}] [PlayerBotDirector] Failed to find spawn location in region '{1}' after 20 attempts", GetTimestamp(), profile.Name);
                return false; // failed to find safe spot
            }

            PlayerBot bot = new PlayerBot(); // Uses default random persona
            bot.MoveToWorld(loc, profile.Map);

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
            for (int i = 0; i < 20; i++) // try up to 20 random points
            {
                int x = Utility.RandomMinMax(profile.Area.Start.X, profile.Area.End.X);
                int y = Utility.RandomMinMax(profile.Area.Start.Y, profile.Area.End.Y);
                int z = map.GetAverageZ(x, y);
                Point3D p = new Point3D(x, y, z);

                if (CanSpawnAt(map, p))
                {
                    if (DEBUG_ENABLED)
                        Console.WriteLine("[{0}] [PlayerBotDirector] Found spawn location {1} in region '{2}' (attempt {3}/20)", 
                            GetTimestamp(), p, profile.Name, i + 1);
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
    }
} 