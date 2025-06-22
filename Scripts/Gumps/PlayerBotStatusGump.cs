using System;
using System.Collections;
using System.Collections.Generic;
using Server;
using Server.Engines;
using Server.Mobiles;
using Server.Network;

namespace Server.Gumps
{
    public enum PlayerBotStatusPage
    {
        Overview,
        BotList,
        RegionDetails,
        Configuration,
        BotDetails
    }

    public class PlayerBotStatusGump : Gump
    {
        private Mobile m_From;
        private PlayerBotStatusPage m_PageType;
        private ArrayList m_List;
        private int m_ListPage;
        private object m_State;

        private const int LabelColor = 0x7FFF;
        private const int SelectedColor = 0x421F;
        private const int DisabledColor = 0x4210;

        private const int LabelColor32 = 0xFFFFFF;
        private const int SelectedColor32 = 0x8080FF;
        private const int DisabledColor32 = 0x808080;

        private const int LabelHue = 0x480;
        private const int GreenHue = 0x40;
        private const int RedHue = 0x20;
        private const int BlueHue = 0x35;
        private const int YellowHue = 0x36;

        public void AddPageButton(int x, int y, int buttonID, string text, PlayerBotStatusPage page, params PlayerBotStatusPage[] subPages)
        {
            bool isSelection = (m_PageType == page);

            for (int i = 0; !isSelection && i < subPages.Length; ++i)
                isSelection = (m_PageType == subPages[i]);

            AddSelectedButton(x, y, buttonID, text, isSelection);
        }

        public void AddSelectedButton(int x, int y, int buttonID, string text, bool isSelection)
        {
            AddButton(x, y - 1, isSelection ? 4006 : 4005, 4007, buttonID, GumpButtonType.Reply, 0);
            AddHtml(x + 35, y, 200, 20, Color(text, isSelection ? SelectedColor32 : LabelColor32), false, false);
        }

        public void AddButtonLabeled(int x, int y, int buttonID, string text)
        {
            AddButton(x, y - 1, 4005, 4007, buttonID, GumpButtonType.Reply, 0);
            AddHtml(x + 35, y, 240, 20, Color(text, LabelColor32), false, false);
        }

        public string Center(string text)
        {
            return String.Format("<CENTER>{0}</CENTER>", text);
        }

        public string Color(string text, int color)
        {
            return String.Format("<BASEFONT COLOR=#{0:X6}>{1}</BASEFONT>", color, text);
        }

        public void AddBlackAlpha(int x, int y, int width, int height)
        {
            AddImageTiled(x, y, width, height, 2624);
            AddAlphaRegion(x, y, width, height);
        }

        public int GetButtonID(int type, int index)
        {
            return 1 + (type * 1000) + index;
        }

