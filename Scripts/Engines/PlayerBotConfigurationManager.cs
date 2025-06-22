using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Mobiles;

namespace Server.Engines
{
    /// <summary>
    /// Manages configuration files for the PlayerBot system.
    /// Parses INI-style configuration files and provides structured data.
    /// </summary>
    public static class PlayerBotConfigurationManager
    {
        #region Constants
        private const string CONFIG_BASE_PATH = "Data/PlayerBot/";
        private const string REGIONS_FILE = "Regions.cfg";
        private const string POIS_FILE = "PointsOfInterest.cfg";
        private const string ROUTES_FILE = "TravelRoutes.cfg";
        private const string BEHAVIOR_FILE = "BehaviorSettings.cfg";
        #endregion

        #region Configuration Data
        private static Dictionary<string, RegionConfig> m_Regions;
        private static Dictionary<string, POIConfig> m_PointsOfInterest;
        private static Dictionary<string, RouteConfig> m_TravelRoutes;
        private static BehaviorConfig m_BehaviorSettings;
        private static DateTime m_LastLoadTime;
        #endregion

        #region Public Properties
        public static Dictionary<string, RegionConfig> Regions { get { return m_Regions ?? new Dictionary<string, RegionConfig>(); } }
        public static Dictionary<string, POIConfig> PointsOfInterest { get { return m_PointsOfInterest ?? new Dictionary<string, POIConfig>(); } }
        public static Dictionary<string, RouteConfig> TravelRoutes { get { return m_TravelRoutes ?? new Dictionary<string, RouteConfig>(); } }
        public static BehaviorConfig BehaviorSettings { get { return m_BehaviorSettings ?? new BehaviorConfig(); } }
        public static DateTime LastLoadTime { get { return m_LastLoadTime; } }
        #endregion

        #region Initialization
        /// <summary>
        /// Load all configuration files
        /// </summary>
        public static void Initialize()
        {
            Console.WriteLine("[{0}] [PlayerBotConfig] Loading configuration files...", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            
            try
            {
                LoadRegions();
                LoadPointsOfInterest();
                LoadTravelRoutes();
                LoadBehaviorSettings();
                
                m_LastLoadTime = DateTime.Now;
                
                Console.WriteLine("[{0}] [PlayerBotConfig] Configuration loaded successfully:", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Console.WriteLine("[{0}]   - {1} regions ({2} active)", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), 
                    m_Regions.Count, GetActiveRegionCount());
                Console.WriteLine("[{0}]   - {1} points of interest", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), 
                    m_PointsOfInterest.Count);
                Console.WriteLine("[{0}]   - {1} travel routes", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), 
                    m_TravelRoutes.Count);
                Console.WriteLine("[{0}]   - Behavior settings loaded", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("[{0}] [PlayerBotConfig] Error loading configuration: {1}", 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), ex.Message);
                
                // Create default configurations if loading fails
                CreateDefaultConfigurations();
            }
        }

        /// <summary>
        /// Reload all configuration files (for runtime changes)
        /// </summary>
        public static void Reload()
        {
            Console.WriteLine("[{0}] [PlayerBotConfig] Reloading configuration files...", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Initialize();
        }
        #endregion

        #region Configuration Loading
        private static void LoadRegions()
        {
            string filePath = Path.Combine(CONFIG_BASE_PATH, REGIONS_FILE);
            m_Regions = new Dictionary<string, RegionConfig>();
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine("[{0}] [PlayerBotConfig] Warning: {1} not found, using defaults", 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), filePath);
                return;
            }

            Dictionary<string, Dictionary<string, string>> sections = ParseINIFile(filePath);
            
