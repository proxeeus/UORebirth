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
    }
} 