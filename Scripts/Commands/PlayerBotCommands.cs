using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Engines;
using Server.Mobiles;

namespace Server.Commands
{
    public class PlayerBotCommands
    {
        public static void Initialize()
        {
            CommandSystem.Register("ReloadBotConfig", AccessLevel.Administrator, new CommandEventHandler(ReloadBotConfig_OnCommand));
            CommandSystem.Register("BotStatus", AccessLevel.GameMaster, new CommandEventHandler(BotStatus_OnCommand));
            CommandSystem.Register("BotInfo", AccessLevel.GameMaster, new CommandEventHandler(BotInfo_OnCommand));
            CommandSystem.Register("SpawnBot", AccessLevel.GameMaster, new CommandEventHandler(SpawnBot_OnCommand));
            CommandSystem.Register("DeleteBots", AccessLevel.Administrator, new CommandEventHandler(DeleteBots_OnCommand));
            CommandSystem.Register("BotDiagnostic", AccessLevel.Administrator, new CommandEventHandler(BotDiagnostic_OnCommand));
            CommandSystem.Register("FixUnmanagedBots", AccessLevel.Administrator, new CommandEventHandler(FixUnmanagedBots_OnCommand));
            CommandSystem.Register("CreateScene", AccessLevel.GameMaster, new CommandEventHandler(CreateScene_OnCommand));
            CommandSystem.Register("ListScenes", AccessLevel.GameMaster, new CommandEventHandler(ListScenes_OnCommand));
            CommandSystem.Register("EndScene", AccessLevel.GameMaster, new CommandEventHandler(EndScene_OnCommand));
            CommandSystem.Register("SceneInfo", AccessLevel.GameMaster, new CommandEventHandler(SceneInfo_OnCommand));
            CommandSystem.Register("BotSceneDebug", AccessLevel.GameMaster, new CommandEventHandler(BotSceneDebug_OnCommand));
            CommandSystem.Register("ForceScene", AccessLevel.GameMaster, new CommandEventHandler(ForceScene_OnCommand));
            CommandSystem.Register("SceneStatus", AccessLevel.GameMaster, new CommandEventHandler(SceneStatus_OnCommand));
            CommandSystem.Register("WarSceneTest", AccessLevel.GameMaster, new CommandEventHandler(WarSceneTest_OnCommand));
            CommandSystem.Register("FixCaravanKarma", AccessLevel.GameMaster, new CommandEventHandler(FixCaravanKarma_OnCommand));
            CommandSystem.Register("TestOrganicScenes", AccessLevel.GameMaster, new CommandEventHandler(TestOrganicScenes_OnCommand));
            CommandSystem.Register("WarEligibilityBreakdown", AccessLevel.GameMaster, new CommandEventHandler(WarEligibilityBreakdown_OnCommand));
            CommandSystem.Register("TestWarChanges", AccessLevel.GameMaster, new CommandEventHandler(TestWarChanges_OnCommand));
            CommandSystem.Register("DebugSceneCreation", AccessLevel.GameMaster, new CommandEventHandler(DebugSceneCreation_OnCommand));
        }

        [Usage("ReloadBotConfig")]
        [Description("Reloads all PlayerBot configuration files without server restart.")]
        public static void ReloadBotConfig_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            try
            {
                from.SendMessage("Reloading PlayerBot configuration files...");
                
                PlayerBotConfigurationManager.Reload();
                
                from.SendMessage(0x40, "PlayerBot configuration reloaded successfully!");
                from.SendMessage("- {0} regions loaded ({1} active)", 
                    PlayerBotConfigurationManager.Regions.Count, 
                    GetActiveRegionCount());
                from.SendMessage("- {0} points of interest loaded", 
                    PlayerBotConfigurationManager.PointsOfInterest.Count);
                from.SendMessage("- {0} travel routes loaded", 
                    PlayerBotConfigurationManager.TravelRoutes.Count);
                from.SendMessage("- Behavior settings updated");
                from.SendMessage("- Last loaded: {0}", 
                    PlayerBotConfigurationManager.LastLoadTime.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception ex)
            {
                from.SendMessage(0x25, "Error reloading configuration: {0}", ex.Message);
                Console.WriteLine("[PlayerBotCommands] Error reloading config: {0}", ex);
            }
        }

        [Usage("BotStatus")]
        [Description("Opens the comprehensive PlayerBot system status gump.")]
        public static void BotStatus_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            from.SendGump(new Server.Gumps.PlayerBotStatusGump(from));
        }

        [Usage("BotInfo")]
        [Description("Displays detailed information about PlayerBots in the area.")]
        public static void BotInfo_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            from.SendMessage("Searching for PlayerBots within 20 tiles...");
            
            List<PlayerBot> nearbyBots = new List<PlayerBot>();
            
            foreach (Mobile m in from.GetMobilesInRange(20))
            {
                PlayerBot bot = m as PlayerBot;
                if (bot != null && !bot.Deleted)
                {
                    nearbyBots.Add(bot);
                }
            }
            
            if (nearbyBots.Count == 0)
            {
                from.SendMessage("No PlayerBots found in the area.");
                return;
            }
            
            from.SendMessage(0x35, "Found {0} PlayerBot(s):", nearbyBots.Count);
            