            foreach (KeyValuePair<string, Dictionary<string, string>> section in sections)
            {
                try
                {
                    RegionConfig region = new RegionConfig();
                    region.Name = section.Key;
                    region.Map = ParseMap(GetValue(section.Value, "Map", "Felucca"));
                    region.Bounds = ParseRectangle(GetValue(section.Value, "Bounds", "0,0,100,100"));
                    region.MinBots = ParseInt(GetValue(section.Value, "MinBots", "1"));
                    region.MaxBots = ParseInt(GetValue(section.Value, "MaxBots", "5"));
                    region.SpawnWeight = ParseDouble(GetValue(section.Value, "SpawnWeight", "1.0"));
                    region.SafetyLevel = ParseSafetyLevel(GetValue(section.Value, "SafetyLevel", "Wilderness"));
                    region.Active = ParseBool(GetValue(section.Value, "Active", "true"));
                    
                    m_Regions[region.Name] = region;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[{0}] [PlayerBotConfig] Error parsing region '{1}': {2}", 
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), section.Key, ex.Message);
                }
            }
        }

        private static void LoadPointsOfInterest()
        {
            string filePath = Path.Combine(CONFIG_BASE_PATH, POIS_FILE);
            m_PointsOfInterest = new Dictionary<string, POIConfig>();
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine("[{0}] [PlayerBotConfig] Warning: {1} not found, using defaults", 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), filePath);
                return;
            }

            Dictionary<string, Dictionary<string, string>> sections = ParseINIFile(filePath);
            
            foreach (KeyValuePair<string, Dictionary<string, string>> section in sections)
            {
                try
                {
                    POIConfig poi = new POIConfig();
                    poi.Name = section.Key;
                    poi.Type = ParsePOIType(GetValue(section.Value, "Type", "Landmark"));
                    poi.Map = ParseMap(GetValue(section.Value, "Map", "Felucca"));
                    poi.Location = ParsePoint3D(GetValue(section.Value, "Location", "0,0,0"));
                    poi.Region = GetValue(section.Value, "Region", "");
                    poi.VisitChance = ParseInt(GetValue(section.Value, "VisitChance", "10"));
                    poi.BehaviorMessages = ParseStringArray(GetValue(section.Value, "BehaviorMessages", ""));
                    poi.ProfilePreference = ParseProfilePreference(GetValue(section.Value, "ProfilePreference", "All"));
                    
                    m_PointsOfInterest[poi.Name] = poi;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[{0}] [PlayerBotConfig] Error parsing POI '{1}': {2}", 
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), section.Key, ex.Message);
                }
            }
        }

        private static void LoadTravelRoutes()
        {
            string filePath = Path.Combine(CONFIG_BASE_PATH, ROUTES_FILE);
            m_TravelRoutes = new Dictionary<string, RouteConfig>();
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine("[{0}] [PlayerBotConfig] Warning: {1} not found, using defaults", 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), filePath);
                return;
            }

            Dictionary<string, Dictionary<string, string>> sections = ParseINIFile(filePath);
            
            foreach (KeyValuePair<string, Dictionary<string, string>> section in sections)
            {
                try
                {
                    RouteConfig route = new RouteConfig();
                    route.Name = section.Key;
                    route.From = GetValue(section.Value, "From", "");
                    route.To = GetValue(section.Value, "To", "");
                    route.Waypoints = ParseWaypoints(GetValue(section.Value, "Waypoints", ""));
                    route.TravelTime = ParseInt(GetValue(section.Value, "TravelTime", "10"));
                    route.Difficulty = ParseDifficulty(GetValue(section.Value, "Difficulty", "Easy"));
                    route.Map = ParseMap(GetValue(section.Value, "Map", "Felucca"));
                    
                    m_TravelRoutes[route.Name] = route;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[{0}] [PlayerBotConfig] Error parsing route '{1}': {2}", 
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), section.Key, ex.Message);
                }
            }
        }

        private static void LoadBehaviorSettings()
        {
            string filePath = Path.Combine(CONFIG_BASE_PATH, BEHAVIOR_FILE);
            m_BehaviorSettings = new BehaviorConfig();
            
            if (!File.Exists(filePath))
            {
                Console.WriteLine("[{0}] [PlayerBotConfig] Warning: {1} not found, using defaults", 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), filePath);
                return;
            }

            Dictionary<string, Dictionary<string, string>> sections = ParseINIFile(filePath);
            
            // Population settings
            if (sections.ContainsKey("Population"))
            {
                Dictionary<string, string> pop = sections["Population"];
                m_BehaviorSettings.GlobalCap = ParseInt(GetValue(pop, "GlobalCap", "200"));
                m_BehaviorSettings.PopulationTickSeconds = ParseInt(GetValue(pop, "PopulationTickSeconds", "30"));
                m_BehaviorSettings.StartupDelaySeconds = ParseInt(GetValue(pop, "StartupDelaySeconds", "10"));
                m_BehaviorSettings.SpawnLocationAttempts = ParseInt(GetValue(pop, "SpawnLocationAttempts", "20"));
            }
            
            // Behavior settings
            if (sections.ContainsKey("Behavior"))
            {
                Dictionary<string, string> beh = sections["Behavior"];
                m_BehaviorSettings.BehaviorTickSeconds = ParseInt(GetValue(beh, "BehaviorTickSeconds", "45"));
                m_BehaviorSettings.TravelChancePercent = ParseInt(GetValue(beh, "TravelChancePercent", "15"));
                m_BehaviorSettings.InteractionChancePercent = ParseInt(GetValue(beh, "InteractionChancePercent", "25"));
                m_BehaviorSettings.InterRegionTravelChance = ParseInt(GetValue(beh, "InterRegionTravelChance", "5"));
                m_BehaviorSettings.ShopVisitChance = ParseInt(GetValue(beh, "ShopVisitChance", "10"));
                m_BehaviorSettings.DynamicEventChance = ParseInt(GetValue(beh, "DynamicEventChance", "3"));
            }
            
            // Travel settings
            if (sections.ContainsKey("Travel"))
            {
                Dictionary<string, string> travel = sections["Travel"];
                m_BehaviorSettings.MinTravelDistance = ParseInt(GetValue(travel, "MinTravelDistance", "10"));
                m_BehaviorSettings.MaxTravelDistance = ParseInt(GetValue(travel, "MaxTravelDistance", "50"));
                m_BehaviorSettings.TravelTimeoutMinutes = ParseInt(GetValue(travel, "TravelTimeoutMinutes", "10"));
                m_BehaviorSettings.LocalTravelPreference = ParseInt(GetValue(travel, "LocalTravelPreference", "60"));
                m_BehaviorSettings.DestinationRange = ParseInt(GetValue(travel, "DestinationRange", "5"));
            }
            
            // Interaction settings
            if (sections.ContainsKey("Interaction"))
            {
                Dictionary<string, string> inter = sections["Interaction"];
                m_BehaviorSettings.InteractionCooldownSeconds = ParseInt(GetValue(inter, "InteractionCooldownSeconds", "30"));
                m_BehaviorSettings.ResponseDelayMinSeconds = ParseInt(GetValue(inter, "ResponseDelayMinSeconds", "2"));
                m_BehaviorSettings.ResponseDelayMaxSeconds = ParseInt(GetValue(inter, "ResponseDelayMaxSeconds", "5"));
                m_BehaviorSettings.InteractionRange = ParseInt(GetValue(inter, "InteractionRange", "5"));
                m_BehaviorSettings.ProximityGroupSize = ParseInt(GetValue(inter, "ProximityGroupSize", "10"));
            }
            
            // Persona Distribution settings
            if (sections.ContainsKey("PersonaDistribution"))
            {
                Dictionary<string, string> persona = sections["PersonaDistribution"];
                m_BehaviorSettings.DefaultAdventurerPercent = ParseInt(GetValue(persona, "DefaultAdventurerPercent", "50"));
                m_BehaviorSettings.DefaultCrafterPercent = ParseInt(GetValue(persona, "DefaultCrafterPercent", "35"));
                m_BehaviorSettings.DefaultPlayerKillerPercent = ParseInt(GetValue(persona, "DefaultPlayerKillerPercent", "15"));
                m_BehaviorSettings.SafeAdventurerPercent = ParseInt(GetValue(persona, "SafeAdventurerPercent", "45"));
                m_BehaviorSettings.SafeCrafterPercent = ParseInt(GetValue(persona, "SafeCrafterPercent", "50"));
                m_BehaviorSettings.SafePlayerKillerPercent = ParseInt(GetValue(persona, "SafePlayerKillerPercent", "5"));
                m_BehaviorSettings.DangerousAdventurerPercent = ParseInt(GetValue(persona, "DangerousAdventurerPercent", "40"));
                m_BehaviorSettings.DangerousCrafterPercent = ParseInt(GetValue(persona, "DangerousCrafterPercent", "20"));
                m_BehaviorSettings.DangerousPlayerKillerPercent = ParseInt(GetValue(persona, "DangerousPlayerKillerPercent", "40"));
                m_BehaviorSettings.WildernessAdventurerPercent = ParseInt(GetValue(persona, "WildernessAdventurerPercent", "50"));
                m_BehaviorSettings.WildernessCrafterPercent = ParseInt(GetValue(persona, "WildernessCrafterPercent", "30"));
                m_BehaviorSettings.WildernessPlayerKillerPercent = ParseInt(GetValue(persona, "WildernessPlayerKillerPercent", "20"));
            }

            // Debug settings
            if (sections.ContainsKey("Debug"))
            {
                Dictionary<string, string> debug = sections["Debug"];
                m_BehaviorSettings.EnableLogging = ParseBool(GetValue(debug, "EnableLogging", "true"));
                m_BehaviorSettings.VerboseSpawning = ParseBool(GetValue(debug, "VerboseSpawning", "true"));
                m_BehaviorSettings.VerboseTravel = ParseBool(GetValue(debug, "VerboseTravel", "true"));
                m_BehaviorSettings.VerboseInteractions = ParseBool(GetValue(debug, "VerboseInteractions", "true"));
                m_BehaviorSettings.VerboseBehaviors = ParseBool(GetValue(debug, "VerboseBehaviors", "true"));
                m_BehaviorSettings.VerboseEvents = ParseBool(GetValue(debug, "VerboseEvents", "true"));
            }
        }
        #endregion

        #region Helper Methods
        private static Dictionary<string, Dictionary<string, string>> ParseINIFile(string filePath)
        {
            Dictionary<string, Dictionary<string, string>> result = new Dictionary<string, Dictionary<string, string>>();
            
            if (!File.Exists(filePath))
                return result;
                
            string[] lines = File.ReadAllLines(filePath);
            string currentSection = "";
            Dictionary<string, string> currentSectionData = null;
            
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                
                // Skip comments and empty lines
                if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                    continue;
                
                // Section header
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    currentSectionData = new Dictionary<string, string>();
                    result[currentSection] = currentSectionData;
                    continue;
                }
                
                // Key-value pair
                if (currentSectionData != null && trimmed.Contains("="))
                {
                    int equalsIndex = trimmed.IndexOf('=');
                    string key = trimmed.Substring(0, equalsIndex).Trim();
                    string value = trimmed.Substring(equalsIndex + 1).Trim();
                    currentSectionData[key] = value;
                }
            }
            
            return result;
        }

        private static string GetValue(Dictionary<string, string> section, string key, string defaultValue)
        {
            if (section.ContainsKey(key))
                return section[key];
            return defaultValue;
        }

        private static int ParseInt(string value)
        {
            int result;
            if (int.TryParse(value, out result))
                return result;
            return 0;
        }

        private static double ParseDouble(string value)
        {
            double result;
            if (double.TryParse(value, out result))
                return result;
            return 1.0;
        }

        private static bool ParseBool(string value)
        {
            return value.ToLower() == "true" || value == "1";
        }

        private static Map ParseMap(string value)
        {
            switch (value.ToLower())
            {
                case "felucca": return Map.Felucca;
                case "trammel": return Map.Trammel;
                case "ilshenar": return Map.Ilshenar;
                case "malas": return Map.Malas;
                case "tokuno": return Map.Tokuno;
                default: return Map.Felucca;
            }
        }

        private static Rectangle2D ParseRectangle(string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length == 4)
            {
                int x1 = ParseInt(parts[0]);
                int y1 = ParseInt(parts[1]);
                int x2 = ParseInt(parts[2]);
                int y2 = ParseInt(parts[3]);
                return new Rectangle2D(x1, y1, x2 - x1, y2 - y1);
            }
            return new Rectangle2D(0, 0, 100, 100);
        }

        private static Point3D ParsePoint3D(string value)
        {
            string[] parts = value.Split(',');
            if (parts.Length >= 2)
            {
                int x = ParseInt(parts[0]);
                int y = ParseInt(parts[1]);
                int z = parts.Length > 2 ? ParseInt(parts[2]) : 0;
                return new Point3D(x, y, z);
            }
            return Point3D.Zero;
        }

        private static Point3D[] ParseWaypoints(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new Point3D[0];
                
            string[] waypointStrings = value.Split('|');
            Point3D[] waypoints = new Point3D[waypointStrings.Length];
            
            for (int i = 0; i < waypointStrings.Length; i++)
            {
                waypoints[i] = ParsePoint3D(waypointStrings[i]);
            }
            
            return waypoints;
        }

        private static string[] ParseStringArray(string value)
        {
            if (string.IsNullOrEmpty(value))
                return new string[0];
                
            return value.Split('|');
        }

        private static PlayerBotDirector.POIType ParsePOIType(string value)
        {
            switch (value.ToLower())
            {
                case "bank": return PlayerBotDirector.POIType.Bank;
                case "shop": return PlayerBotDirector.POIType.Shop;
                case "tavern": return PlayerBotDirector.POIType.Tavern;
                case "healer": return PlayerBotDirector.POIType.Healer;
                case "dungeon": return PlayerBotDirector.POIType.Dungeon;
                case "landmark": return PlayerBotDirector.POIType.Landmark;
                case "waypoint": return PlayerBotDirector.POIType.Waypoint;
                default: return PlayerBotDirector.POIType.Landmark;
            }
        }

        private static PlayerBotPersona.PlayerBotProfile ParseProfilePreference(string value)
        {
            switch (value.ToLower())
            {
                case "crafter": return PlayerBotPersona.PlayerBotProfile.Crafter;
                case "adventurer": return PlayerBotPersona.PlayerBotProfile.Adventurer;
                case "playerkiller": return PlayerBotPersona.PlayerBotProfile.PlayerKiller;
                default: return PlayerBotPersona.PlayerBotProfile.Adventurer; // "All" defaults to Adventurer
            }
        }

        private static RouteDifficulty ParseDifficulty(string value)
        {
            switch (value.ToLower())
            {
                case "easy": return RouteDifficulty.Easy;
                case "medium": return RouteDifficulty.Medium;
                case "hard": return RouteDifficulty.Hard;
                default: return RouteDifficulty.Easy;
            }
        }

        private static SafetyLevel ParseSafetyLevel(string value)
        {
            switch (value.ToLower())
            {
                case "safe": return SafetyLevel.Safe;
                case "wilderness": return SafetyLevel.Wilderness;
                case "dangerous": return SafetyLevel.Dangerous;
                default: return SafetyLevel.Wilderness;
            }
        }

        private static int GetActiveRegionCount()
        {
            int count = 0;
            foreach (RegionConfig region in m_Regions.Values)
            {
                if (region.Active)
                    count++;
            }
            return count;
        }

        private static void CreateDefaultConfigurations()
        {
            Console.WriteLine("[{0}] [PlayerBotConfig] Creating default configurations...", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            
            // Create minimal default configurations
            m_Regions = new Dictionary<string, RegionConfig>();
            m_PointsOfInterest = new Dictionary<string, POIConfig>();
            m_TravelRoutes = new Dictionary<string, RouteConfig>();
            m_BehaviorSettings = new BehaviorConfig();
        }
        #endregion

        #region Configuration Data Classes
        public class RegionConfig
        {
            public string Name;
            public Map Map;
            public Rectangle2D Bounds;
            public int MinBots;
            public int MaxBots;
            public double SpawnWeight;
            public SafetyLevel SafetyLevel;
            public bool Active;
        }

        public class POIConfig
        {
            public string Name;
            public PlayerBotDirector.POIType Type;
            public Map Map;
            public Point3D Location;
            public string Region;
            public int VisitChance;
            public string[] BehaviorMessages;
            public PlayerBotPersona.PlayerBotProfile ProfilePreference;
        }

        public class RouteConfig
        {
            public string Name;
            public string From;
            public string To;
            public Point3D[] Waypoints;
            public int TravelTime;
            public RouteDifficulty Difficulty;
            public Map Map;
        }

        public class BehaviorConfig
        {
            // Population
            public int GlobalCap = 200;
            public int PopulationTickSeconds = 30;
            public int StartupDelaySeconds = 10;
            public int SpawnLocationAttempts = 20;
            
            // Behavior
            public int BehaviorTickSeconds = 45;
            public int TravelChancePercent = 15;
            public int InteractionChancePercent = 25;
            public int InterRegionTravelChance = 5;
            public int ShopVisitChance = 10;
            public int DynamicEventChance = 3;
            
            // Travel
            public int MinTravelDistance = 10;
            public int MaxTravelDistance = 50;
            public int TravelTimeoutMinutes = 10;
            public int LocalTravelPreference = 60;
            public int DestinationRange = 5;
            
            // Interaction
            public int InteractionCooldownSeconds = 30;
            public int ResponseDelayMinSeconds = 2;
            public int ResponseDelayMaxSeconds = 5;
            public int InteractionRange = 5;
            public int ProximityGroupSize = 10;
            
            // Persona Distribution
            public int DefaultAdventurerPercent = 50;
            public int DefaultCrafterPercent = 35;
            public int DefaultPlayerKillerPercent = 15;
            public int SafeAdventurerPercent = 45;
            public int SafeCrafterPercent = 50;
            public int SafePlayerKillerPercent = 5;
            public int DangerousAdventurerPercent = 40;
            public int DangerousCrafterPercent = 20;
            public int DangerousPlayerKillerPercent = 40;
            public int WildernessAdventurerPercent = 50;
            public int WildernessCrafterPercent = 30;
            public int WildernessPlayerKillerPercent = 20;

            // Debug
            public bool EnableLogging = true;
            public bool VerboseSpawning = true;
            public bool VerboseTravel = true;
            public bool VerboseInteractions = true;
            public bool VerboseBehaviors = true;
            public bool VerboseEvents = true;
        }

        public enum RouteDifficulty
        {
            Easy,
            Medium,
            Hard
        }

        public enum SafetyLevel
        {
            Safe,
            Wilderness,
            Dangerous
        }
        #endregion
    }
} 