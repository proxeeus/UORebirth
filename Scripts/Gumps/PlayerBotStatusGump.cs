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
        BotDetails,
        SceneManagement,
        POIManagement
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

            // Main background - properly sized for all content
            AddBackground(0, 0, 750, 700, 5054);

            // Navigation panel - taller to fit all buttons properly
            AddBlackAlpha(10, 10, 180, 190);
            // Main content area - wider and taller
            AddBlackAlpha(200, 10, 540, 640);
            // Notice area - wider to match new size
            AddBlackAlpha(10, 660, 730, 30);

            // Navigation buttons - properly spaced
            AddPageButton(15, 15, GetButtonID(0, 0), "OVERVIEW", PlayerBotStatusPage.Overview);
            AddPageButton(15, 40, GetButtonID(0, 1), "BOT LIST", PlayerBotStatusPage.BotList, PlayerBotStatusPage.BotDetails);
            AddPageButton(15, 65, GetButtonID(0, 2), "REGIONS", PlayerBotStatusPage.RegionDetails);
            AddPageButton(15, 90, GetButtonID(0, 3), "CONFIG", PlayerBotStatusPage.Configuration);
            AddPageButton(15, 115, GetButtonID(0, 4), "SCENES", PlayerBotStatusPage.SceneManagement);
            AddPageButton(15, 140, GetButtonID(0, 5), "POIS", PlayerBotStatusPage.POIManagement);
            AddPageButton(15, 165, GetButtonID(0, 6), "REFRESH", PlayerBotStatusPage.Overview);

            if (notice != null)
                AddHtml(12, 662, 726, 26, Color(notice, LabelColor32), false, false);

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
                case PlayerBotStatusPage.SceneManagement:
                {
                    AddSceneManagementPage();
                    break;
                }
                case PlayerBotStatusPage.POIManagement:
                {
                    AddPOIManagementPage();
                    break;
                }
            }
        }

        private void AddOverviewPage()
        {
            PlayerBotDirector director = PlayerBotDirector.Instance;
            PlayerBotConfigurationManager.BehaviorConfig config = PlayerBotConfigurationManager.BehaviorSettings;

            AddHtml(210, 20, 520, 20, Color(Center("PlayerBot System Overview"), LabelColor32), false, false);

            int y = 50;

            // Population Statistics
            AddHtml(210, y, 520, 20, Color(Center("Population Statistics"), SelectedColor32), false, false);
            y += 30;

            int registeredBots = director.GetRegisteredBotCount();
            int worldBots = director.GetWorldPlayerBotCount();
            
            AddLabel(220, y, LabelHue, "Registered Bots:");
            AddLabel(400, y, registeredBots > 0 ? GreenHue : RedHue, registeredBots.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "World Total Bots:");
            int countHue = (worldBots == registeredBots) ? GreenHue : YellowHue;
            AddLabel(400, y, countHue, worldBots.ToString());
            y += 20;

            if (worldBots != registeredBots)
            {
                int unmanagedCount = worldBots - registeredBots;
                AddLabel(220, y, RedHue, "Unmanaged Bots:");
                AddLabel(400, y, RedHue, unmanagedCount.ToString());
                AddLabel(450, y, RedHue, "(Use [BotDiagnostic)");
                y += 20;
            }

            AddLabel(220, y, LabelHue, "Global Capacity:");
            AddLabel(400, y, LabelHue, config.GlobalCap.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Utilization:");
            double utilization = config.GlobalCap > 0 ? (double)registeredBots / config.GlobalCap * 100.0 : 0.0;
            int utilizationHue = utilization > 90 ? RedHue : (utilization > 75 ? YellowHue : GreenHue);
            AddLabel(400, y, utilizationHue, String.Format("{0:F1}%", utilization));
            y += 30;

            // Persona Distribution
            Dictionary<PlayerBotPersona.PlayerBotProfile, int> personaCounts = GetPersonaDistribution();
            AddHtml(210, y, 520, 20, Color(Center("Persona Distribution"), SelectedColor32), false, false);
            y += 30;

            int adventurers = personaCounts.ContainsKey(PlayerBotPersona.PlayerBotProfile.Adventurer) ? personaCounts[PlayerBotPersona.PlayerBotProfile.Adventurer] : 0;
            int crafters = personaCounts.ContainsKey(PlayerBotPersona.PlayerBotProfile.Crafter) ? personaCounts[PlayerBotPersona.PlayerBotProfile.Crafter] : 0;
            int pks = personaCounts.ContainsKey(PlayerBotPersona.PlayerBotProfile.PlayerKiller) ? personaCounts[PlayerBotPersona.PlayerBotProfile.PlayerKiller] : 0;

            AddLabel(220, y, LabelHue, "Adventurers:");
            AddLabel(400, y, BlueHue, adventurers.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Crafters:");
            AddLabel(400, y, GreenHue, crafters.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Player Killers:");
            AddLabel(400, y, RedHue, pks.ToString());
            y += 30;

            // Region Summary
            AddHtml(210, y, 520, 20, Color(Center("Region Summary"), SelectedColor32), false, false);
            y += 30;

            int activeRegions = 0;
            foreach (PlayerBotConfigurationManager.RegionConfig region in PlayerBotConfigurationManager.Regions.Values)
            {
                if (region.Active)
                    activeRegions++;
            }

            AddLabel(220, y, LabelHue, "Total Active Regions:");
            AddLabel(400, y, activeRegions > 0 ? GreenHue : RedHue, activeRegions.ToString());
            y += 20;

            if (activeRegions == 0)
            {
                AddLabel(220, y, RedHue, "No active regions configured!");
                y += 20;
            }

            y += 10;

            // System Status
            AddHtml(210, y, 520, 20, Color(Center("System Status"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Population Tick:");
            AddLabel(400, y, LabelHue, config.PopulationTickSeconds + "s");
            y += 20;

            AddLabel(220, y, LabelHue, "Behavior Tick:");
            AddLabel(400, y, LabelHue, config.BehaviorTickSeconds + "s");
            y += 20;

            AddLabel(220, y, LabelHue, "Logging Enabled:");
            AddLabel(400, y, config.EnableLogging ? GreenHue : RedHue, config.EnableLogging ? "Yes" : "No");
            y += 20;

            AddLabel(220, y, LabelHue, "Last Config Load:");
            AddLabel(400, y, LabelHue, PlayerBotConfigurationManager.LastLoadTime.ToString("HH:mm:ss"));
            y += 30;

            // Quick Actions
            AddHtml(210, y, 520, 20, Color(Center("Quick Actions"), SelectedColor32), false, false);
            y += 30;

            AddButtonLabeled(220, y, GetButtonID(9, 0), "Reload Configuration");
            AddButtonLabeled(450, y, GetButtonID(9, 1), "Spawn Test Bot");
            y += 25;

            AddButtonLabeled(220, y, GetButtonID(9, 2), "Delete All Bots");
            AddButtonLabeled(450, y, GetButtonID(9, 3), "Fix Unmanaged Bots");
            y += 25;

            AddButtonLabeled(220, y, GetButtonID(9, 4), "Run Diagnostic");
            AddButtonLabeled(450, y, GetButtonID(9, 5), "Export Statistics");
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

            AddHtml(210, 20, 520, 20, Color(Center("Active PlayerBots"), LabelColor32), false, false);

            // Column headers - better spaced for wider gump
            AddLabelCropped(210, 50, 120, 20, LabelHue, "Name");
            AddLabelCropped(340, 50, 90, 20, LabelHue, "Profile");
            AddLabelCropped(440, 50, 70, 20, LabelHue, "Status");
            AddLabelCropped(520, 50, 80, 20, LabelHue, "Location");
            AddLabelCropped(610, 50, 60, 20, LabelHue, "Experience");
            AddLabelCropped(680, 50, 50, 20, LabelHue, "Actions");

            if (m_List.Count == 0)
                AddLabel(210, 80, RedHue, "No active PlayerBots found.");

            for (int i = 0, index = (m_ListPage * 15); i < 15 && index >= 0 && index < m_List.Count; ++i, ++index)
            {
                PlayerBot bot = (PlayerBot)m_List[index];
                int y = 80 + (i * 25);

                // Name
                AddLabelCropped(210, y, 120, 20, GetBotNameHue(bot), bot.Name);

                // Profile
                string profileText = bot.PlayerBotProfile.ToString();
                if (profileText.Length > 10)
                    profileText = profileText.Substring(0, 10);
                AddLabelCropped(340, y, 90, 20, GetProfileHue(bot.PlayerBotProfile), profileText);

                // Status
                string status = GetBotStatus(bot);
                AddLabelCropped(440, y, 70, 20, GetStatusHue(bot), status);

                // Location
                string location = String.Format("{0},{1}", bot.X, bot.Y);
                AddLabelCropped(520, y, 80, 20, LabelHue, location);

                // Experience
                string expText = bot.PlayerBotExperience.ToString();
                if (expText.Length > 8)
                    expText = expText.Substring(0, 8);
                AddLabelCropped(610, y, 60, 20, LabelHue, expText);

                // Actions
                AddButton(680, y - 1, 4005, 4007, GetButtonID(2, index), GumpButtonType.Reply, 0);
                AddHtml(680 + 35, y, 50, 20, Color("Go", LabelColor32), false, false);
            }

            // Summary and pagination controls at bottom
            int totalCount = m_List.Count;
            int startIndex = m_ListPage * 15 + 1;
            int endIndex = Math.Min((m_ListPage + 1) * 15, totalCount);
            
            // Summary text on the left
            AddHtml(210, 480, 400, 20, Color(String.Format("Showing {0}-{1} of {2} bots", startIndex, endIndex, totalCount), LabelColor32), false, false);
            
            // Pagination buttons on the right
            if (m_ListPage > 0)
                AddButton(650, 478, 0x15E3, 0x15E7, GetButtonID(1, 0), GumpButtonType.Reply, 0);
            else
                AddImage(650, 478, 0x25EA);

            if ((m_ListPage + 1) * 15 < m_List.Count)
                AddButton(670, 478, 0x15E1, 0x15E5, GetButtonID(1, 1), GumpButtonType.Reply, 0);
            else
                AddImage(670, 478, 0x25E6);
        }

        private void AddRegionDetailsPage()
        {
            // Build list of active regions if not already built
            if (m_List == null)
            {
                m_List = new ArrayList();
                foreach (KeyValuePair<string, PlayerBotConfigurationManager.RegionConfig> regionPair in PlayerBotConfigurationManager.Regions)
                {
                    PlayerBotConfigurationManager.RegionConfig region = regionPair.Value;
                    if (region.Active)
                    {
                        m_List.Add(region);
                    }
                }
            }

            AddHtml(210, 20, 520, 20, Color(Center("Region Details"), LabelColor32), false, false);

            // Pagination controls - regions per page: 5 (each takes ~70px, better spacing)
            int regionsPerPage = 5;
            
            int y = 50;
            Dictionary<string, int> regionCounts = GetBotsPerRegion();

            if (m_List.Count == 0)
            {
                AddLabel(210, y, RedHue, "No active regions found.");
                return;
            }

            // Display regions for current page
            for (int i = 0, index = (m_ListPage * regionsPerPage); i < regionsPerPage && index >= 0 && index < m_List.Count; ++i, ++index)
            {
                PlayerBotConfigurationManager.RegionConfig region = (PlayerBotConfigurationManager.RegionConfig)m_List[index];
                int currentBots = regionCounts.ContainsKey(region.Name) ? regionCounts[region.Name] : 0;

                // Region header
                AddHtml(210, y, 520, 20, Color(Center(region.Name), SelectedColor32), false, false);
                y += 25;

                // Left column
                AddLabel(220, y, LabelHue, "Status:");
                AddLabel(300, y, region.Active ? GreenHue : RedHue, region.Active ? "Active" : "Inactive");
                
                AddLabel(220, y + 20, LabelHue, "Map:");
                AddLabel(300, y + 20, LabelHue, region.Map.ToString());
                
                AddLabel(220, y + 40, LabelHue, "Safety Level:");
                int safetyHue = region.SafetyLevel == PlayerBotConfigurationManager.SafetyLevel.Safe ? GreenHue : 
                               (region.SafetyLevel == PlayerBotConfigurationManager.SafetyLevel.Wilderness ? YellowHue : RedHue);
                AddLabel(300, y + 40, safetyHue, region.SafetyLevel.ToString());

                // Right column
                AddLabel(450, y, LabelHue, "Bot Population:");
                string popText = String.Format("{0} / {1}-{2}", currentBots, region.MinBots, region.MaxBots);
                int popHue = currentBots < region.MinBots ? RedHue : (currentBots > region.MaxBots ? YellowHue : GreenHue);
                AddLabel(550, y, popHue, popText);
                
                AddLabel(450, y + 20, LabelHue, "Spawn Weight:");
                AddLabel(550, y + 20, LabelHue, region.SpawnWeight.ToString("F1"));
                
                AddLabel(450, y + 40, LabelHue, "Bounds:");
                AddLabel(550, y + 40, LabelHue, String.Format("{0},{1}-{2},{3}", 
                    region.Bounds.X, region.Bounds.Y, 
                    region.Bounds.X + region.Bounds.Width, 
                    region.Bounds.Y + region.Bounds.Height));
                
                // Goto button
                AddButton(680, y + 20, 4005, 4007, GetButtonID(5, index), GumpButtonType.Reply, 0);
                AddHtml(680 + 32, y + 20, 35, 20, Color("Goto", LabelColor32), false, false);
                
                y += 70;
            }

            // Summary and pagination controls at bottom
            int totalCount = m_List.Count;
            int startIndex = m_ListPage * regionsPerPage + 1;
            int endIndex = Math.Min((m_ListPage + 1) * regionsPerPage, totalCount);
            
            // Summary text on the left
            AddHtml(210, 580, 400, 20, Color(String.Format("Showing {0}-{1} of {2} regions", startIndex, endIndex, totalCount), LabelColor32), false, false);
            
            // Pagination buttons on the right
            if (m_ListPage > 0)
                AddButton(650, 578, 0x15E3, 0x15E7, GetButtonID(1, 0), GumpButtonType.Reply, 0);
            else
                AddImage(650, 578, 0x25EA);

            if ((m_ListPage + 1) * regionsPerPage < m_List.Count)
                AddButton(670, 578, 0x15E1, 0x15E5, GetButtonID(1, 1), GumpButtonType.Reply, 0);
            else
                AddImage(670, 578, 0x25E6);
        }

        private void AddConfigurationPage()
        {
            PlayerBotConfigurationManager.BehaviorConfig config = PlayerBotConfigurationManager.BehaviorSettings;

            AddHtml(210, 20, 520, 20, Color(Center("Configuration Settings"), LabelColor32), false, false);

            int y = 50;

            // Population Settings
            AddHtml(210, y, 520, 20, Color(Center("Population Settings"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Global Capacity:");
            AddLabel(400, y, LabelHue, config.GlobalCap.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "Population Tick:");
            AddLabel(400, y, LabelHue, config.PopulationTickSeconds + " seconds");
            y += 20;

            AddLabel(220, y, LabelHue, "Spawn Attempts:");
            AddLabel(400, y, LabelHue, config.SpawnLocationAttempts.ToString());
            y += 30;

            // Behavior Settings
            AddHtml(210, y, 520, 20, Color(Center("Behavior Settings"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Behavior Tick:");
            AddLabel(400, y, LabelHue, config.BehaviorTickSeconds + " seconds");
            y += 20;

            AddLabel(220, y, LabelHue, "Interaction Chance:");
            AddLabel(400, y, LabelHue, config.InteractionChancePercent + "%");
            y += 20;

            AddLabel(220, y, LabelHue, "Shop Visit Chance:");
            AddLabel(400, y, LabelHue, config.ShopVisitChance + "%");
            y += 30;

            // Logging Settings
            AddHtml(210, y, 520, 20, Color(Center("Logging Settings"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Enable Logging:");
            AddLabel(400, y, config.EnableLogging ? GreenHue : RedHue, config.EnableLogging ? "Yes" : "No");
            y += 20;

            AddLabel(220, y, LabelHue, "Verbose Spawning:");
            AddLabel(400, y, config.VerboseSpawning ? GreenHue : RedHue, config.VerboseSpawning ? "Yes" : "No");
            y += 20;

            AddLabel(220, y, LabelHue, "Verbose Travel:");
            AddLabel(400, y, config.VerboseTravel ? GreenHue : RedHue, config.VerboseTravel ? "Yes" : "No");
            y += 20;

            AddLabel(220, y, LabelHue, "Verbose Interactions:");
            AddLabel(400, y, config.VerboseInteractions ? GreenHue : RedHue, config.VerboseInteractions ? "Yes" : "No");
            y += 30;

            // File Information
            AddHtml(210, y, 520, 20, Color(Center("File Information"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Regions Loaded:");
            AddLabel(400, y, LabelHue, PlayerBotConfigurationManager.Regions.Count.ToString());
            y += 20;

            AddLabel(220, y, LabelHue, "POIs Loaded:");
            AddLabel(400, y, LabelHue, PlayerBotConfigurationManager.PointsOfInterest.Count.ToString());
            y += 20;



            AddLabel(220, y, LabelHue, "Last Loaded:");
            AddLabel(400, y, LabelHue, PlayerBotConfigurationManager.LastLoadTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private void AddBotDetailsPage()
        {
            PlayerBot bot = m_State as PlayerBot;
            if (bot == null || bot.Deleted)
            {
                AddHtml(210, 20, 520, 20, Color(Center("Bot Not Found"), RedHue), false, false);
                return;
            }

            AddHtml(210, 20, 520, 20, Color(Center("Bot Details: " + bot.Name), LabelColor32), false, false);

            int y = 50;

            // Basic Information
            AddHtml(210, y, 520, 20, Color(Center("Basic Information"), SelectedColor32), false, false);
            y += 30;

            // Left column
            AddLabel(220, y, LabelHue, "Name:");
            AddLabel(320, y, GetBotNameHue(bot), bot.Name);
            
            AddLabel(220, y + 20, LabelHue, "Profile:");
            AddLabel(320, y + 20, GetProfileHue(bot.PlayerBotProfile), bot.PlayerBotProfile.ToString());
            
            AddLabel(220, y + 40, LabelHue, "Experience:");
            AddLabel(320, y + 40, LabelHue, bot.PlayerBotExperience.ToString());

            // Right column
            AddLabel(450, y, LabelHue, "Status:");
            AddLabel(550, y, GetStatusHue(bot), GetBotStatus(bot));
            
            AddLabel(450, y + 20, LabelHue, "Location:");
            AddLabel(550, y + 20, LabelHue, String.Format("{0} ({1},{2})", bot.Map, bot.X, bot.Y));
            
            AddLabel(450, y + 40, LabelHue, "Speech Hue:");
            AddLabel(550, y + 40, bot.SpeechHue, "Sample Text");
            
            y += 10;

            // Statistics
            AddHtml(210, y, 520, 20, Color(Center("Statistics"), SelectedColor32), false, false);
            y += 30;

            // Left column - Fame/Karma/Stats
            AddLabel(220, y, LabelHue, "Fame:");
            AddLabel(320, y, bot.Fame >= 0 ? GreenHue : RedHue, bot.Fame.ToString());
            
            AddLabel(220, y + 20, LabelHue, "Karma:");
            AddLabel(320, y + 20, bot.Karma >= 0 ? GreenHue : RedHue, bot.Karma.ToString());
            
            AddLabel(220, y + 40, LabelHue, "Hits:");
            AddLabel(320, y + 40, LabelHue, String.Format("{0}/{1}", bot.Hits, bot.HitsMax));

            // Right column - More stats
            AddLabel(450, y, LabelHue, "Mana:");
            AddLabel(550, y, LabelHue, String.Format("{0}/{1}", bot.Mana, bot.ManaMax));
            
            AddLabel(450, y + 20, LabelHue, "Stamina:");
            AddLabel(550, y + 20, LabelHue, String.Format("{0}/{1}", bot.Stam, bot.StamMax));
            
            AddLabel(450, y + 40, LabelHue, "Str/Dex/Int:");
            AddLabel(550, y + 40, LabelHue, String.Format("{0}/{1}/{2}", bot.Str, bot.Dex, bot.Int));
            
            y += 10;

            // Combat Information
            AddHtml(210, y, 520, 20, Color(Center("Combat Information"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Preferred Combat:");
            AddLabel(350, y, LabelHue, bot.PrefersMelee ? "Melee" : "Ranged");
            
            AddLabel(220, y + 20, LabelHue, "Combat Skill:");
            AddLabel(350, y + 20, LabelHue, bot.PreferedCombatSkill.ToString());
            
            AddLabel(450, y, LabelHue, "Current Target:");
            if (bot.Combatant != null)
                AddLabel(570, y, RedHue, bot.Combatant.Name);
            else
                AddLabel(570, y, GreenHue, "None");
                
            y += 50;

            // Control Information
            AddHtml(210, y, 520, 20, Color(Center("Control Information"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Controlled:");
            AddLabel(320, y, bot.Controled ? YellowHue : GreenHue, bot.Controled ? "Yes" : "No");

            if (bot.Controled && bot.ControlMaster != null)
            {
                AddLabel(450, y, LabelHue, "Master:");
                AddLabel(520, y, LabelHue, bot.ControlMaster.Name);
            }

            y += 40;

            // Actions
            AddHtml(210, y, 520, 20, Color(Center("Actions"), SelectedColor32), false, false);
            y += 30;

            AddButtonLabeled(220, y, GetButtonID(3, 0), "Go To Bot");
            AddButtonLabeled(350, y, GetButtonID(3, 1), "Get Bot");
            AddButtonLabeled(480, y, GetButtonID(3, 5), "Refresh");
            y += 25;

            AddButtonLabeled(220, y, GetButtonID(3, 2), "Properties");
            AddButtonLabeled(350, y, GetButtonID(3, 3), "Skills");
            AddButtonLabeled(480, y, GetButtonID(3, 4), "Delete Bot");
        }

        private void AddSceneManagementPage()
        {
            PlayerBotDirector director = PlayerBotDirector.Instance;
            List<PlayerBotScene> activeScenes = director.GetActiveScenes();

            AddHtml(210, 20, 520, 20, Color(Center("Scene Management"), LabelColor32), false, false);

            int y = 50;

            // Scene Statistics
            AddHtml(210, y, 520, 20, Color(Center("Scene Statistics"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Active Scenes:");
            AddLabel(400, y, activeScenes.Count > 0 ? GreenHue : LabelHue, activeScenes.Count.ToString());
            y += 20;

            // Count scenes by type
            int warScenes = 0;
            foreach (PlayerBotScene scene in activeScenes)
            {
                if (scene is Server.Engines.Scenes.WarScene)
                    warScenes++;

            }

            AddLabel(220, y, LabelHue, "War Scenes:");
            AddLabel(400, y, warScenes > 0 ? RedHue : LabelHue, warScenes.ToString());
            y += 20;

            y += 10;

            // Scene Creation
            AddHtml(210, y, 520, 20, Color(Center("Create New Scene"), SelectedColor32), false, false);
            y += 30;

            AddButtonLabeled(220, y, GetButtonID(4, 0), "Create War Scene");
            y += 30;

            // Active Scenes List
            AddHtml(210, y, 520, 20, Color(Center("Active Scenes"), SelectedColor32), false, false);
            y += 30;

            if (activeScenes.Count == 0)
            {
                AddLabel(220, y, DisabledColor32, "No active scenes");
                y += 20;
            }
            else
            {
                // Table headers
                AddLabel(220, y, LabelHue, "ID");
                AddLabel(250, y, LabelHue, "Type");
                AddLabel(320, y, LabelHue, "State");
                AddLabel(380, y, LabelHue, "Participants");
                AddLabel(470, y, LabelHue, "Duration");
                AddLabel(540, y, LabelHue, "Actions");
                y += 20;

                // Scene list (limited to fit in view)
                int maxScenes = Math.Min(activeScenes.Count, 15);
                for (int i = 0; i < maxScenes; i++)
                {
                    PlayerBotScene scene = activeScenes[i];
                    string sceneType = scene.GetType().Name.Replace("Scene", "");
                    int participants = scene.GetParticipantCount();
                    double duration = (DateTime.Now - scene.StartTime).TotalSeconds;

                    // Scene ID
                    AddLabel(220, y, LabelHue, scene.SceneId.ToString());

                    // Scene Type with color coding
                    int typeHue = sceneType == "War" ? RedHue : LabelHue;
                    AddLabel(250, y, typeHue, sceneType);

                    // State
                    int stateHue = scene.CurrentState == PlayerBotScene.SceneState.Active ? GreenHue : YellowHue;
                    AddLabel(320, y, stateHue, scene.CurrentState.ToString());

                    // Participants
                    AddLabel(400, y, participants > 0 ? GreenHue : RedHue, participants.ToString());

                    // Duration
                    string durationStr = duration < 60 ? String.Format("{0:F0}s", duration) : 
                                        duration < 3600 ? String.Format("{0:F1}m", duration / 60) :
                                        String.Format("{0:F1}h", duration / 3600);
                    AddLabel(470, y, LabelHue, durationStr);

                    // Action buttons
                    AddButton(540, y - 1, 4005, 4007, GetButtonID(4, 10 + i), GumpButtonType.Reply, 0);
                    AddLabel(575, y, LabelHue, "Info");

                    AddButton(610, y - 1, 4005, 4007, GetButtonID(4, 50 + i), GumpButtonType.Reply, 0);
                    AddLabel(645, y, RedHue, "End");

                    y += 20;
                }

                if (activeScenes.Count > maxScenes)
                {
                    AddLabel(220, y, DisabledColor32, String.Format("... and {0} more scenes", activeScenes.Count - maxScenes));
                    y += 20;
                }
            }

            y += 20;

            // Global Scene Actions
            AddHtml(210, y, 520, 20, Color(Center("Global Actions"), SelectedColor32), false, false);
            y += 30;

            AddButtonLabeled(220, y, GetButtonID(4, 2), "End All Scenes");
            AddButtonLabeled(380, y, GetButtonID(4, 3), "Refresh List");
            y += 30;

            // Scene Configuration (if we want to add this later)
            AddHtml(210, y, 520, 20, Color(Center("Scene Configuration"), SelectedColor32), false, false);
            y += 30;

            AddLabel(220, y, LabelHue, "Auto-Scene Creation:");
            AddLabel(400, y, director.AutoSceneCreation ? GreenHue : RedHue, director.AutoSceneCreation ? "Enabled" : "Disabled");
            y += 20;

            AddButtonLabeled(220, y, GetButtonID(4, 4), director.AutoSceneCreation ? "Disable Auto-Scenes" : "Enable Auto-Scenes");
        }

        private void AddPOIManagementPage()
        {
            AddHtml(210, 20, 520, 20, Color(Center("Points of Interest Management"), LabelColor32), false, false);

            int y = 50;

            // POI Statistics
            AddHtml(210, y, 520, 20, Color(Center("POI Statistics"), SelectedColor32), false, false);
            y += 30;

            int totalPOIs = PlayerBotConfigurationManager.PointsOfInterest.Count;
            Dictionary<PlayerBotDirector.POIType, int> poiTypeCounts = GetPOITypeCounts();

            AddLabel(220, y, LabelHue, "Total POIs:");
            AddLabel(400, y, totalPOIs > 0 ? GreenHue : RedHue, totalPOIs.ToString());
            y += 20;

            foreach (KeyValuePair<PlayerBotDirector.POIType, int> kvp in poiTypeCounts)
            {
                AddLabel(240, y, LabelHue, kvp.Key.ToString() + ":");
                AddLabel(400, y, LabelHue, kvp.Value.ToString());
                y += 20;
            }

            y += 10;

            // POI List with pagination
            AddHtml(210, y, 520, 20, Color(Center("POI List"), SelectedColor32), false, false);
            y += 30;

            // Create sorted list of POIs
            List<PlayerBotConfigurationManager.POIConfig> sortedPOIs = new List<PlayerBotConfigurationManager.POIConfig>();
            foreach (PlayerBotConfigurationManager.POIConfig poi in PlayerBotConfigurationManager.PointsOfInterest.Values)
            {
                sortedPOIs.Add(poi);
            }
            sortedPOIs.Sort((a, b) => string.Compare(a.Name, b.Name));

            // Pagination
            int itemsPerPage = 15;
            int totalPages = (int)Math.Ceiling((double)sortedPOIs.Count / itemsPerPage);
            int currentPage = m_ListPage;
            
            if (currentPage >= totalPages)
                currentPage = totalPages - 1;
            if (currentPage < 0)
                currentPage = 0;

            // Page navigation
            if (totalPages > 1)
            {
                AddLabel(220, y, LabelHue, String.Format("Page {0} of {1}", currentPage + 1, totalPages));
                
                if (currentPage > 0)
                    AddButton(400, y - 1, 4014, 4015, GetButtonID(6, 0), GumpButtonType.Reply, 0); // Previous
                
                if (currentPage < totalPages - 1)
                    AddButton(430, y - 1, 4005, 4006, GetButtonID(6, 1), GumpButtonType.Reply, 0); // Next
                
                y += 25;
            }

            // Column headers
            AddLabel(220, y, LabelHue, "Name");
            AddLabel(350, y, LabelHue, "Type");
            AddLabel(420, y, LabelHue, "Map");
            AddLabel(470, y, LabelHue, "Location");
            AddLabel(580, y, LabelHue, "Region");
            AddLabel(670, y, LabelHue, "Actions");
            y += 20;

            // POI entries
            int startIndex = currentPage * itemsPerPage;
            int endIndex = Math.Min(startIndex + itemsPerPage, sortedPOIs.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                PlayerBotConfigurationManager.POIConfig poi = sortedPOIs[i];
                
                // POI name with type-based color
                int nameHue = GetPOITypeHue(poi.Type);
                AddLabel(220, y, nameHue, poi.Name);
                
                // POI type
                AddLabel(350, y, LabelHue, poi.Type.ToString());
                
                // Map
                AddLabel(420, y, LabelHue, poi.Map.ToString());
                
                // Location
                AddLabel(470, y, LabelHue, String.Format("{0},{1},{2}", poi.Location.X, poi.Location.Y, poi.Location.Z));
                
                // Region
                AddLabel(580, y, LabelHue, poi.Region);
                
                // Actions
                AddButton(670, y - 1, 4005, 4007, GetButtonID(7, i), GumpButtonType.Reply, 0);
                AddHtml(705, y, 50, 20, Color("Go", LabelColor32), false, false);
                
                y += 20;
            }

            // POI Management Actions
            y += 20;
            AddHtml(210, y, 520, 20, Color(Center("POI Management"), SelectedColor32), false, false);
            y += 30;

            AddButtonLabeled(220, y, GetButtonID(6, 10), "Reload POI Configuration");
            y += 30;

            AddButtonLabeled(220, y, GetButtonID(6, 11), "Show POI Usage Statistics");
            y += 30;

            AddButtonLabeled(220, y, GetButtonID(6, 12), "Test POI Pathfinding");
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

        private Dictionary<PlayerBotDirector.POIType, int> GetPOITypeCounts()
        {
            Dictionary<PlayerBotDirector.POIType, int> typeCounts = new Dictionary<PlayerBotDirector.POIType, int>();

            foreach (PlayerBotConfigurationManager.POIConfig poi in PlayerBotConfigurationManager.PointsOfInterest.Values)
            {
                if (!typeCounts.ContainsKey(poi.Type))
                    typeCounts[poi.Type] = 0;
                typeCounts[poi.Type]++;
            }

            return typeCounts;
        }

        private int GetPOITypeHue(PlayerBotDirector.POIType type)
        {
            switch (type)
            {
                case PlayerBotDirector.POIType.Bank:
                    return YellowHue;
                case PlayerBotDirector.POIType.Shop:
                    return GreenHue;
                case PlayerBotDirector.POIType.Tavern:
                    return BlueHue;
                case PlayerBotDirector.POIType.Healer:
                    return GreenHue;
                case PlayerBotDirector.POIType.Dungeon:
                    return RedHue;
                case PlayerBotDirector.POIType.Landmark:
                    return LabelHue;
                case PlayerBotDirector.POIType.Waypoint:
                    return LabelHue;
                default:
                    return LabelHue;
            }
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

        private void ShowSceneInfo(Mobile from, PlayerBotScene scene)
        {
            string sceneType = scene.GetType().Name.Replace("Scene", "");
            
            from.SendMessage(0x35, "=== Scene {0} Information ===", scene.SceneId);
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
                        case 4: page = PlayerBotStatusPage.SceneManagement; break;
                        case 5: page = PlayerBotStatusPage.POIManagement; break;
                        case 6: // Refresh
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
                case 4: // Scene Management
                {
                    PlayerBotDirector director = PlayerBotDirector.Instance;
                    List<PlayerBotScene> activeScenes = director.GetActiveScenes();
                    string notice = null;

                    switch (index)
                    {
                        case 0: // Create War Scene
                            try
                            {
                                PlayerBotScene warScene = new Server.Engines.Scenes.WarScene(from.Location, from.Map, 8);
                                warScene.Initialize(); // Initialize to spawn participants
                                director.AddScene(warScene);
                                notice = String.Format("War scene created with ID {0} at your location with {1} participants.", warScene.SceneId, warScene.GetParticipantCount());
                            }
                            catch (Exception ex)
                            {
                                notice = "Error creating war scene: " + ex.Message;
                            }
                            break;



                        case 2: // End All Scenes
                            try
                            {
                                int endedCount = 0;
                                foreach (PlayerBotScene scene in activeScenes)
                                {
                                    scene.ForceEnd();
                                    endedCount++;
                                }
                                notice = String.Format("Ended {0} scene(s).", endedCount);
                            }
                            catch (Exception ex)
                            {
                                notice = "Error ending scenes: " + ex.Message;
                            }
                            break;

                        case 3: // Refresh List
                            notice = "Scene list refreshed.";
                            break;

                        case 4: // Toggle Auto-Scene Creation
                            try
                            {
                                director.AutoSceneCreation = !director.AutoSceneCreation;
                                notice = String.Format("Auto-scene creation {0}.", director.AutoSceneCreation ? "enabled" : "disabled");
                            }
                            catch (Exception ex)
                            {
                                notice = "Error toggling auto-scene creation: " + ex.Message;
                            }
                            break;

                        default:
                            // Handle scene info buttons (10-24) and end scene buttons (50-64)
                            if (index >= 10 && index < 25)
                            {
                                int sceneIndex = index - 10;
                                if (sceneIndex < activeScenes.Count)
                                {
                                    PlayerBotScene scene = activeScenes[sceneIndex];
                                    ShowSceneInfo(from, scene);
                                    return; // Don't refresh the main gump
                                }
                            }
                            else if (index >= 50 && index < 65)
                            {
                                int sceneIndex = index - 50;
                                if (sceneIndex < activeScenes.Count)
                                {
                                    try
                                    {
                                        PlayerBotScene scene = activeScenes[sceneIndex];
                                        string sceneType = scene.GetType().Name.Replace("Scene", "");
                                        scene.ForceEnd();
                                        notice = String.Format("Ended {0} scene {1}.", sceneType, scene.SceneId);
                                    }
                                    catch (Exception ex)
                                    {
                                        notice = "Error ending scene: " + ex.Message;
                                    }
                                }
                            }
                            break;
                    }

                    from.SendGump(new PlayerBotStatusGump(from, PlayerBotStatusPage.SceneManagement, 0, null, notice, null));
                    break;
                }
                case 5: // Go To Region
                {
                    if (m_List != null && index >= 0 && index < m_List.Count)
                    {
                        PlayerBotConfigurationManager.RegionConfig region = (PlayerBotConfigurationManager.RegionConfig)m_List[index];
                        if (region != null)
                        {
                            // Calculate center of region
                            Point3D center = new Point3D(
                                region.Bounds.X + (region.Bounds.Width / 2),
                                region.Bounds.Y + (region.Bounds.Height / 2),
                                region.Map.GetAverageZ(region.Bounds.X + (region.Bounds.Width / 2), region.Bounds.Y + (region.Bounds.Height / 2))
                            );
                            
                            from.MoveToWorld(center, region.Map);
                            from.SendGump(new PlayerBotStatusGump(from, m_PageType, m_ListPage, m_List, "Teleported to " + region.Name, m_State));
                        }
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
                        case 3: // Fix Unmanaged Bots
                            try
                            {
                                int fixedCount = PlayerBotDirector.Instance.ForceRegisterUnmanagedBots();
                                if (fixedCount > 0)
                                    notice = String.Format("Registered {0} unmanaged PlayerBot(s).", fixedCount);
                                else
                                    notice = "No unmanaged PlayerBots found.";
                            }
                            catch (Exception ex)
                            {
                                notice = "Error fixing unmanaged bots: " + ex.Message;
                            }
                            break;
                        case 4: // Run Diagnostic
                            try
                            {
                                int worldCount = PlayerBotDirector.Instance.GetWorldPlayerBotCount();
                                int registeredCount = PlayerBotDirector.Instance.GetRegisteredBotCount();
                                int unmanagedCount = worldCount - registeredCount;
                                
                                if (unmanagedCount == 0)
                                    notice = String.Format("Diagnostic complete: All {0} PlayerBots are properly managed.", worldCount);
                                else
                                    notice = String.Format("Diagnostic complete: {0} managed, {1} unmanaged bots found.", registeredCount, unmanagedCount);
                            }
                            catch (Exception ex)
                            {
                                notice = "Error running diagnostic: " + ex.Message;
                            }
                            break;
                        case 5: // Export Statistics
                            notice = "Statistics export not yet implemented.";
                            break;
                    }
                    from.SendGump(new PlayerBotStatusGump(from, m_PageType, m_ListPage, null, notice, m_State));
                    break;
                }
                case 6: // POI Management pagination
                {
                    switch (index)
                    {
                        case 0: // Previous page
                            if (m_ListPage > 0)
                                from.SendGump(new PlayerBotStatusGump(from, PlayerBotStatusPage.POIManagement, m_ListPage - 1, null, null, null));
                            break;
                        case 1: // Next page
                            from.SendGump(new PlayerBotStatusGump(from, PlayerBotStatusPage.POIManagement, m_ListPage + 1, null, null, null));
                            break;
                        case 10: // Reload POI Configuration
                            try
                            {
                                PlayerBotConfigurationManager.Reload();
                                from.SendGump(new PlayerBotStatusGump(from, PlayerBotStatusPage.POIManagement, 0, null, "POI configuration reloaded successfully.", null));
                            }
                            catch (Exception ex)
                            {
                                from.SendGump(new PlayerBotStatusGump(from, PlayerBotStatusPage.POIManagement, 0, null, "Error reloading POI configuration: " + ex.Message, null));
                            }
                            break;
                        case 11: // Show POI Usage Statistics
                            ShowPOIUsageStatistics(from);
                            return; // Don't refresh the main gump
                        case 12: // Test POI Pathfinding
                            from.SendGump(new PlayerBotStatusGump(from, PlayerBotStatusPage.POIManagement, m_ListPage, null, "POI pathfinding test not yet implemented.", null));
                            break;
                    }
                    break;
                }
                case 7: // Go To POI
                {
                    // Create a sorted list to match the display order
                    List<PlayerBotConfigurationManager.POIConfig> sortedPOIs = new List<PlayerBotConfigurationManager.POIConfig>();
                    foreach (PlayerBotConfigurationManager.POIConfig poi in PlayerBotConfigurationManager.PointsOfInterest.Values)
                    {
                        sortedPOIs.Add(poi);
                    }
                    sortedPOIs.Sort((a, b) => string.Compare(a.Name, b.Name));

                    if (index >= 0 && index < sortedPOIs.Count)
                    {
                        PlayerBotConfigurationManager.POIConfig poi = sortedPOIs[index];
                        from.MoveToWorld(poi.Location, poi.Map);
                        from.SendGump(new PlayerBotStatusGump(from, PlayerBotStatusPage.POIManagement, m_ListPage, null, "Teleported to " + poi.Name, null));
                    }
                    break;
                }
            }
        }

        private void ShowPOIUsageStatistics(Mobile from)
        {
            from.SendMessage(0x35, "=== POI Usage Statistics ===");
            
            Dictionary<PlayerBotDirector.POIType, int> typeCounts = GetPOITypeCounts();
            Dictionary<string, int> regionCounts = new Dictionary<string, int>();
            Dictionary<Map, int> mapCounts = new Dictionary<Map, int>();
            
            foreach (PlayerBotConfigurationManager.POIConfig poi in PlayerBotConfigurationManager.PointsOfInterest.Values)
            {
                // Count by region
                if (!string.IsNullOrEmpty(poi.Region))
                {
                    if (!regionCounts.ContainsKey(poi.Region))
                        regionCounts[poi.Region] = 0;
                    regionCounts[poi.Region]++;
                }
                
                // Count by map
                if (!mapCounts.ContainsKey(poi.Map))
                    mapCounts[poi.Map] = 0;
                mapCounts[poi.Map]++;
            }
            
            from.SendMessage("Total POIs: {0}", PlayerBotConfigurationManager.PointsOfInterest.Count);
            from.SendMessage("");
            
            from.SendMessage("POIs by Type:");
            foreach (KeyValuePair<PlayerBotDirector.POIType, int> kvp in typeCounts)
            {
                from.SendMessage("  {0}: {1}", kvp.Key, kvp.Value);
            }
            from.SendMessage("");
            
            from.SendMessage("POIs by Map:");
            foreach (KeyValuePair<Map, int> kvp in mapCounts)
            {
                from.SendMessage("  {0}: {1}", kvp.Key, kvp.Value);
            }
            from.SendMessage("");
            
            from.SendMessage("POIs by Region:");
            foreach (KeyValuePair<string, int> kvp in regionCounts)
            {
                from.SendMessage("  {0}: {1}", kvp.Key, kvp.Value);
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