        public static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalDays >= 1.0)
                return String.Format("{0:F1} days", ts.TotalDays);
            else if (ts.TotalHours >= 1.0)
                return String.Format("{0:F1} hours", ts.TotalHours);
            else if (ts.TotalMinutes >= 1.0)
                return String.Format("{0:F1} minutes", ts.TotalMinutes);
            else
                return String.Format("{0:F1} seconds", ts.TotalSeconds);
        }

        public PlayerBotStatusGump(Mobile from) : this(from, PlayerBotStatusPage.Overview, 0, null, null, null)
        {
        }

        public PlayerBotStatusGump(Mobile from, PlayerBotStatusPage pageType, int listPage, ArrayList list, string notice, object state) : base(50, 40)
        {
            from.CloseGump(typeof(PlayerBotStatusGump));

            m_From = from;
            m_PageType = pageType;
            m_ListPage = listPage;
            m_State = state;
            m_List = list;

            AddPage(0);

            // Main background - larger than AdminGump to accommodate more content
            AddBackground(0, 0, 620, 540, 5054);

            // Navigation panel
            AddBlackAlpha(10, 10, 180, 120);
            // Main content area
            AddBlackAlpha(200, 10, 410, 480);
            // Notice area
            AddBlackAlpha(10, 500, 600, 30);

            // Navigation buttons
            AddPageButton(10, 10, GetButtonID(0, 0), "OVERVIEW", PlayerBotStatusPage.Overview);
            AddPageButton(10, 30, GetButtonID(0, 1), "BOT LIST", PlayerBotStatusPage.BotList, PlayerBotStatusPage.BotDetails);
            AddPageButton(10, 50, GetButtonID(0, 2), "REGIONS", PlayerBotStatusPage.RegionDetails);
            AddPageButton(10, 70, GetButtonID(0, 3), "CONFIG", PlayerBotStatusPage.Configuration);
            AddPageButton(10, 90, GetButtonID(0, 4), "REFRESH", PlayerBotStatusPage.Overview);

            if (notice != null)
                AddHtml(12, 502, 596, 26, Color(notice, LabelColor32), false, false);

            switch (pageType)
            {
                case PlayerBotStatusPage.Overview:
                {
                    AddOverviewPage();
                    break;
                }
                case PlayerBotStatusPage.BotList:
                {
                    AddBotListPage();
                    break;
                }
                case PlayerBotStatusPage.RegionDetails:
                {
                    AddRegionDetailsPage();
                    break;
                }
                case PlayerBotStatusPage.Configuration:
                {
                    AddConfigurationPage();
                    break;
                }
                case PlayerBotStatusPage.BotDetails:
                {
                    AddBotDetailsPage();
                    break;
                }
            }
        }

        private void AddOverviewPage()
        {
            PlayerBotDirector director = PlayerBotDirector.Instance;
            PlayerBotConfigurationManager.BehaviorConfig config = PlayerBotConfigurationManager.BehaviorSettings;

            AddHtml(210, 20, 400, 20, Color(Center("PlayerBot System Overview"), LabelColor32), false, false);

            int y = 50;

            // Population Statistics
            AddHtml(210, y, 400, 20, Color(Center("Population Statistics"), SelectedColor32), false, false);
            y += 30;

            int totalBots = director.GetRegisteredBotCount();
            AddLabel(220, y, LabelHue, "Total Active Bots:");
            AddLabel(380, y, totalBots > 0 ? GreenHue : RedHue, totalBots.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Global Capacity:");
            AddLabel(380, y, LabelHue, config.GlobalCap.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Utilization:");
            double utilization = config.GlobalCap > 0 ? (double)totalBots / config.GlobalCap * 100.0 : 0.0;
            int utilizationHue = utilization > 90 ? RedHue : (utilization > 75 ? YellowHue : GreenHue);
            AddLabel(380, y, utilizationHue, String.Format("{0:F1}%", utilization));
            y += 30;

            // Persona Distribution
            Dictionary<PlayerBotPersona.PlayerBotProfile, int> personaCounts = GetPersonaDistribution();
            AddHtml(210, y, 400, 20, Color(Center("Persona Distribution"), SelectedColor32), false, false);
            y += 30;

            int adventurers = personaCounts.ContainsKey(PlayerBotPersona.PlayerBotProfile.Adventurer) ? personaCounts[PlayerBotPersona.PlayerBotProfile.Adventurer] : 0;
            int crafters = personaCounts.ContainsKey(PlayerBotPersona.PlayerBotProfile.Crafter) ? personaCounts[PlayerBotPersona.PlayerBotProfile.Crafter] : 0;
            int pks = personaCounts.ContainsKey(PlayerBotPersona.PlayerBotProfile.PlayerKiller) ? personaCounts[PlayerBotPersona.PlayerBotProfile.PlayerKiller] : 0;

            AddLabel(220, y, LabelHue, "Adventurers:");
            AddLabel(380, y, BlueHue, adventurers.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Crafters:");
            AddLabel(380, y, GreenHue, crafters.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Player Killers:");
            AddLabel(380, y, RedHue, pks.ToString());
            y += 30;

            // Region Summary
            AddHtml(210, y, 400, 20, Color(Center("Active Regions"), SelectedColor32), false, false);
            y += 30;

            Dictionary<string, int> regionCounts = GetBotsPerRegion();
            int activeRegions = 0;
            foreach (PlayerBotConfigurationManager.RegionConfig region in PlayerBotConfigurationManager.Regions.Values)
            {
                if (region.Active)
                {
                    activeRegions++;
                    int currentBots = regionCounts.ContainsKey(region.Name) ? regionCounts[region.Name] : 0;
                    int targetMin = region.MinBots;
                    int targetMax = region.MaxBots;

                    AddLabel(220, y, LabelHue, region.Name + ":");
                    
                    string botText = String.Format("{0}/{1}", currentBots, targetMax);
                    int botHue = currentBots < targetMin ? RedHue : (currentBots > targetMax ? YellowHue : GreenHue);
                    AddLabel(380, y, botHue, botText);
                    
                    // Safety indicator
                    string safetyText = String.Format("[{0}]", region.SafetyLevel);
                    int safetyHue = region.SafetyLevel == PlayerBotConfigurationManager.SafetyLevel.Safe ? GreenHue : 
                                   (region.SafetyLevel == PlayerBotConfigurationManager.SafetyLevel.Wilderness ? YellowHue : RedHue);
                    AddLabel(450, y, safetyHue, safetyText);
                    
                    y += 20;
                }
            }

            if (activeRegions == 0)
            {
                AddLabel(220, y, RedHue, "No active regions configured!");
                y += 20;
            }

            y += 10;

            // System Status
            AddHtml(210, y, 400, 20, Color(Center("System Status"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Population Tick:");
            AddLabel(380, y, LabelHue, config.PopulationTickSeconds + "s");
            y += 20;

            AddLabel(220, y, LabelHue, "Behavior Tick:");
            AddLabel(380, y, LabelHue, config.BehaviorTickSeconds + "s");
            y += 20;

            AddLabel(220, y, LabelHue, "Logging Enabled:");
            AddLabel(380, y, config.EnableLogging ? GreenHue : RedHue, config.EnableLogging ? "Yes" : "No");
            y += 20;

            AddLabel(220, y, LabelHue, "Last Config Load:");
            AddLabel(380, y, LabelHue, PlayerBotConfigurationManager.LastLoadTime.ToString("HH:mm:ss"));
            y += 30;

            // Quick Actions
            AddHtml(210, y, 400, 20, Color(Center("Quick Actions"), SelectedColor32), false, false);
            y += 30;

            AddButtonLabeled(220, y, GetButtonID(9, 0), "Reload Configuration");
            AddButtonLabeled(420, y, GetButtonID(9, 1), "Spawn Test Bot");
            y += 25;

            AddButtonLabeled(220, y, GetButtonID(9, 2), "Delete All Bots");
            AddButtonLabeled(420, y, GetButtonID(9, 3), "Export Statistics");
        }

        private void AddBotListPage()
        {
            if (m_List == null)
            {
                m_List = new ArrayList();
                foreach (Mobile m in World.Mobiles.Values)
                {
                    PlayerBot bot = m as PlayerBot;
                    if (bot != null && !bot.Deleted && bot.Alive)
                    {
                        m_List.Add(bot);
                    }
                }
                // Sort by name
                m_List.Sort(new PlayerBotComparer());
            }

            AddHtml(210, 20, 400, 20, Color(Center("Active PlayerBots"), LabelColor32), false, false);

            // Column headers
            AddLabelCropped(210, 50, 100, 20, LabelHue, "Name");
            AddLabelCropped(320, 50, 80, 20, LabelHue, "Profile");
            AddLabelCropped(410, 50, 60, 20, LabelHue, "Status");
            AddLabelCropped(480, 50, 60, 20, LabelHue, "Location");
            AddLabelCropped(550, 50, 50, 20, LabelHue, "Actions");

            // Pagination
            if (m_ListPage > 0)
                AddButton(575, 52, 0x15E3, 0x15E7, GetButtonID(1, 0), GumpButtonType.Reply, 0);
            else
                AddImage(575, 52, 0x25EA);

            if ((m_ListPage + 1) * 15 < m_List.Count)
                AddButton(592, 52, 0x15E1, 0x15E5, GetButtonID(1, 1), GumpButtonType.Reply, 0);
            else
                AddImage(592, 52, 0x25E6);

            if (m_List.Count == 0)
                AddLabel(210, 80, RedHue, "No active PlayerBots found.");

            for (int i = 0, index = (m_ListPage * 15); i < 15 && index >= 0 && index < m_List.Count; ++i, ++index)
            {
                PlayerBot bot = (PlayerBot)m_List[index];
                int y = 80 + (i * 25);

                // Name
                AddLabelCropped(210, y, 100, 20, GetBotNameHue(bot), bot.Name);

                // Profile
                string profileText = bot.PlayerBotProfile.ToString();
                if (profileText.Length > 8)
                    profileText = profileText.Substring(0, 8);
                AddLabelCropped(320, y, 80, 20, GetProfileHue(bot.PlayerBotProfile), profileText);

                // Status
                string status = GetBotStatus(bot);
                AddLabelCropped(410, y, 60, 20, GetStatusHue(bot), status);

                // Location
                string location = String.Format("{0},{1}", bot.X, bot.Y);
                AddLabelCropped(480, y, 60, 20, LabelHue, location);

                // Actions
                AddButton(550, y - 1, 4005, 4007, GetButtonID(2, index), GumpButtonType.Reply, 0);
                AddHtml(550 + 35, y, 50, 20, Color("Go To", LabelColor32), false, false);
            }

            // Summary
            int totalCount = m_List.Count;
            int startIndex = m_ListPage * 15 + 1;
            int endIndex = Math.Min((m_ListPage + 1) * 15, totalCount);
            
            AddHtml(210, 460, 400, 20, Color(Center(String.Format("Showing {0}-{1} of {2} bots", startIndex, endIndex, totalCount)), LabelColor32), false, false);
        }

        private void AddRegionDetailsPage()
        {
            AddHtml(210, 20, 400, 20, Color(Center("Region Details"), LabelColor32), false, false);

            int y = 50;
            Dictionary<string, int> regionCounts = GetBotsPerRegion();

            foreach (KeyValuePair<string, PlayerBotConfigurationManager.RegionConfig> regionPair in PlayerBotConfigurationManager.Regions)
            {
                PlayerBotConfigurationManager.RegionConfig region = regionPair.Value;
                if (!region.Active)
                    continue;

                int currentBots = regionCounts.ContainsKey(region.Name) ? regionCounts[region.Name] : 0;

                // Region header
                AddHtml(210, y, 400, 20, Color(Center(region.Name), SelectedColor32), false, false);
                y += 25;

                // Status indicators
                AddLabel(220, y, LabelHue, "Status:");
                AddLabel(280, y, region.Active ? GreenHue : RedHue, region.Active ? "Active" : "Inactive");
                y += 20;

                AddLabel(220, y, LabelHue, "Map:");
                AddLabel(280, y, LabelHue, region.Map.ToString());
                y += 20;

                AddLabel(220, y, LabelHue, "Safety Level:");
                int safetyHue = region.SafetyLevel == PlayerBotConfigurationManager.SafetyLevel.Safe ? GreenHue : 
                               (region.SafetyLevel == PlayerBotConfigurationManager.SafetyLevel.Wilderness ? YellowHue : RedHue);
                AddLabel(280, y, safetyHue, region.SafetyLevel.ToString());
                y += 20;

                AddLabel(220, y, LabelHue, "Bot Population:");
                string popText = String.Format("{0} / {1}-{2}", currentBots, region.MinBots, region.MaxBots);
                int popHue = currentBots < region.MinBots ? RedHue : (currentBots > region.MaxBots ? YellowHue : GreenHue);
                AddLabel(280, y, popHue, popText);
                y += 20;

                AddLabel(220, y, LabelHue, "Spawn Weight:");
                AddLabel(280, y, LabelHue, region.SpawnWeight.ToString("F1"));
                y += 20;

                AddLabel(220, y, LabelHue, "Bounds:");
                AddLabel(280, y, LabelHue, String.Format("{0},{1} to {2},{3}", 
                    region.Bounds.X, region.Bounds.Y, 
                    region.Bounds.X + region.Bounds.Width, 
                    region.Bounds.Y + region.Bounds.Height));
                y += 30;

                if (y > 430)
                    break;
            }
        }

        private void AddConfigurationPage()
        {
            PlayerBotConfigurationManager.BehaviorConfig config = PlayerBotConfigurationManager.BehaviorSettings;

            AddHtml(210, 20, 400, 20, Color(Center("Configuration Settings"), LabelColor32), false, false);

            int y = 50;

            // Population Settings
            AddHtml(210, y, 400, 20, Color(Center("Population Settings"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Global Capacity:");
            AddLabel(380, y, LabelHue, config.GlobalCap.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Population Tick:");
            AddLabel(380, y, LabelHue, config.PopulationTickSeconds + " seconds");
            y += 20;

            AddLabel(220, y, LabelHue, "Spawn Attempts:");
            AddLabel(380, y, LabelHue, config.SpawnLocationAttempts.ToString());
            y += 30;

            // Behavior Settings
            AddHtml(210, y, 400, 20, Color(Center("Behavior Settings"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Behavior Tick:");
            AddLabel(380, y, LabelHue, config.BehaviorTickSeconds + " seconds");
            y += 20;

            AddLabel(220, y, LabelHue, "Travel Chance:");
            AddLabel(380, y, LabelHue, config.TravelChancePercent + "%");
            y += 20;

            AddLabel(220, y, LabelHue, "Interaction Chance:");
            AddLabel(380, y, LabelHue, config.InteractionChancePercent + "%");
            y += 20;

            AddLabel(220, y, LabelHue, "Shop Visit Chance:");
            AddLabel(380, y, LabelHue, config.ShopVisitChance + "%");
            y += 30;

            // Travel Settings
            AddHtml(210, y, 400, 20, Color(Center("Travel Settings"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Min Travel Distance:");
            AddLabel(380, y, LabelHue, config.MinTravelDistance + " tiles");
            y += 20;

            AddLabel(220, y, LabelHue, "Max Travel Distance:");
            AddLabel(380, y, LabelHue, config.MaxTravelDistance + " tiles");
            y += 20;

            AddLabel(220, y, LabelHue, "Inter-Region Travel:");
            AddLabel(380, y, LabelHue, config.InterRegionTravelChance + "%");
            y += 30;

            // Logging Settings
            AddHtml(210, y, 400, 20, Color(Center("Logging Settings"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Enable Logging:");
            AddLabel(380, y, config.EnableLogging ? GreenHue : RedHue, config.EnableLogging ? "Yes" : "No");
            y += 20;

            AddLabel(220, y, LabelHue, "Verbose Spawning:");
            AddLabel(380, y, config.VerboseSpawning ? GreenHue : RedHue, config.VerboseSpawning ? "Yes" : "No");
            y += 20;

            AddLabel(220, y, LabelHue, "Verbose Travel:");
            AddLabel(380, y, config.VerboseTravel ? GreenHue : RedHue, config.VerboseTravel ? "Yes" : "No");
            y += 20;

            AddLabel(220, y, LabelHue, "Verbose Interactions:");
            AddLabel(380, y, config.VerboseInteractions ? GreenHue : RedHue, config.VerboseInteractions ? "Yes" : "No");
            y += 30;

            // File Information
            AddHtml(210, y, 400, 20, Color(Center("File Information"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Regions Loaded:");
            AddLabel(380, y, LabelHue, PlayerBotConfigurationManager.Regions.Count.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "POIs Loaded:");
            AddLabel(380, y, LabelHue, PlayerBotConfigurationManager.PointsOfInterest.Count.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Routes Loaded:");
            AddLabel(380, y, LabelHue, PlayerBotConfigurationManager.TravelRoutes.Count.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Last Loaded:");
            AddLabel(380, y, LabelHue, PlayerBotConfigurationManager.LastLoadTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private void AddBotDetailsPage()
        {
            PlayerBot bot = m_State as PlayerBot;
            if (bot == null || bot.Deleted)
            {
                AddHtml(210, 20, 400, 20, Color(Center("Bot Not Found"), RedHue), false, false);
                return;
            }

            AddHtml(210, 20, 400, 20, Color(Center("Bot Details: " + bot.Name), LabelColor32), false, false);

            int y = 50;

            // Basic Information
            AddHtml(210, y, 400, 20, Color(Center("Basic Information"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Name:");
            AddLabel(320, y, GetBotNameHue(bot), bot.Name);
            y += 20;

            AddLabel(220, y, LabelHue, "Profile:");
            AddLabel(320, y, GetProfileHue(bot.PlayerBotProfile), bot.PlayerBotProfile.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Experience:");
            AddLabel(320, y, LabelHue, bot.PlayerBotExperience.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Status:");
            AddLabel(320, y, GetStatusHue(bot), GetBotStatus(bot));
            y += 20;

            AddLabel(220, y, LabelHue, "Location:");
            AddLabel(320, y, LabelHue, String.Format("{0} ({1},{2})", bot.Map, bot.X, bot.Y));
            y += 30;

            // Statistics
            AddHtml(210, y, 400, 20, Color(Center("Statistics"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Fame:");
            AddLabel(320, y, bot.Fame >= 0 ? GreenHue : RedHue, bot.Fame.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Karma:");
            AddLabel(320, y, bot.Karma >= 0 ? GreenHue : RedHue, bot.Karma.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Hits:");
            AddLabel(320, y, LabelHue, String.Format("{0}/{1}", bot.Hits, bot.HitsMax));
            y += 20;

            AddLabel(220, y, LabelHue, "Mana:");
            AddLabel(320, y, LabelHue, String.Format("{0}/{1}", bot.Mana, bot.ManaMax));
            y += 20;

            AddLabel(220, y, LabelHue, "Stamina:");
            AddLabel(320, y, LabelHue, String.Format("{0}/{1}", bot.Stam, bot.StamMax));
            y += 30;

            // Combat Information
            AddHtml(210, y, 400, 20, Color(Center("Combat Information"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Preferred Combat:");
            AddLabel(320, y, LabelHue, bot.PrefersMelee ? "Melee" : "Ranged");
            y += 20;

            AddLabel(220, y, LabelHue, "Combat Skill:");
            AddLabel(320, y, LabelHue, bot.PreferedCombatSkill.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Current Target:");
            if (bot.Combatant != null)
                AddLabel(320, y, RedHue, bot.Combatant.Name);
            else
                AddLabel(320, y, GreenHue, "None");
            y += 30;

            // Control Information
            AddHtml(210, y, 400, 20, Color(Center("Control Information"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Controlled:");
            AddLabel(320, y, bot.Controled ? YellowHue : GreenHue, bot.Controled ? "Yes" : "No");
            y += 20;

            if (bot.Controled && bot.ControlMaster != null)
            {
                AddLabel(220, y, LabelHue, "Master:");
                AddLabel(320, y, LabelHue, bot.ControlMaster.Name);
                y += 20;
            }

            y += 10;

            // Actions
            AddHtml(210, y, 400, 20, Color(Center("Actions"), SelectedColor32), false, false);
            y += 30;

            AddButtonLabeled(220, y, GetButtonID(3, 0), "Go To Bot");
            AddButtonLabeled(350, y, GetButtonID(3, 1), "Get Bot");
            y += 25;

            AddButtonLabeled(220, y, GetButtonID(3, 2), "Properties");
            AddButtonLabeled(350, y, GetButtonID(3, 3), "Skills");
            y += 25;

            AddButtonLabeled(220, y, GetButtonID(3, 4), "Delete Bot");
            AddButtonLabeled(350, y, GetButtonID(3, 5), "Refresh");
        }

        #region Helper Methods

        private Dictionary<string, int> GetBotsPerRegion()
        {
            Dictionary<string, int> regionCounts = new Dictionary<string, int>();

            foreach (Mobile m in World.Mobiles.Values)
            {
                PlayerBot bot = m as PlayerBot;
                if (bot != null && !bot.Deleted && bot.Alive)
                {
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

        private Dictionary<PlayerBotPersona.PlayerBotProfile, int> GetPersonaDistribution()
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

        private int GetBotNameHue(PlayerBot bot)
        {
            if (bot.Controled)
                return YellowHue;
            if (bot.Combatant != null)
                return RedHue;
            return LabelHue;
        }

        private int GetProfileHue(PlayerBotPersona.PlayerBotProfile profile)
        {
            switch (profile)
            {
                case PlayerBotPersona.PlayerBotProfile.PlayerKiller:
                    return RedHue;
                case PlayerBotPersona.PlayerBotProfile.Crafter:
                    return GreenHue;
                case PlayerBotPersona.PlayerBotProfile.Adventurer:
                    return BlueHue;
                default:
                    return LabelHue;
            }
        }

        private int GetStatusHue(PlayerBot bot)
        {
            if (bot.Controled)
                return YellowHue;
            if (bot.Combatant != null)
                return RedHue;
            return GreenHue;
        }

        private string GetBotStatus(PlayerBot bot)
        {
            if (bot.Controled)
                return "Hired";
            if (bot.Combatant != null)
                return "Fighting";
            return "Free";
        }

        #endregion

        public override void OnResponse(NetState sender, RelayInfo info)
        {
            Mobile from = sender.Mobile;

            if (from != m_From)
                return;

            int buttonID = info.ButtonID;
            int adjustedID = buttonID - 1;
            int type = adjustedID / 1000;
            int index = adjustedID % 1000;

            switch (type)
            {
                case 0: // Page navigation
                {
                    PlayerBotStatusPage page;
                    string notice = null;

                    switch (index)
                    {
                        case 0: page = PlayerBotStatusPage.Overview; break;
                        case 1: page = PlayerBotStatusPage.BotList; break;
                        case 2: page = PlayerBotStatusPage.RegionDetails; break;
                        case 3: page = PlayerBotStatusPage.Configuration; break;
                        case 4: // Refresh
                            page = m_PageType;
                            notice = "Data refreshed.";
                            break;
                        default: return;
                    }

                    from.SendGump(new PlayerBotStatusGump(from, page, 0, null, notice, null));
                    break;
                }
                case 1: // List navigation
                {
                    switch (index)
                    {
                        case 0: // Previous page
                            if (m_List != null && m_ListPage > 0)
                                from.SendGump(new PlayerBotStatusGump(from, m_PageType, m_ListPage - 1, m_List, null, m_State));
                            break;
                        case 1: // Next page
                            if (m_List != null)
                                from.SendGump(new PlayerBotStatusGump(from, m_PageType, m_ListPage + 1, m_List, null, m_State));
                            break;
                    }
                    break;
                }
                case 2: // Go To Bot
                {
                    if (m_List != null && index >= 0 && index < m_List.Count)
                    {
                        PlayerBot bot = (PlayerBot)m_List[index];
                        if (bot != null && !bot.Deleted)
                        {
                            from.MoveToWorld(bot.Location, bot.Map);
                            from.SendGump(new PlayerBotStatusGump(from, m_PageType, m_ListPage, m_List, "Teleported to " + bot.Name, m_State));
                        }
                    }
                    break;
                }
                case 3: // Bot detail actions
                {
                    PlayerBot bot = m_State as PlayerBot;
                    if (bot == null || bot.Deleted)
                    {
                        from.SendGump(new PlayerBotStatusGump(from, PlayerBotStatusPage.BotList, 0, null, "Bot no longer exists.", null));
                        return;
                    }

                    switch (index)
                    {
                        case 0: // Go To Bot
                            from.MoveToWorld(bot.Location, bot.Map);
                            from.SendGump(new PlayerBotStatusGump(from, m_PageType, m_ListPage, m_List, "Teleported to " + bot.Name, m_State));
                            break;
                        case 1: // Get Bot
                            bot.MoveToWorld(from.Location, from.Map);
                            from.SendGump(new PlayerBotStatusGump(from, m_PageType, m_ListPage, m_List, bot.Name + " moved to your location.", m_State));
                            break;
                        case 2: // Properties
                            from.SendGump(new PlayerBotStatusGump(from, m_PageType, m_ListPage, m_List, null, m_State));
                            from.SendGump(new Server.Gumps.PropertiesGump(from, bot));
                            break;
                        case 3: // Skills
                            from.SendGump(new PlayerBotStatusGump(from, m_PageType, m_ListPage, m_List, null, m_State));
                            from.SendGump(new Server.Scripts.Gumps.SkillsGump(from, bot));
                            break;
                        case 4: // Delete Bot
                            bot.Delete();
                            from.SendGump(new PlayerBotStatusGump(from, PlayerBotStatusPage.BotList, 0, null, bot.Name + " has been deleted.", null));
                            break;
                        case 5: // Refresh
                            from.SendGump(new PlayerBotStatusGump(from, m_PageType, m_ListPage, m_List, "Bot details refreshed.", m_State));
                            break;
                    }
                    break;
                }
                case 9: // Quick actions
                {
                    string notice = null;
                    switch (index)
                    {
                        case 0: // Reload Configuration
                            try
                            {
                                PlayerBotConfigurationManager.Reload();
                                notice = "Configuration reloaded successfully.";
                            }
                            catch (Exception ex)
                            {
                                notice = "Error reloading configuration: " + ex.Message;
                            }
                            break;
                        case 1: // Spawn Test Bot
                            try
                            {
                                PlayerBot testBot = new PlayerBot();
                                testBot.MoveToWorld(from.Location, from.Map);
                                PlayerBotDirector.Instance.RegisterBot(testBot);
                                notice = "Test bot '" + testBot.Name + "' spawned.";
                            }
                            catch (Exception ex)
                            {
                                notice = "Error spawning test bot: " + ex.Message;
                            }
                            break;
                        case 2: // Delete All Bots
                            int deleteCount = 0;
                            ArrayList botsToDelete = new ArrayList();
                            foreach (Mobile m in World.Mobiles.Values)
                            {
                                PlayerBot bot = m as PlayerBot;
                                if (bot != null && !bot.Deleted)
                                    botsToDelete.Add(bot);
                            }
                            foreach (PlayerBot bot in botsToDelete)
                            {
                                bot.Delete();
                                deleteCount++;
                            }
                            notice = String.Format("Deleted {0} PlayerBots.", deleteCount);
                            break;
                        case 3: // Export Statistics
                            notice = "Statistics export not yet implemented.";
                            break;
                    }
                    from.SendGump(new PlayerBotStatusGump(from, m_PageType, m_ListPage, null, notice, m_State));
                    break;
                }
            }
        }
    }

    public class PlayerBotComparer : IComparer
    {
        public int Compare(object x, object y)
        {
            if (x == null && y == null)
                return 0;
            if (x == null)
                return -1;
            if (y == null)
                return 1;

            PlayerBot botX = x as PlayerBot;
            PlayerBot botY = y as PlayerBot;

            if (botX == null && botY == null)
                return 0;
            if (botX == null)
                return -1;
            if (botY == null)
                return 1;

            return String.Compare(botX.Name, botY.Name);
        }
    }
} 