            foreach (PlayerBot bot in nearbyBots)
            {
                string profile = bot.PlayerBotProfile != null ? bot.PlayerBotProfile.ToString() : "Unknown";
                string experience = bot.PlayerBotExperience != null ? bot.PlayerBotExperience.ToString() : "Unknown";
                string status = bot.Controled ? "Hired" : (bot.Combatant != null ? "Fighting" : "Free");
                
                from.SendMessage("  - {0} ({1} {2}) - {3} - Karma: {4}, Fame: {5}", 
                    bot.Name, experience, profile, status, bot.Karma, bot.Fame);
                from.SendMessage("    Location: {0} ({1})", bot.Location, bot.Map);
                
                if (bot.Controled && bot.ControlMaster != null)
                {
                    from.SendMessage("    Hired by: {0}", bot.ControlMaster.Name);
                }
            }
        }

        [Usage("SpawnBot [profile] [experience]")]
        [Description("Spawns a PlayerBot with optional profile and experience. Profiles: Adventurer, Crafter, PlayerKiller. Experience: Newbie, Average, Proficient, Grandmaster.")]
        public static void SpawnBot_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            PlayerBotPersona.PlayerBotProfile? profile = null;
            PlayerBotPersona.PlayerBotExperience? experience = null;
            
            // Parse arguments
            if (e.Arguments.Length > 0)
            {
                switch (e.Arguments[0].ToLower())
                {
                    case "adventurer": profile = PlayerBotPersona.PlayerBotProfile.Adventurer; break;
                    case "crafter": profile = PlayerBotPersona.PlayerBotProfile.Crafter; break;
                    case "playerkiller": 
                    case "pk": profile = PlayerBotPersona.PlayerBotProfile.PlayerKiller; break;
                    default:
                        from.SendMessage("Invalid profile. Valid options: Adventurer, Crafter, PlayerKiller");
                        return;
                }
            }
            
            if (e.Arguments.Length > 1)
            {
                switch (e.Arguments[1].ToLower())
                {
                    case "newbie": experience = PlayerBotPersona.PlayerBotExperience.Newbie; break;
                    case "average": experience = PlayerBotPersona.PlayerBotExperience.Average; break;
                    case "proficient": experience = PlayerBotPersona.PlayerBotExperience.Proficient; break;
                    case "grandmaster": 
                    case "gm": experience = PlayerBotPersona.PlayerBotExperience.Grandmaster; break;
                    default:
                        from.SendMessage("Invalid experience. Valid options: Newbie, Average, Proficient, Grandmaster");
                        return;
                }
            }
            
            try
            {
                PlayerBot bot = new PlayerBot();
                
                // Override persona if specified
                if (profile.HasValue)
                {
                    bot.OverridePersona(profile.Value);
                }
                
                if (experience.HasValue)
                {
                    bot.PlayerBotExperience = experience.Value;
                }
                
                // Spawn at player's location
                bot.MoveToWorld(from.Location, from.Map);
                
                // Register with director
                PlayerBotDirector.Instance.RegisterBot(bot);
                
                from.SendMessage(0x40, "Spawned PlayerBot '{0}' ({1} {2})", 
                    bot.Name, 
                    bot.PlayerBotExperience, 
                    bot.PlayerBotProfile);
            }
            catch (Exception ex)
            {
                from.SendMessage(0x25, "Error spawning bot: {0}", ex.Message);
                Console.WriteLine("[PlayerBotCommands] Error spawning bot: {0}", ex);
            }
        }

        [Usage("DeleteBots [range]")]
        [Description("Deletes all PlayerBots within specified range (default: 10 tiles). Use 'all' to delete all bots shard-wide.")]
        public static void DeleteBots_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            int range = 10;
            bool deleteAll = false;
            
            if (e.Arguments.Length > 0)
            {
                if (e.Arguments[0].ToLower() == "all")
                {
                    deleteAll = true;
                }
                else if (!int.TryParse(e.Arguments[0], out range))
                {
                    from.SendMessage("Invalid range. Use a number or 'all' for shard-wide deletion.");
                    return;
                }
            }
            
            List<PlayerBot> botsToDelete = new List<PlayerBot>();
            
            if (deleteAll)
            {
                foreach (Mobile m in World.Mobiles.Values)
                {
                    PlayerBot bot = m as PlayerBot;
                    if (bot != null && !bot.Deleted)
                    {
                        botsToDelete.Add(bot);
                    }
                }
            }
            else
            {
                foreach (Mobile m in from.GetMobilesInRange(range))
                {
                    PlayerBot bot = m as PlayerBot;
                    if (bot != null && !bot.Deleted)
                    {
                        botsToDelete.Add(bot);
                    }
                }
            }
            
            if (botsToDelete.Count == 0)
            {
                from.SendMessage("No PlayerBots found to delete.");
                return;
            }
            
            from.SendMessage("Deleting {0} PlayerBot(s)...", botsToDelete.Count);
            
            foreach (PlayerBot bot in botsToDelete)
            {
                bot.Delete();
            }
            
            from.SendMessage(0x40, "Deleted {0} PlayerBot(s).", botsToDelete.Count);
        }

        #region Helper Methods
        [Usage("BotDiagnostic")]
        [Description("Runs diagnostic checks on the PlayerBot system to identify issues.")]
        public static void BotDiagnostic_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            from.SendMessage(0x35, "=== PlayerBot System Diagnostic ===");
            
            // Get counts
            int worldCount = PlayerBotDirector.Instance.GetWorldPlayerBotCount();
            int registeredCount = PlayerBotDirector.Instance.GetRegisteredBotCount();
            List<PlayerBot> unmanaged = PlayerBotDirector.Instance.GetUnmanagedPlayerBots();
            
            from.SendMessage("Total PlayerBots in world: {0}", worldCount);
            from.SendMessage("Registered with director: {0}", registeredCount);
            from.SendMessage("Unmanaged bots: {0}", unmanaged.Count);
            
            if (unmanaged.Count > 0)
            {
                from.SendMessage(0x25, "WARNING: Found {0} unmanaged PlayerBot(s)!", unmanaged.Count);
                from.SendMessage("These bots exist in the world but are not managed by the director.");
                from.SendMessage("Use [FixUnmanagedBots to register them automatically.");
                
                if (unmanaged.Count <= 10)
                {
                    from.SendMessage("Unmanaged bots:");
                    foreach (PlayerBot bot in unmanaged)
                    {
                        from.SendMessage("  - {0} (Serial: {1}) at {2}", bot.Name, bot.Serial, bot.Location);
                    }
                }
                else
                {
                    from.SendMessage("Too many unmanaged bots to list individually ({0} total).", unmanaged.Count);
                }
            }
            else
            {
                from.SendMessage(0x40, "All PlayerBots are properly managed by the director.");
            }
            
            // Additional diagnostics
            from.SendMessage("=== Additional Information ===");
            from.SendMessage("Director initialized: {0}", PlayerBotDirector.Instance != null ? "Yes" : "No");
            from.SendMessage("Configuration loaded: {0}", PlayerBotConfigurationManager.LastLoadTime.ToString("yyyy-MM-dd HH:mm:ss"));
            from.SendMessage("Active regions: {0}", GetActiveRegionCount());
        }

        [Usage("FixUnmanagedBots")]
        [Description("Automatically registers all unmanaged PlayerBots with the director.")]
        public static void FixUnmanagedBots_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            from.SendMessage("Scanning for unmanaged PlayerBots...");
            
            int fixedCount = PlayerBotDirector.Instance.ForceRegisterUnmanagedBots();
            
            if (fixedCount > 0)
            {
                from.SendMessage(0x40, "Successfully registered {0} previously unmanaged PlayerBot(s).", fixedCount);
                from.SendMessage("All PlayerBots should now be properly managed by the director.");
            }
            else
            {
                from.SendMessage("No unmanaged PlayerBots found. All bots are already properly registered.");
            }
            
            // Show final counts
            int worldCount = PlayerBotDirector.Instance.GetWorldPlayerBotCount();
            int registeredCount = PlayerBotDirector.Instance.GetRegisteredBotCount();
            
            from.SendMessage("Final counts - World: {0}, Registered: {1}", worldCount, registeredCount);
        }

        private static int GetActiveRegionCount()
        {
            int count = 0;
            foreach (PlayerBotConfigurationManager.RegionConfig region in PlayerBotConfigurationManager.Regions.Values)
            {
                if (region.Active)
                    count++;
            }
            return count;
        }

        private static Dictionary<string, int> GetBotsPerRegion()
        {
            Dictionary<string, int> regionCounts = new Dictionary<string, int>();
            
            foreach (Mobile m in World.Mobiles.Values)
            {
                PlayerBot bot = m as PlayerBot;
                if (bot != null && !bot.Deleted && bot.Alive)
                {
                    // Find which region this bot is in
                    foreach (PlayerBotConfigurationManager.RegionConfig region in PlayerBotConfigurationManager.Regions.Values)
                    {
                        if (region.Active && region.Map == bot.Map && region.Bounds.Contains(bot.Location))
                        {
                            if (!regionCounts.ContainsKey(region.Name))
                                regionCounts[region.Name] = 0;
                            regionCounts[region.Name]++;
                            break;
                        }
                    }
                }
            }
            
            return regionCounts;
        }

        private static Dictionary<PlayerBotPersona.PlayerBotProfile, int> GetPersonaDistribution()
        {
            Dictionary<PlayerBotPersona.PlayerBotProfile, int> personaCounts = new Dictionary<PlayerBotPersona.PlayerBotProfile, int>();
            
            foreach (Mobile m in World.Mobiles.Values)
            {
                PlayerBot bot = m as PlayerBot;
                if (bot != null && !bot.Deleted && bot.Alive)
                {
                    PlayerBotPersona.PlayerBotProfile profile = bot.PlayerBotProfile;
                    if (!personaCounts.ContainsKey(profile))
                        personaCounts[profile] = 0;
                    personaCounts[profile]++;
                }
            }
            
            return personaCounts;
        }
        #endregion

        #region Scene Management Commands
        [Usage("CreateScene <type> [count]")]
        [Description("Creates a scene at your location. Types: War, Caravan. Optional count parameter for War scenes (default: 8).")]
        public static void CreateScene_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            if (e.Arguments.Length < 1)
            {
                from.SendMessage("Usage: [CreateScene <type> [count]");
                from.SendMessage("Available types: War, Caravan");
                from.SendMessage("War scenes accept optional participant count (default: 8)");
                return;
            }
            
            string sceneType = e.Arguments[0].ToLower();
            
            try
            {
                PlayerBotScene scene = null;
                
                switch (sceneType)
                {
                    case "war":
                        int participantCount = 8; // Default
                        if (e.Arguments.Length > 1)
                        {
                            if (!int.TryParse(e.Arguments[1], out participantCount) || participantCount < 2 || participantCount > 20)
                            {
                                from.SendMessage("Invalid participant count. Must be between 2 and 20.");
                                return;
                            }
                        }
                        
                        scene = new Server.Engines.Scenes.WarScene(from.Location, from.Map, participantCount);
                        break;
                        
                    case "caravan":
                        // Find a suitable destination for the caravan
                        Point3D destination = FindCaravanDestination(from.Location, from.Map);
                        if (destination == Point3D.Zero)
                        {
                            from.SendMessage("Could not find a suitable destination for the caravan from this location.");
                            return;
                        }
                        
                        scene = new Server.Engines.Scenes.MerchantCaravanScene(from.Location, destination, from.Map);
                        break;
                        
                    default:
                        from.SendMessage("Unknown scene type '{0}'. Available types: War, Caravan", sceneType);
                        return;
                }
                
                if (scene != null)
                {
                    PlayerBotDirector.Instance.AddScene(scene);
                    from.SendMessage(0x40, "Created {0} scene at {1}.", sceneType, from.Location);
                    from.SendMessage("Scene ID: {0}", scene.SceneId);
                }
            }
            catch (Exception ex)
            {
                from.SendMessage(0x25, "Error creating scene: {0}", ex.Message);
                Console.WriteLine("[PlayerBotCommands] Error creating scene: {0}", ex);
            }
        }

        [Usage("ListScenes")]
        [Description("Lists all active PlayerBot scenes.")]
        public static void ListScenes_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            List<PlayerBotScene> activeScenes = PlayerBotDirector.Instance.GetActiveScenes();
            
            if (activeScenes.Count == 0)
            {
                from.SendMessage("No active scenes.");
                return;
            }
            
            from.SendMessage(0x35, "=== Active Scenes ({0}) ===", activeScenes.Count);
            
            foreach (PlayerBotScene scene in activeScenes)
            {
                string sceneType = scene.GetType().Name.Replace("Scene", "");
                string state = scene.CurrentState.ToString();
                int participants = scene.GetParticipantCount();
                
                from.SendMessage("Scene {0}: {1} ({2})", scene.SceneId, sceneType, state);
                from.SendMessage("  Location: {0} ({1})", scene.CenterLocation, scene.Map);
                from.SendMessage("  Participants: {0}", participants);
                from.SendMessage("  Duration: {0:F1}s", (DateTime.Now - scene.StartTime).TotalSeconds);
                
                // Scene-specific information
                if (scene is Server.Engines.Scenes.WarScene)
                {
                    Server.Engines.Scenes.WarScene warScene = scene as Server.Engines.Scenes.WarScene;
                    from.SendMessage("  War Type: {0}", warScene.WarType);
                }
                else if (scene is Server.Engines.Scenes.MerchantCaravanScene)
                {
                    Server.Engines.Scenes.MerchantCaravanScene caravanScene = scene as Server.Engines.Scenes.MerchantCaravanScene;
                    from.SendMessage("  Destination: {0}", caravanScene.Destination);
                    from.SendMessage("  Progress: {0:F1}%", caravanScene.GetProgress() * 100);
                }
                
                from.SendMessage("");
            }
        }

        [Usage("EndScene <sceneId>")]
        [Description("Forcibly ends a scene by its ID. Use 'all' to end all scenes.")]
        public static void EndScene_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            if (e.Arguments.Length < 1)
            {
                from.SendMessage("Usage: [EndScene <sceneId>");
                from.SendMessage("Use [ListScenes to see active scene IDs, or 'all' to end all scenes.");
                return;
            }
            
            string sceneIdStr = e.Arguments[0].ToLower();
            
            if (sceneIdStr == "all")
            {
                List<PlayerBotScene> activeScenes = PlayerBotDirector.Instance.GetActiveScenes();
                int endedCount = 0;
                
                foreach (PlayerBotScene scene in activeScenes)
                {
                    try
                    {
                        scene.ForceEnd();
                        endedCount++;
                    }
                    catch (Exception ex)
                    {
                        from.SendMessage(0x25, "Error ending scene {0}: {1}", scene.SceneId, ex.Message);
                    }
                }
                
                from.SendMessage(0x40, "Ended {0} scene(s).", endedCount);
                return;
            }
            
            int sceneId;
            if (!int.TryParse(sceneIdStr, out sceneId))
            {
                from.SendMessage("Invalid scene ID. Must be a number or 'all'.");
                return;
            }
            
            PlayerBotScene targetScene = PlayerBotDirector.Instance.GetSceneById(sceneId);
            if (targetScene == null)
            {
                from.SendMessage("Scene with ID {0} not found.", sceneId);
                return;
            }
            
            try
            {
                string sceneType = targetScene.GetType().Name.Replace("Scene", "");
                targetScene.ForceEnd();
                from.SendMessage(0x40, "Ended {0} scene {1}.", sceneType, sceneId);
            }
            catch (Exception ex)
            {
                from.SendMessage(0x25, "Error ending scene: {0}", ex.Message);
                Console.WriteLine("[PlayerBotCommands] Error ending scene: {0}", ex);
            }
        }

        [Usage("SceneInfo <sceneId>")]
        [Description("Displays detailed information about a specific scene.")]
        public static void SceneInfo_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            if (e.Arguments.Length < 1)
            {
                from.SendMessage("Usage: [SceneInfo <sceneId>");
                from.SendMessage("Use [ListScenes to see active scene IDs.");
                return;
            }
            
            int sceneId;
            if (!int.TryParse(e.Arguments[0], out sceneId))
            {
                from.SendMessage("Invalid scene ID. Must be a number.");
                return;
            }
            
            PlayerBotScene scene = PlayerBotDirector.Instance.GetSceneById(sceneId);
            if (scene == null)
            {
                from.SendMessage("Scene with ID {0} not found.", sceneId);
                return;
            }
            
            string sceneType = scene.GetType().Name.Replace("Scene", "");
            
            from.SendMessage(0x35, "=== Scene {0} Information ===", sceneId);
            from.SendMessage("Type: {0}", sceneType);
            from.SendMessage("State: {0}", scene.CurrentState);
            from.SendMessage("Location: {0} ({1})", scene.CenterLocation, scene.Map);
            from.SendMessage("Start Time: {0:yyyy-MM-dd HH:mm:ss}", scene.StartTime);
            from.SendMessage("Duration: {0:F1}s", (DateTime.Now - scene.StartTime).TotalSeconds);
            from.SendMessage("Participants: {0}", scene.GetParticipantCount());
            
            // List participants
            List<PlayerBot> participants = scene.GetParticipants();
            if (participants.Count > 0)
            {
                from.SendMessage("Participant List:");
                foreach (PlayerBot bot in participants)
                {
                    if (bot != null && !bot.Deleted)
                    {
                        string status = bot.Alive ? "Alive" : "Dead";
                        string combatStatus = bot.Combatant != null ? " (Fighting)" : "";
                        from.SendMessage("  - {0} ({1}){2}", bot.Name, status, combatStatus);
                    }
                }
            }
            
            // Scene-specific details
            if (scene is Server.Engines.Scenes.WarScene)
            {
                Server.Engines.Scenes.WarScene warScene = scene as Server.Engines.Scenes.WarScene;
                from.SendMessage("=== War Scene Details ===");
                from.SendMessage("War Type: {0}", warScene.WarType);
                from.SendMessage("Faction A: {0} members", warScene.GetFactionACount());
                from.SendMessage("Faction B: {0} members", warScene.GetFactionBCount());
            }
            else if (scene is Server.Engines.Scenes.MerchantCaravanScene)
            {
                Server.Engines.Scenes.MerchantCaravanScene caravanScene = scene as Server.Engines.Scenes.MerchantCaravanScene;
                from.SendMessage("=== Caravan Scene Details ===");
                from.SendMessage("Start Location: {0}", caravanScene.CenterLocation);
                from.SendMessage("Destination: {0}", caravanScene.Destination);
                from.SendMessage("Progress: {0:F1}%", caravanScene.GetProgress() * 100);
                from.SendMessage("Merchants: {0}", caravanScene.GetMerchantCount());
                from.SendMessage("Guards: {0}", caravanScene.GetGuardCount());
            }
        }

        private static Point3D FindCaravanDestination(Point3D start, Map map)
        {
            // Try to find a suitable destination from the available regions
            List<PlayerBotConfigurationManager.RegionConfig> possibleDestinations = new List<PlayerBotConfigurationManager.RegionConfig>();
            
            foreach (PlayerBotConfigurationManager.RegionConfig region in PlayerBotConfigurationManager.Regions.Values)
            {
                if (region.Active && region.Map == map)
                {
                    // Calculate distance
                    Point3D regionCenter = new Point3D(
                        (region.Bounds.X + region.Bounds.Width / 2),
                        (region.Bounds.Y + region.Bounds.Height / 2),
                        0);
                    
                    double distance = Math.Sqrt(Math.Pow(start.X - regionCenter.X, 2) + Math.Pow(start.Y - regionCenter.Y, 2));
                    
                    // Only consider destinations that are reasonably far away (at least 50 tiles)
                    if (distance >= 50 && distance <= 200)
                    {
                        possibleDestinations.Add(region);
                    }
                }
            }
            
            if (possibleDestinations.Count > 0)
            {
                PlayerBotConfigurationManager.RegionConfig chosen = possibleDestinations[Utility.Random(possibleDestinations.Count)];
                return new Point3D(
                    chosen.Bounds.X + chosen.Bounds.Width / 2,
                    chosen.Bounds.Y + chosen.Bounds.Height / 2,
                    0);
            }
            
            // Fallback: create a destination some distance away
            int angle = Utility.Random(360);
            int fallbackDistance = Utility.RandomMinMax(75, 150);
            
            int x = start.X + (int)(Math.Cos(angle * Math.PI / 180) * fallbackDistance);
            int y = start.Y + (int)(Math.Sin(angle * Math.PI / 180) * fallbackDistance);
            
            return new Point3D(x, y, start.Z);
        }

        [Usage("[BotSceneDebug")]
        [Description("Debug scene creation issues - shows detailed information about why scenes might not trigger")]
        public static void BotSceneDebug_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            from.SendMessage("=== PlayerBot Scene Debug ===");
            from.SendMessage("Location: {0} on {1}", from.Location, from.Map);
            
            // Get current region
            System.Reflection.FieldInfo regionsField = PlayerBotDirector.Instance.GetType()
                .GetField("m_Regions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            System.Collections.Generic.List<PlayerBotDirector.RegionProfile> regions = null;
            if (regionsField != null)
            {
                regions = regionsField.GetValue(PlayerBotDirector.Instance) as System.Collections.Generic.List<PlayerBotDirector.RegionProfile>;
            }
                
            if (regions == null)
            {
                from.SendMessage("Could not access region data.");
                return;
            }
            
            PlayerBotDirector.RegionProfile currentRegion = null;
            foreach (PlayerBotDirector.RegionProfile region in regions)
            {
                if (region.Area.Contains(from.Location))
                {
                    currentRegion = region;
                    break;
                }
            }
            
            if (currentRegion != null)
            {
                from.SendMessage("Current Region: {0}", currentRegion.Name);
                from.SendMessage("  Safety Level: {0}", currentRegion.SafetyLevel);
                from.SendMessage("  Area: {0}", currentRegion.Area);
                from.SendMessage("  Size: {0}x{1}", currentRegion.Area.Width, currentRegion.Area.Height);
            }
            else
            {
                from.SendMessage("Not in a configured PlayerBot region.");
                from.SendMessage("Checking nearby regions...");
                
                // Find nearest regions
                int nearestDistance = int.MaxValue;
                PlayerBotDirector.RegionProfile nearestRegion = null;
                
                foreach (PlayerBotDirector.RegionProfile region in regions)
                {
                    int distance = GetDistanceToRegion(from.Location, region.Area);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestRegion = region;
                    }
                }
                
                if (nearestRegion != null)
                {
                    from.SendMessage("Nearest region: {0} (distance: {1})", nearestRegion.Name, nearestDistance);
                }
            }
            
            // Check war scene requirements
            from.SendMessage("");
            from.SendMessage("=== War Scene Requirements ===");
            if (currentRegion != null)
            {
                bool isDangerous = currentRegion.SafetyLevel != PlayerBotConfigurationManager.SafetyLevel.Safe;
                bool hasSpace = currentRegion.Area.Width >= 40 && currentRegion.Area.Height >= 40;
                
                from.SendMessage("Region Safety: {0} {1}", currentRegion.SafetyLevel, 
                    isDangerous ? "(GOOD - dangerous enough)" : "(BAD - too safe)");
                from.SendMessage("Region Size: {0}x{1} {2}", currentRegion.Area.Width, currentRegion.Area.Height,
                    hasSpace ? "(GOOD - large enough)" : "(BAD - too small, need 40x40)");
                
                if (isDangerous && hasSpace)
                {
                    from.SendMessage("✓ This region CAN support war scenes!");
                }
                else
                {
                    from.SendMessage("✗ This region CANNOT support war scenes.");
                }
            }
            else
            {
                from.SendMessage("Not in a region - war scenes require configured regions.");
            }
            
            // Check caravan scene requirements  
            from.SendMessage("");
            from.SendMessage("=== Caravan Scene Requirements ===");
            if (currentRegion != null)
            {
                bool hasSpace = currentRegion.Area.Width >= 50 && currentRegion.Area.Height >= 50;
                
                from.SendMessage("Region Size: {0}x{1} {2}", currentRegion.Area.Width, currentRegion.Area.Height,
                    hasSpace ? "(GOOD - large enough)" : "(BAD - too small, need 50x50)");
                
                if (hasSpace)
                {
                    from.SendMessage("✓ This region CAN support caravan scenes!");
                }
                else
                {
                    from.SendMessage("✗ This region CANNOT support caravan scenes.");
                }
            }
            else
            {
                from.SendMessage("Not in a region - caravan scenes require configured regions.");
            }
            
            // Show scene creation settings
            from.SendMessage("");
            from.SendMessage("=== Scene Creation Settings ===");
            from.SendMessage("Auto-scene creation: {0}", PlayerBotDirector.Instance.AutoSceneCreation ? "ENABLED" : "DISABLED");
            from.SendMessage("Dynamic event chance: {0}%", PlayerBotConfigurationManager.BehaviorSettings.DynamicEventChance);
            from.SendMessage("Scene check interval: 60 seconds");
            
            // Show active scenes
            System.Collections.Generic.List<PlayerBotScene> activeScenes = Server.Engines.PlayerBotDirector.Instance.GetActiveScenes();
            from.SendMessage("Active Scenes: {0}", activeScenes.Count);
            foreach (PlayerBotScene scene in activeScenes)
            {
                from.SendMessage("  Scene {0}: {1} at {2} (State: {3})", 
                    scene.SceneId, scene.GetType().Name.Replace("Scene", ""), scene.CenterLocation, scene.CurrentState);
            }
            
            // Show PlayerBot counts
            from.SendMessage("Registered PlayerBots: {0}", Server.Engines.PlayerBotDirector.Instance.GetRegisteredBotCount());
            from.SendMessage("World PlayerBots: {0}", Server.Engines.PlayerBotDirector.Instance.GetWorldPlayerBotCount());
        }

        [Usage("[ForceScene <war|caravan>")]
        [Description("Force create a scene at your location for testing (bypasses normal requirements)")]
        public static void ForceScene_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            if (e.Arguments.Length < 1)
            {
                from.SendMessage("Usage: [ForceScene <war|caravan>");
                return;
            }
            
            string sceneType = e.Arguments[0].ToLower();
            
            try
            {
                PlayerBotScene scene = null;
                
                if (sceneType == "war")
                {
                    from.SendMessage("Creating forced war scene at your location...");
                    from.SendMessage("Location: {0} on {1}", from.Location, from.Map);
                    
                    // Test spawn location first
                    for (int i = 0; i < 5; i++)
                    {
                        int x = from.X + Utility.RandomMinMax(-10, 10);
                        int y = from.Y + Utility.RandomMinMax(-10, 10);
                        int z = from.Map.GetAverageZ(x, y);
                        Point3D testLoc = new Point3D(x, y, z);
                        
                        bool canSpawn = from.Map.CanSpawnMobile(testLoc.X, testLoc.Y, testLoc.Z);
                        from.SendMessage("Test spawn {0}: {1} - CanSpawn: {2}", i + 1, testLoc, canSpawn);
                    }
                    
                    // Create war scene directly without normal checks
                    scene = new Server.Engines.Scenes.WarScene(from.Location, from.Map, 8);
                    
                    // Force initialize the scene to spawn bots
                    from.SendMessage("Initializing scene...");
                    scene.Initialize();
                    from.SendMessage("War scene initialized with {0} participants", scene.GetParticipantCount());
                    
                    // Debug participant details
                    System.Collections.Generic.List<PlayerBot> participants = scene.GetParticipants();
                    if (participants.Count > 0)
                    {
                        from.SendMessage("Participants spawned successfully:");
                        foreach (PlayerBot bot in participants)
                        {
                            if (bot != null && !bot.Deleted)
                            {
                                from.SendMessage("  - {0} at {1}", bot.Name, bot.Location);
                            }
                        }
                    }
                    else
                    {
                        from.SendMessage("WARNING: No participants were spawned! Checking spawn issues...");
                        
                        // Try manual spawn test
                        from.SendMessage("Testing manual PlayerBot spawn...");
                        PlayerBot testBot = new PlayerBot();
                        testBot.MoveToWorld(new Point3D(from.X + 2, from.Y + 2, from.Z), from.Map);
                        if (testBot.Location == Point3D.Zero)
                        {
                            from.SendMessage("Manual spawn failed - location issue");
                            testBot.Delete();
                        }
                        else
                        {
                            from.SendMessage("Manual spawn succeeded at {0}", testBot.Location);
                            from.SendMessage("Deleting test bot...");
                            testBot.Delete();
                        }
                    }
                }
                else if (sceneType == "caravan")
                {
                    from.SendMessage("Creating forced caravan scene at your location...");
                    from.SendMessage("Location: {0} on {1}", from.Location, from.Map);
                    
                    // Create caravan scene with a destination 100 tiles away
                    Point3D destination = new Point3D(
                        from.X + Utility.RandomMinMax(-100, 100),
                        from.Y + Utility.RandomMinMax(-100, 100),
                        from.Z
                    );
                    
                    from.SendMessage("Destination: {0}", destination);
                    
                    scene = new Server.Engines.Scenes.MerchantCaravanScene(from.Location, destination, from.Map);
                    
                    // Force initialize the scene to spawn bots
                    from.SendMessage("Initializing scene...");
                    scene.Initialize();
                    from.SendMessage("Caravan scene initialized with {0} participants", scene.GetParticipantCount());
                    
                    // Debug participant details
                    System.Collections.Generic.List<PlayerBot> participants = scene.GetParticipants();
                    if (participants.Count > 0)
                    {
                        from.SendMessage("Participants spawned successfully:");
                        foreach (PlayerBot bot in participants)
                        {
                            if (bot != null && !bot.Deleted)
                            {
                                from.SendMessage("  - {0} at {1}", bot.Name, bot.Location);
                            }
                        }
                    }
                    else
                    {
                        from.SendMessage("WARNING: No participants were spawned!");
                    }
                }
                else
                {
                    from.SendMessage("Invalid scene type. Use 'war' or 'caravan'");
                    return;
                }
                
                if (scene != null)
                {
                    // Add the scene directly to the director
                    Server.Engines.PlayerBotDirector.Instance.AddScene(scene);
                    from.SendMessage("Scene {0} created successfully! Scene ID: {1}", sceneType, scene.SceneId);
                    from.SendMessage("Use [ListScenes to view active scenes.");
                    from.SendMessage("Use [SceneStatus for detailed scene information.");
                }
                else
                {
                    from.SendMessage("Failed to create scene.");
                }
            }
            catch (Exception ex)
            {
                from.SendMessage("Error creating scene: {0}", ex.Message);
                from.SendMessage("Stack trace: {0}", ex.StackTrace);
                Console.WriteLine("[ForceScene] Error: {0}", ex);
            }
        }

        [Usage("[SceneStatus")]
        [Description("Shows detailed status of all active scenes")]
        public static void SceneStatus_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            System.Collections.Generic.List<PlayerBotScene> activeScenes = Server.Engines.PlayerBotDirector.Instance.GetActiveScenes();
            
            from.SendMessage("=== Active Scene Status ===");
            from.SendMessage("Total active scenes: {0}", activeScenes.Count);
            
            if (activeScenes.Count == 0)
            {
                from.SendMessage("No active scenes found.");
                return;
            }
            
            foreach (PlayerBotScene scene in activeScenes)
            {
                from.SendMessage("--- Scene {0} ---", scene.SceneId);
                from.SendMessage("Type: {0}", scene.GetType().Name.Replace("Scene", ""));
                from.SendMessage("State: {0}", scene.CurrentState);
                from.SendMessage("Location: {0} on {1}", scene.CenterLocation, scene.Map);
                from.SendMessage("Participants: {0}", scene.GetParticipantCount());
                
                if (scene.GetParticipantCount() > 0)
                {
                    System.Collections.Generic.List<PlayerBot> participants = scene.GetParticipants();
                    from.SendMessage("Participant details:");
                    foreach (PlayerBot bot in participants)
                    {
                        if (bot != null && !bot.Deleted)
                        {
                            from.SendMessage("  - {0} at {1} (Alive: {2})", bot.Name, bot.Location, bot.Alive);
                        }
                        else
                        {
                            from.SendMessage("  - [DELETED BOT]");
                        }
                    }
                }
                else
                {
                    from.SendMessage("No participants found - this may indicate a spawning issue.");
                }
                
                from.SendMessage("Duration: {0:F1} seconds", (DateTime.Now - scene.StartTime).TotalSeconds);
                from.SendMessage("");
            }
        }

        [Usage("[WarSceneTest")]
        [Description("Test which regions can trigger war scenes and show scene creation chances")]
        public static void WarSceneTest_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            from.SendMessage("=== War Scene Testing ===");
            from.SendMessage("Current DynamicEventChance: {0}%", PlayerBotConfigurationManager.BehaviorSettings.DynamicEventChance);
            from.SendMessage("Auto-scene creation: {0}", PlayerBotDirector.Instance.AutoSceneCreation ? "ENABLED" : "DISABLED");
            from.SendMessage("");
            
            System.Reflection.FieldInfo regionsField = PlayerBotDirector.Instance.GetType()
                .GetField("m_Regions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            System.Collections.Generic.List<PlayerBotDirector.RegionProfile> regions = null;
            if (regionsField != null)
            {
                regions = regionsField.GetValue(PlayerBotDirector.Instance) as System.Collections.Generic.List<PlayerBotDirector.RegionProfile>;
            }
                
            if (regions == null)
            {
                from.SendMessage("Could not access region data.");
                return;
            }
            
            from.SendMessage("Checking {0} regions for war scene eligibility:", regions.Count);
            from.SendMessage("");
            
            int eligibleRegions = 0;
            
            foreach (PlayerBotDirector.RegionProfile region in regions)
            {
                // Check if region is dangerous
                bool isDangerous = region.SafetyLevel != PlayerBotConfigurationManager.SafetyLevel.Safe;
                
                // Check if region has players
                System.Reflection.MethodInfo getPlayersMethod = PlayerBotDirector.Instance.GetType()
                    .GetMethod("GetPlayersInRegion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                System.Collections.Generic.List<Mobile> nearbyPlayers = null;
                if (getPlayersMethod != null)
                {
                    nearbyPlayers = getPlayersMethod.Invoke(PlayerBotDirector.Instance, new object[] { region }) as System.Collections.Generic.List<Mobile>;
                }
                bool hasPlayers = nearbyPlayers != null && nearbyPlayers.Count > 0;
                
                // Check size requirements
                bool hasSpace = region.Area.Width >= 40 && region.Area.Height >= 40;
                
                bool canTriggerWar = isDangerous && hasPlayers && hasSpace;
                
                if (canTriggerWar) eligibleRegions++;
                
                string status = canTriggerWar ? "ELIGIBLE" : "not eligible";
                from.SendMessage("{0}: {1}", region.Name, status);
                from.SendMessage("  Safety: {0} | Players: {1} | Size: {2}x{3}", 
                    region.SafetyLevel, hasPlayers ? nearbyPlayers.Count : 0, region.Area.Width, region.Area.Height);
                    
                if (!isDangerous) from.SendMessage("  - Too safe for war scenes");
                if (!hasPlayers) from.SendMessage("  - No players in region");
                if (!hasSpace) from.SendMessage("  - Too small (need 40x40 minimum)");
                from.SendMessage("");
            }
            
            from.SendMessage("=== Summary ===");
            from.SendMessage("Eligible regions for war scenes: {0}/{1}", eligibleRegions, regions.Count);
            
            if (eligibleRegions == 0)
            {
                from.SendMessage("No regions can trigger war scenes right now!");
                from.SendMessage("To test war scenes:");
                from.SendMessage("1. Go to a dangerous region (Wilderness/Dangerous)");
                from.SendMessage("2. Make sure the region is at least 40x40 tiles");
                from.SendMessage("3. Stay in the region during scene ticks (every 60s)");
            }
            else
            {
                double chancePerTick = PlayerBotConfigurationManager.BehaviorSettings.DynamicEventChance / 100.0;
                double chancePerRegion = chancePerTick / regions.Count;
                double warChancePerTick = chancePerRegion * eligibleRegions * 0.5; // Assume 50% chance war vs caravan
                
                from.SendMessage("Estimated war scene chance per tick: {0:F2}%", warChancePerTick * 100);
                from.SendMessage("Average time between war scenes: {0:F1} minutes", 
                    warChancePerTick > 0 ? (1.0 / warChancePerTick) : 0);
            }
        }

        [Usage("[FixCaravanKarma")]
        [Description("Fix karma issues in active caravan scenes")]
        public static void FixCaravanKarma_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            System.Collections.Generic.List<PlayerBotScene> activeScenes = PlayerBotDirector.Instance.GetActiveScenes();
            int fixedBots = 0;
            
            foreach (PlayerBotScene scene in activeScenes)
            {
                if (scene is Server.Engines.Scenes.MerchantCaravanScene)
                {
                    System.Collections.Generic.List<PlayerBot> participants = scene.GetParticipants();
                    foreach (PlayerBot bot in participants)
                    {
                        if (bot != null && !bot.Deleted)
                        {
                            // Merchants and guards should both be good-aligned
                            if (bot.Name.Contains("Merchant"))
                            {
                                bot.Karma = Utility.Random(50, 200); // Clearly good
                                bot.Say("*adjusts karma as an honest merchant*");
                            }
                            else if (bot.Name.Contains("Guard"))
                            {
                                bot.Karma = Utility.Random(100, 300); // Very good (lawful)
                                bot.Say("*straightens up as a lawful guard*");
                            }
                            fixedBots++;
                        }
                    }
                }
            }
            
            from.SendMessage("Fixed karma for {0} caravan participants.", fixedBots);
            if (fixedBots > 0)
            {
                from.SendMessage("Caravan members should now be properly aligned and not attack each other.");
            }
        }

        [Usage("[TestOrganicScenes")]
        [Description("Test organic scene creation by simulating the director's scene selection process")]
        public static void TestOrganicScenes_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            from.SendMessage("=== Organic Scene Creation Test ===");
            from.SendMessage("Simulating PlayerBotDirector scene creation process...");
            from.SendMessage("");
            
            PlayerBotDirector director = PlayerBotDirector.Instance;
            
            // Check if auto-scene creation is enabled
            if (!director.AutoSceneCreation)
            {
                from.SendMessage("ERROR: Auto-scene creation is DISABLED!");
                from.SendMessage("Use [BotStatus gump to enable it, or use:");
                from.SendMessage("  director.AutoSceneCreation = true");
                return;
            }
            
            from.SendMessage("Auto-scene creation: ENABLED");
            from.SendMessage("Dynamic event chance: {0}%", PlayerBotConfigurationManager.BehaviorSettings.DynamicEventChance);
            from.SendMessage("");
            
            // Get regions using reflection
            System.Reflection.FieldInfo regionsField = director.GetType()
                .GetField("m_Regions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            System.Collections.Generic.List<PlayerBotDirector.RegionProfile> regions = null;
            if (regionsField != null)
            {
                regions = regionsField.GetValue(director) as System.Collections.Generic.List<PlayerBotDirector.RegionProfile>;
            }
            
            if (regions == null)
            {
                from.SendMessage("ERROR: Could not access region data");
                return;
            }
            
            from.SendMessage("Testing {0} configured regions:", regions.Count);
            from.SendMessage("");
            
            int warEligible = 0;
            int caravanEligible = 0;
            
            foreach (PlayerBotDirector.RegionProfile region in regions)
            {
                // Get players in region using reflection
                System.Reflection.MethodInfo getPlayersMethod = director.GetType()
                    .GetMethod("GetPlayersInRegion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                System.Collections.Generic.List<Mobile> playersInRegion = null;
                if (getPlayersMethod != null)
                {
                    playersInRegion = getPlayersMethod.Invoke(director, new object[] { region }) as System.Collections.Generic.List<Mobile>;
                }
                
                if (playersInRegion == null)
                    playersInRegion = new System.Collections.Generic.List<Mobile>();
                
                // Test war scene eligibility
                bool warCanTrigger = false;
                try
                {
                    Point3D regionCenter = new Point3D(region.Area.X + region.Area.Width / 2, region.Area.Y + region.Area.Height / 2, 0);
                    Server.Engines.Scenes.WarScene testWar = new Server.Engines.Scenes.WarScene(regionCenter, region.Map);
                    warCanTrigger = testWar.CanTrigger(region, playersInRegion);
                    testWar = null; // Don't actually create it
                }
                catch (Exception ex)
                {
                    from.SendMessage("Error testing war scene for {0}: {1}", region.Name, ex.Message);
                }
                
                // Test caravan scene eligibility  
                bool caravanCanTrigger = false;
                try
                {
                    Point3D regionStart = new Point3D(region.Area.X, region.Area.Y, 0);
                    Point3D destination = new Point3D(
                        region.Area.X + region.Area.Width + 100,
                        region.Area.Y + region.Area.Height + 100,
                        0
                    );
                    Server.Engines.Scenes.MerchantCaravanScene testCaravan = new Server.Engines.Scenes.MerchantCaravanScene(regionStart, destination, region.Map);
                    caravanCanTrigger = testCaravan.CanTrigger(region, playersInRegion);
                    testCaravan = null; // Don't actually create it
                }
                catch (Exception ex)
                {
                    from.SendMessage("Error testing caravan scene for {0}: {1}", region.Name, ex.Message);
                }
                
                if (warCanTrigger) warEligible++;
                if (caravanCanTrigger) caravanEligible++;
                
                string warStatus = warCanTrigger ? "YES" : "no";
                string caravanStatus = caravanCanTrigger ? "YES" : "no";
                
                from.SendMessage("{0}:", region.Name);
                from.SendMessage("  Safety: {0} | Players: {1} | Size: {2}x{3}", 
                    region.SafetyLevel, playersInRegion.Count, region.Area.Width, region.Area.Height);
                from.SendMessage("  War: {0} | Caravan: {1}", warStatus, caravanStatus);
                
                // Explain why war scenes can't trigger
                if (!warCanTrigger)
                {
                    if (region.SafetyLevel == PlayerBotConfigurationManager.SafetyLevel.Safe)
                        from.SendMessage("    War blocked: Region too safe");
                    if (playersInRegion.Count == 0)
                        from.SendMessage("    War blocked: No players in region");
                    if (region.Area.Width < 40 || region.Area.Height < 40)
                        from.SendMessage("    War blocked: Area too small (need 40x40)");
                }
                
                from.SendMessage("");
            }
            
            from.SendMessage("=== Summary ===");
            from.SendMessage("Regions eligible for war scenes: {0}/{1}", warEligible, regions.Count);
            from.SendMessage("Regions eligible for caravan scenes: {0}/{1}", caravanEligible, regions.Count);
            from.SendMessage("");
            
            if (warEligible == 0)
            {
                from.SendMessage("NO REGIONS can trigger war scenes organically!");
                from.SendMessage("This is why you're not seeing organic wars.");
                from.SendMessage("");
                from.SendMessage("Solutions:");
                from.SendMessage("1. Go to a dangerous region (Wilderness/Dangerous)");
                from.SendMessage("2. Stay in that region during scene ticks (every 60s)");
                from.SendMessage("3. Make sure the region is at least 40x40 tiles");
                from.SendMessage("");
                from.SendMessage("Recommended test locations:");
                from.SendMessage("- Buccaneer's Den (dangerous island)");
                from.SendMessage("- Despise dungeon entrance");
                from.SendMessage("- Destard dungeon entrance");
                from.SendMessage("- Covetous dungeon entrance");
            }
            else
            {
                double chancePerTick = PlayerBotConfigurationManager.BehaviorSettings.DynamicEventChance / 100.0;
                double warChancePerTick = (chancePerTick / regions.Count) * warEligible * 0.5; // Assume 50% war vs caravan split
                
                from.SendMessage("Estimated organic war chance per tick: {0:F2}%", warChancePerTick * 100);
                if (warChancePerTick > 0)
                {
                    double avgMinutes = (1.0 / warChancePerTick) / 60.0; // Convert ticks to minutes
                    from.SendMessage("Average time between organic wars: {0:F1} minutes", avgMinutes);
                }
            }
            
            from.SendMessage("");
            from.SendMessage("Next scene tick in: ~{0} seconds", 60 - (DateTime.Now.Second % 60));
        }

        [Usage("[WarEligibilityBreakdown")]
        [Description("Shows detailed breakdown of why regions can't trigger war scenes")]
        public static void WarEligibilityBreakdown_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            from.SendMessage("=== War Scene Eligibility Breakdown ===");
            from.SendMessage("");
            
            PlayerBotDirector director = PlayerBotDirector.Instance;
            
            // Get regions using reflection
            System.Reflection.FieldInfo regionsField = director.GetType()
                .GetField("m_Regions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            System.Collections.Generic.List<PlayerBotDirector.RegionProfile> regions = null;
            if (regionsField != null)
            {
                regions = regionsField.GetValue(director) as System.Collections.Generic.List<PlayerBotDirector.RegionProfile>;
            }
            
            if (regions == null)
            {
                from.SendMessage("ERROR: Could not access region data");
                return;
            }
            
            int totalRegions = regions.Count;
            int tooSafe = 0;
            int noPlayers = 0;
            int tooSmall = 0;
            int eligible = 0;
            
            from.SendMessage("Analyzing {0} regions...", totalRegions);
            from.SendMessage("");
            
            foreach (PlayerBotDirector.RegionProfile region in regions)
            {
                // Get players in region
                System.Reflection.MethodInfo getPlayersMethod = director.GetType()
                    .GetMethod("GetPlayersInRegion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                System.Collections.Generic.List<Mobile> playersInRegion = null;
                if (getPlayersMethod != null)
                {
                    playersInRegion = getPlayersMethod.Invoke(director, new object[] { region }) as System.Collections.Generic.List<Mobile>;
                }
                
                if (playersInRegion == null)
                    playersInRegion = new System.Collections.Generic.List<Mobile>();
                
                // Check each requirement
                bool isDangerous = region.SafetyLevel != PlayerBotConfigurationManager.SafetyLevel.Safe;
                bool hasPlayers = playersInRegion.Count > 0;
                bool hasSpace = region.Area.Width >= 40 && region.Area.Height >= 40;
                
                bool canTriggerWar = isDangerous && hasPlayers && hasSpace;
                
                if (canTriggerWar)
                {
                    eligible++;
                    from.SendMessage("✓ ELIGIBLE: {0}", region.Name);
                    from.SendMessage("    Safety: {0} | Players: {1} | Size: {2}x{3}", 
                        region.SafetyLevel, playersInRegion.Count, region.Area.Width, region.Area.Height);
                }
                else
                {
                    string reason = "";
                    if (!isDangerous)
                    {
                        tooSafe++;
                        reason += "TOO SAFE ";
                    }
                    if (!hasPlayers)
                    {
                        noPlayers++;
                        reason += "NO PLAYERS ";
                    }
                    if (!hasSpace)
                    {
                        tooSmall++;
                        reason += "TOO SMALL ";
                    }
                    
                    from.SendMessage("✗ {0}: {1}", region.Name, reason.Trim());
                    from.SendMessage("    Safety: {0} | Players: {1} | Size: {2}x{3}", 
                        region.SafetyLevel, playersInRegion.Count, region.Area.Width, region.Area.Height);
                }
                from.SendMessage("");
            }
            
            from.SendMessage("=== SUMMARY ===");
            from.SendMessage("Total regions: {0}", totalRegions);
            from.SendMessage("Eligible for war: {0} ({1:F1}%)", eligible, (double)eligible / totalRegions * 100);
            from.SendMessage("");
            from.SendMessage("Blocking factors:");
            from.SendMessage("  Too safe: {0} regions", tooSafe);
            from.SendMessage("  No players: {0} regions", noPlayers);
            from.SendMessage("  Too small: {0} regions", tooSmall);
            from.SendMessage("");
            
            if (noPlayers > 0)
            {
                from.SendMessage("*** MAIN ISSUE: {0} regions have no players! ***", noPlayers);
                from.SendMessage("War scenes require players to be present during scene ticks.");
                from.SendMessage("This is why organic wars are so rare.");
                from.SendMessage("");
                from.SendMessage("SOLUTIONS:");
                from.SendMessage("1. Stay in dangerous regions longer");
                from.SendMessage("2. Use [ForceScene war for manual testing");
                from.SendMessage("3. Consider modifying war scene requirements");
            }
        }

        [Usage("[TestWarChanges")]
        [Description("Test the updated war scene requirements (should no longer require players)")]
        public static void TestWarChanges_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            from.SendMessage("=== Testing Updated War Scene Requirements ===");
            from.SendMessage("War scenes should now work like caravans (no player requirement)");
            from.SendMessage("");
            
            PlayerBotDirector director = PlayerBotDirector.Instance;
            
            // Get regions using reflection
            System.Reflection.FieldInfo regionsField = director.GetType()
                .GetField("m_Regions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            System.Collections.Generic.List<PlayerBotDirector.RegionProfile> regions = null;
            if (regionsField != null)
            {
                regions = regionsField.GetValue(director) as System.Collections.Generic.List<PlayerBotDirector.RegionProfile>;
            }
            
            if (regions == null)
            {
                from.SendMessage("ERROR: Could not access region data");
                return;
            }
            
            int warEligible = 0;
            int caravanEligible = 0;
            int dangerousRegions = 0;
            
            foreach (PlayerBotDirector.RegionProfile region in regions)
            {
                if (region.SafetyLevel != PlayerBotConfigurationManager.SafetyLevel.Safe)
                    dangerousRegions++;
                
                // Test with empty player list (simulating no players present)
                System.Collections.Generic.List<Mobile> emptyPlayerList = new System.Collections.Generic.List<Mobile>();
                
                // Test war scene eligibility
                bool warCanTrigger = false;
                try
                {
                    Point3D regionCenter = new Point3D(region.Area.X + region.Area.Width / 2, region.Area.Y + region.Area.Height / 2, 0);
                    Server.Engines.Scenes.WarScene testWar = new Server.Engines.Scenes.WarScene(regionCenter, region.Map);
                    warCanTrigger = testWar.CanTrigger(region, emptyPlayerList);
                    testWar = null;
                }
                catch (Exception ex)
                {
                    from.SendMessage("Error testing war scene for {0}: {1}", region.Name, ex.Message);
                }
                
                // Test caravan scene eligibility  
                bool caravanCanTrigger = false;
                try
                {
                    Point3D regionStart = new Point3D(region.Area.X, region.Area.Y, 0);
                    Point3D destination = new Point3D(
                        region.Area.X + region.Area.Width + 100,
                        region.Area.Y + region.Area.Height + 100,
                        0
                    );
                    Server.Engines.Scenes.MerchantCaravanScene testCaravan = new Server.Engines.Scenes.MerchantCaravanScene(regionStart, destination, region.Map);
                    caravanCanTrigger = testCaravan.CanTrigger(region, emptyPlayerList);
                    testCaravan = null;
                }
                catch (Exception ex)
                {
                    from.SendMessage("Error testing caravan scene for {0}: {1}", region.Name, ex.Message);
                }
                
                if (warCanTrigger) warEligible++;
                if (caravanCanTrigger) caravanEligible++;
            }
            
            from.SendMessage("=== RESULTS ===");
            from.SendMessage("Total regions: {0}", regions.Count);
            from.SendMessage("Dangerous regions: {0}", dangerousRegions);
            from.SendMessage("War-eligible regions: {0}", warEligible);
            from.SendMessage("Caravan-eligible regions: {0}", caravanEligible);
            from.SendMessage("");
            
            if (warEligible > 1)
            {
                from.SendMessage("✓ SUCCESS! War scenes can now trigger without players!");
                from.SendMessage("Expected war scenes should be much more common now.");
                
                double chancePerTick = PlayerBotConfigurationManager.BehaviorSettings.DynamicEventChance / 100.0;
                double warChancePerTick = (chancePerTick / regions.Count) * warEligible * 0.5; // Assume 50% war vs caravan split
                
                from.SendMessage("Estimated organic war chance per tick: {0:F2}%", warChancePerTick * 100);
                if (warChancePerTick > 0)
                {
                    double avgMinutes = (1.0 / warChancePerTick) / 60.0;
                    from.SendMessage("Average time between organic wars: {0:F1} minutes", avgMinutes);
                }
            }
            else
            {
                from.SendMessage("✗ Issue: War scenes still have restrictive requirements");
                from.SendMessage("Only {0} regions are eligible for wars", warEligible);
            }
            
            from.SendMessage("");
            from.SendMessage("With DynamicEventChance at {0}%, you should see organic wars soon!", 
                PlayerBotConfigurationManager.BehaviorSettings.DynamicEventChance);
        }

        [Usage("[DebugSceneCreation")]
        [Description("Debug the actual scene creation process to see why wars aren't triggering")]
        public static void DebugSceneCreation_OnCommand(CommandEventArgs e)
        {
            Mobile from = e.Mobile;
            
            from.SendMessage("=== Scene Creation Debug ===");
            from.SendMessage("Simulating the actual PlayerBotDirector scene creation logic...");
            from.SendMessage("");
            
            PlayerBotDirector director = PlayerBotDirector.Instance;
            
            // Check if auto-scene creation is enabled
            if (!director.AutoSceneCreation)
            {
                from.SendMessage("ERROR: Auto-scene creation is DISABLED!");
                return;
            }
            
            from.SendMessage("Auto-scene creation: ENABLED");
            from.SendMessage("DynamicEventChance: {0}%", PlayerBotConfigurationManager.BehaviorSettings.DynamicEventChance);
            from.SendMessage("");
            
            // Get regions using reflection
            System.Reflection.FieldInfo regionsField = director.GetType()
                .GetField("m_Regions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            System.Collections.Generic.List<PlayerBotDirector.RegionProfile> regions = null;
            if (regionsField != null)
            {
                regions = regionsField.GetValue(director) as System.Collections.Generic.List<PlayerBotDirector.RegionProfile>;
            }
            
            if (regions == null)
            {
                from.SendMessage("ERROR: Could not access region data");
                return;
            }
            
            from.SendMessage("Testing scene creation for {0} regions...", regions.Count);
            from.SendMessage("");
            
            int warEligible = 0;
            int caravanEligible = 0;
            System.Collections.Generic.List<string> warEligibleRegions = new System.Collections.Generic.List<string>();
            System.Collections.Generic.List<string> caravanEligibleRegions = new System.Collections.Generic.List<string>();
            
            foreach (PlayerBotDirector.RegionProfile region in regions)
            {
                // Get players in region using reflection
                System.Reflection.MethodInfo getPlayersMethod = director.GetType()
                    .GetMethod("GetPlayersInRegion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                System.Collections.Generic.List<Mobile> playersInRegion = null;
                if (getPlayersMethod != null)
                {
                    playersInRegion = getPlayersMethod.Invoke(director, new object[] { region }) as System.Collections.Generic.List<Mobile>;
                }
                
                if (playersInRegion == null)
                    playersInRegion = new System.Collections.Generic.List<Mobile>();
                
                // Test war scene eligibility exactly like the director would
                bool warCanTrigger = false;
                string warFailReason = "";
                
                try
                {
                    Point3D regionCenter = new Point3D(region.Area.X + region.Area.Width / 2, region.Area.Y + region.Area.Height / 2, 0);
                    Server.Engines.Scenes.WarScene testWar = new Server.Engines.Scenes.WarScene(regionCenter, region.Map);
                    warCanTrigger = testWar.CanTrigger(region, playersInRegion);
                    
                    if (!warCanTrigger)
                    {
                        // Check each requirement manually
                        if (region.SafetyLevel == PlayerBotConfigurationManager.SafetyLevel.Safe)
                            warFailReason = "Too safe";
                        else if (region.Area.Width < 40 || region.Area.Height < 40)
                            warFailReason = "Too small";
                        else
                            warFailReason = "Unknown reason";
                    }
                    
                    testWar = null;
                }
                catch (Exception ex)
                {
                    warFailReason = "Exception: " + ex.Message;
                }
                
                // Test caravan scene eligibility
                bool caravanCanTrigger = false;
                string caravanFailReason = "";
                
                try
                {
                    Point3D regionStart = new Point3D(region.Area.X, region.Area.Y, 0);
                    Point3D destination = new Point3D(
                        region.Area.X + region.Area.Width + 100,
                        region.Area.Y + region.Area.Height + 100,
                        0
                    );
                    Server.Engines.Scenes.MerchantCaravanScene testCaravan = new Server.Engines.Scenes.MerchantCaravanScene(regionStart, destination, region.Map);
                    caravanCanTrigger = testCaravan.CanTrigger(region, playersInRegion);
                    
                    if (!caravanCanTrigger)
                    {
                        if (region.Area.Width < 50 || region.Area.Height < 50)
                            caravanFailReason = "Too small";
                        else
                            caravanFailReason = "Unknown reason";
                    }
                    
                    testCaravan = null;
                }
                catch (Exception ex)
                {
                    caravanFailReason = "Exception: " + ex.Message;
                }
                
                if (warCanTrigger)
                {
                    warEligible++;
                    warEligibleRegions.Add(region.Name);
                }
                
                if (caravanCanTrigger)
                {
                    caravanEligible++;
                    caravanEligibleRegions.Add(region.Name);
                }
                
                // Show detailed info for each region
                string warStatus = warCanTrigger ? "YES" : ("NO - " + warFailReason);
                string caravanStatus = caravanCanTrigger ? "YES" : ("NO - " + caravanFailReason);
                
                from.SendMessage("{0}: Safety={1}, Size={2}x{3}", 
                    region.Name, region.SafetyLevel, region.Area.Width, region.Area.Height);
                from.SendMessage("  War: {0} | Caravan: {1}", warStatus, caravanStatus);
            }
            
            from.SendMessage("");
            from.SendMessage("=== SUMMARY ===");
            from.SendMessage("War-eligible regions: {0}/{1}", warEligible, regions.Count);
            from.SendMessage("Caravan-eligible regions: {0}/{1}", caravanEligible, regions.Count);
            from.SendMessage("");
            
            if (warEligible > 0)
            {
                from.SendMessage("War-eligible regions:");
                foreach (string regionName in warEligibleRegions)
                {
                    from.SendMessage("  - {0}", regionName);
                }
                from.SendMessage("");
                
                // Calculate actual probability
                double eventChance = PlayerBotConfigurationManager.BehaviorSettings.DynamicEventChance / 100.0;
                double chancePerRegion = eventChance / regions.Count;
                double warChancePerTick = chancePerRegion * warEligible * 0.5; // Assume 50/50 war vs caravan
                
                from.SendMessage("Scene creation math:");
                from.SendMessage("  Event chance per tick: {0}%", eventChance * 100);
                from.SendMessage("  Chance per region: {0:F3}%", chancePerRegion * 100);
                from.SendMessage("  War chance per tick: {0:F3}%", warChancePerTick * 100);
                from.SendMessage("  Expected time between wars: {0:F1} minutes", warChancePerTick > 0 ? (1.0 / warChancePerTick) / 60.0 : 0);
                
                if (warChancePerTick < 0.01) // Less than 1% per tick
                {
                    from.SendMessage("");
                    from.SendMessage("WARNING: War chance is very low even with eligible regions!");
                    from.SendMessage("This might explain why you're not seeing wars.");
                    from.SendMessage("Consider increasing DynamicEventChance or checking scene selection logic.");
                }
            }
            else
            {
                from.SendMessage("NO REGIONS are eligible for war scenes!");
                from.SendMessage("This explains why you're not seeing any wars.");
                from.SendMessage("Check the failure reasons above.");
            }
            
            from.SendMessage("");
            from.SendMessage("Next scene tick in approximately: {0} seconds", 60 - DateTime.Now.Second);
        }

        private static int GetDistanceToRegion(Point3D location, Rectangle2D area)
        {
            int dx = 0;
            int dy = 0;
            
            if (location.X < area.X)
                dx = area.X - location.X;
            else if (location.X > area.X + area.Width)
                dx = location.X - (area.X + area.Width);
            
            if (location.Y < area.Y)
                dy = area.Y - location.Y;
            else if (location.Y > area.Y + area.Height)
                dy = location.Y - (area.Y + area.Height);
            
            return (int)Math.Sqrt(dx * dx + dy * dy);
        }
        
        #endregion
    }
} 