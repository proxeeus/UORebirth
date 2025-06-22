using System;
using System.Collections.Generic;
using Server;
using Server.Mobiles;
using Server.Items;

namespace Server.Engines.Scenes
{
    /// <summary>
    /// Merchant Caravan Scene - Creates traveling merchant groups with guards and pack animals
    /// Players can encounter these caravans for trading or may choose to attack/defend them
    /// </summary>
    public class MerchantCaravanScene : PlayerBotScene
    {
        #region Private Fields
        private List<PlayerBot> m_Merchants;
        private List<PlayerBot> m_Guards;
        private List<PackHorse> m_PackAnimals;
        private Point3D m_Destination;
        private List<Point3D> m_Waypoints;
        private int m_CurrentWaypointIndex;
        private DateTime m_LastMovement;
        private bool m_HasReachedDestination;
        #endregion

        #region Constructor
        public MerchantCaravanScene(Point3D start, Point3D destination, Map map) 
            : base(SceneType.MerchantCaravan, "Merchant Caravan", start, map, false)
        {
            m_Merchants = new List<PlayerBot>();
            m_Guards = new List<PlayerBot>();
            m_PackAnimals = new List<PackHorse>();
            m_Destination = destination;
            m_Waypoints = new List<Point3D>();
            m_CurrentWaypointIndex = 0;
            m_LastMovement = DateTime.Now;
            m_HasReachedDestination = false;
            
            // Calculate travel time based on distance
            double distance = GetDistance(start, destination);
            int travelMinutes = (int)Math.Max(15, distance / 10); // Roughly 1 minute per 10 tiles
            m_Duration = TimeSpan.FromMinutes(travelMinutes);
            
            // Generate waypoints for the journey
            GenerateWaypoints();
            
            m_Name = string.Format("Merchant Caravan to {0}", GetDestinationName(destination, map));
        }
        #endregion

        #region Abstract Implementation
        public override bool CanTrigger(PlayerBotDirector.RegionProfile region, List<Mobile> nearbyPlayers)
        {
            // Merchant caravans can spawn in any region type
            // Don't require players (background activity)
            
            // Check if area is suitable for travel
            if (region.Area.Width < 50 || region.Area.Height < 50)
                return false;
            
            return true;
        }

        public override void Initialize()
        {
            if (m_State != SceneState.Preparing)
                return;

            try
            {
                // Spawn 1-2 merchants
                int merchantCount = Utility.RandomMinMax(1, 2);
                for (int i = 0; i < merchantCount; i++)
                {
                    PlayerBot merchant = SpawnBot(PlayerBotPersona.PlayerBotProfile.Crafter, "Merchant");
                    if (merchant != null)
                    {
                        // Ensure merchants have good karma
                        merchant.Karma = Utility.Random(50, 200);
                        m_Merchants.Add(merchant);
                        EquipMerchant(merchant);
                    }
                }

                // Spawn 2-3 guards
                int guardCount = Utility.RandomMinMax(2, 3);
                for (int i = 0; i < guardCount; i++)
                {
                    PlayerBot guard = SpawnBot(PlayerBotPersona.PlayerBotProfile.Adventurer, "Caravan Guard");
                    if (guard != null)
                    {
                        // Ensure guards have good karma (lawful protectors)
                        guard.Karma = Utility.Random(100, 300);
                        m_Guards.Add(guard);
                        EquipGuard(guard);
                    }
                }

                // Spawn pack animals with trade goods
                int packAnimalCount = Utility.RandomMinMax(2, 4);
                for (int i = 0; i < packAnimalCount; i++)
                {
                    PackHorse horse = SpawnPackAnimal();
                    if (horse != null)
                    {
                        m_PackAnimals.Add(horse);
                        LoadTradeGoods(horse);
                    }
                }

                // Set initial formation
                SetCaravanFormation();

                // Announce caravan start
                AnnounceCaravanStart();

                if (PlayerBotConfigurationManager.BehaviorSettings.EnableLogging && 
                    PlayerBotConfigurationManager.BehaviorSettings.VerboseEvents)
                {
                    Console.WriteLine("[{0}] [MerchantCaravanScene] Caravan started: {1} merchants, {2} guards, {3} pack animals", 
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), 
                        m_Merchants.Count, m_Guards.Count, m_PackAnimals.Count);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MerchantCaravanScene] Error initializing caravan: {0}", ex.Message);
                m_State = SceneState.Complete; // Force cleanup
            }
        }

        public override void OnTick()
        {
            if (m_State != SceneState.Active)
                return;

            try
            {
                // Check if caravan is under attack
                if (IsCaravanUnderAttack())
                {
                    HandleCombatSituation();
                }
                else
                {
                    // Continue journey
                    MoveCaravan();
                }

                // Check if destination reached
                if (HasReachedDestination())
                {
                    m_HasReachedDestination = true;
                    AnnounceCaravanArrival();
                    OnConclude();
                }

                // Periodic caravan chatter
                if (Utility.Random(100) < 5) // 5% chance per tick
                {
                    CaravanChatter();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MerchantCaravanScene] Error during caravan tick: {0}", ex.Message);
            }
        }

        public override void Cleanup()
        {
            try
            {
                if (!m_HasReachedDestination)
                {
                    AnnounceCaravanEnd();
                }

                // Clean up participants (let them continue normal behavior)
                foreach (PlayerBot merchant in m_Merchants)
                {
                    if (merchant != null && !merchant.Deleted)
                    {
                        RemoveParticipant(merchant);
                    }
                }

                foreach (PlayerBot guard in m_Guards)
                {
                    if (guard != null && !guard.Deleted)
                    {
                        RemoveParticipant(guard);
                    }
                }

                // Remove pack animals and their contents
                foreach (PackHorse horse in m_PackAnimals)
                {
                    if (horse != null && !horse.Deleted)
                    {
                        horse.Delete();
                    }
                }

                if (PlayerBotConfigurationManager.BehaviorSettings.EnableLogging && 
                    PlayerBotConfigurationManager.BehaviorSettings.VerboseEvents)
                {
                    Console.WriteLine("[{0}] [MerchantCaravanScene] Caravan scene '{1}' cleaned up", 
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), m_Name);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MerchantCaravanScene] Error during cleanup: {0}", ex.Message);
            }
        }
        #endregion

        #region Helper Methods
        private void GenerateWaypoints()
        {
            // Simple waypoint generation - could be enhanced with pathfinding
            Point3D start = m_Center;
            Point3D end = m_Destination;
            
            // Add start point
            m_Waypoints.Add(start);
            
            // Add intermediate waypoints
            int waypointCount = (int)(GetDistance(start, end) / 30); // One waypoint per 30 tiles
            waypointCount = Math.Max(1, Math.Min(waypointCount, 5)); // 1-5 waypoints
            
            for (int i = 1; i <= waypointCount; i++)
            {
                double progress = (double)i / (waypointCount + 1);
                int x = (int)(start.X + (end.X - start.X) * progress);
                int y = (int)(start.Y + (end.Y - start.Y) * progress);
                int z = m_Map.GetAverageZ(x, y);
                
                m_Waypoints.Add(new Point3D(x, y, z));
            }
            
            // Add destination
            m_Waypoints.Add(end);
        }

        private double GetDistance(Point3D p1, Point3D p2)
        {
            int deltaX = p1.X - p2.X;
            int deltaY = p1.Y - p2.Y;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private string GetDestinationName(Point3D dest, Map map)
        {
            // Simple destination naming - could be enhanced with actual city detection
            if (map == Map.Felucca || map == Map.Trammel)
            {
                if (dest.X >= 1416 && dest.X <= 1516 && dest.Y >= 1490 && dest.Y <= 1590)
                    return "Britain";
                else if (dest.X >= 2440 && dest.X <= 2540 && dest.Y >= 370 && dest.Y <= 470)
                    return "Minoc";
                else if (dest.X >= 2890 && dest.X <= 2990 && dest.Y >= 680 && dest.Y <= 780)
                    return "Vesper";
                else if (dest.X >= 1790 && dest.X <= 1890 && dest.Y >= 2630 && dest.Y <= 2730)
                    return "Trinsic";
            }
            
            return "distant lands";
        }

        private void EquipMerchant(PlayerBot merchant)
        {
            // Give merchants appropriate gear and items
            merchant.AddItem(new Robe(Utility.RandomNeutralHue()));
            merchant.AddItem(new Sandals());
            
            // Add some gold
            merchant.AddItem(new Gold(Utility.RandomMinMax(500, 1500)));
            
            // Add merchant-appropriate items
            if (Utility.RandomBool())
                merchant.AddItem(new Lantern());
                
            merchant.SayWithHue("The roads are long, but profit awaits!");
        }

        private void EquipGuard(PlayerBot guard)
        {
            // Guards get better equipment
            if (Utility.RandomBool())
                guard.AddItem(new ChainChest());
            else
                guard.AddItem(new StuddedChest());
                
            guard.AddItem(new ChainLegs());
            guard.AddItem(new ChainCoif());
            guard.AddItem(new ThighBoots());
            
            // Add weapon
            if (Utility.RandomBool())
                guard.AddItem(new Halberd());
            else
                guard.AddItem(new Bardiche());
                
            guard.SayWithHue("I'll protect this caravan with my life!");
        }

        private PackHorse SpawnPackAnimal()
        {
            Point3D spawnLoc = FindSpawnLocation(m_Center, 5);
            if (spawnLoc == Point3D.Zero)
                return null;
                
            PackHorse horse = new PackHorse();
            horse.MoveToWorld(spawnLoc, m_Map);
            AddSummon(horse);
            
            return horse;
        }

        private void LoadTradeGoods(PackHorse horse)
        {
            // Add various trade goods
            int itemCount = Utility.RandomMinMax(3, 8);
            
            for (int i = 0; i < itemCount; i++)
            {
                Item tradeGood = GenerateTradeGood();
                if (tradeGood != null)
                {
                    horse.AddItem(tradeGood);
                }
            }
        }

        private Item GenerateTradeGood()
        {
            switch (Utility.Random(8))
            {
                case 0: return new Cloth(Utility.RandomMinMax(10, 50));
                case 1: return new IronIngot(Utility.RandomMinMax(20, 100));
                case 2: return new Board(Utility.RandomMinMax(50, 200));
                case 3: return new Leather(Utility.RandomMinMax(20, 80));
                case 4: return new Bottle(Utility.RandomMinMax(5, 20));
                case 5: return new Bandage(Utility.RandomMinMax(20, 50));
                case 6: return new Arrow(Utility.RandomMinMax(50, 200));
                case 7: return new Gold(Utility.RandomMinMax(100, 500));
                default: return new Cloth(10);
            }
        }

        private void SetCaravanFormation()
        {
            // Arrange caravan in formation
            List<Mobile> allMembers = new List<Mobile>();
            allMembers.AddRange(m_Guards);
            allMembers.AddRange(m_Merchants);
            allMembers.AddRange(m_PackAnimals);

            // Position in a line formation
            for (int i = 0; i < allMembers.Count; i++)
            {
                if (allMembers[i] == null || allMembers[i].Deleted) continue;

                Point3D pos = new Point3D(
                    m_Center.X + (i * 2),
                    m_Center.Y,
                    m_Center.Z
                );

                allMembers[i].MoveToWorld(pos, m_Map);
                allMembers[i].Direction = GetDirectionTo(m_Destination);
            }
        }

        private Direction GetDirectionTo(Point3D target)
        {
            int deltaX = target.X - m_Center.X;
            int deltaY = target.Y - m_Center.Y;

            if (Math.Abs(deltaX) > Math.Abs(deltaY))
            {
                return deltaX > 0 ? Direction.East : Direction.West;
            }
            else
            {
                return deltaY > 0 ? Direction.South : Direction.North;
            }
        }

        private void MoveCaravan()
        {
            // Only update destinations every 30 seconds to allow AI time to move
            if (DateTime.Now - m_LastMovement < TimeSpan.FromSeconds(30))
                return;

            if (m_CurrentWaypointIndex >= m_Waypoints.Count)
                return;

            Point3D targetWaypoint = m_Waypoints[m_CurrentWaypointIndex];
            
            // Check if caravan members are close to current waypoint
            bool nearWaypoint = true;
            List<PlayerBot> allBots = new List<PlayerBot>();
            allBots.AddRange(m_Guards);
            allBots.AddRange(m_Merchants);
            
            foreach (PlayerBot member in allBots)
            {
                if (member != null && !member.Deleted)
                {
                    if (GetDistance(member.Location, targetWaypoint) > 10)
                    {
                        nearWaypoint = false;
                        break;
                    }
                }
            }
            
            if (nearWaypoint)
            {
                m_CurrentWaypointIndex++;
                if (m_CurrentWaypointIndex < m_Waypoints.Count)
                {
                    targetWaypoint = m_Waypoints[m_CurrentWaypointIndex];
                }
                else
                {
                    return; // Reached final destination
                }
            }

            // Set new destinations for all caravan members
            MoveTowardsTarget(targetWaypoint);
            m_LastMovement = DateTime.Now;
        }

        private void MoveTowardsTarget(Point3D target)
        {
            // Update center position towards target
            m_Center = target;

            // Set AI destinations for all caravan members to move towards the target
            List<PlayerBot> allBots = new List<PlayerBot>();
            allBots.AddRange(m_Guards);
            allBots.AddRange(m_Merchants);

            foreach (PlayerBot member in allBots)
            {
                if (member == null || member.Deleted) continue;

                // Set destination for the bot's AI
                if (member.AIObject != null)
                {
                    PlayerBotAI playerBotAI = member.AIObject as PlayerBotAI;
                    if (playerBotAI != null)
                    {
                        // Create a destination near the target with some formation spread
                        Point3D memberTarget = new Point3D(
                            target.X + Utility.RandomMinMax(-3, 3),
                            target.Y + Utility.RandomMinMax(-3, 3),
                            target.Z
                        );
                        
                        playerBotAI.SetDestination(memberTarget);
                        member.AIObject.Action = ActionType.Wander;
                    }
                }
            }

            // Move pack animals directly (they don't have PlayerBotAI)
            foreach (PackHorse animal in m_PackAnimals)
            {
                if (animal == null || animal.Deleted) continue;

                Point3D animalTarget = new Point3D(
                    target.X + Utility.RandomMinMax(-2, 2),
                    target.Y + Utility.RandomMinMax(-2, 2),
                    m_Map.GetAverageZ(target.X, target.Y)
                );

                // Use the animal's AI to move towards target
                if (animal.AIObject != null)
                {
                    animal.AIObject.Action = ActionType.Wander;
                    // For animals, we'll use the traditional approach since they don't have PlayerBotAI
                    if (!animal.InRange(animalTarget, 5))
                    {
                        Direction dir = animal.GetDirectionTo(animalTarget);
                        Direction runningDir = dir | Direction.Running; // Pack animals should also run to keep up
                        animal.AIObject.DoMove(runningDir);
                    }
                }
            }
        }

        private bool IsCaravanUnderAttack()
        {
            foreach (PlayerBot guard in m_Guards)
            {
                if (guard != null && !guard.Deleted && guard.Combatant != null)
                    return true;
            }

            foreach (PlayerBot merchant in m_Merchants)
            {
                if (merchant != null && !merchant.Deleted && merchant.Combatant != null)
                    return true;
            }

            return false;
        }

        private void HandleCombatSituation()
        {
            // Guards defend the caravan
            foreach (PlayerBot guard in m_Guards)
            {
                if (guard == null || guard.Deleted || !guard.Alive) continue;

                if (guard.Combatant == null)
                {
                    // Find nearest threat
                    Mobile threat = FindNearestThreat(guard);
                    if (threat != null)
                    {
                        guard.Combatant = threat;
                        guard.SayWithHue("Defend the caravan!");
                    }
                }
            }

            // Merchants try to flee or hide
            foreach (PlayerBot merchant in m_Merchants)
            {
                if (merchant == null || merchant.Deleted || !merchant.Alive) continue;

                if (merchant.Combatant == null && Utility.Random(100) < 20)
                {
                    merchant.SayWithHue("Help! We're under attack!");
                }
            }
        }

        private Mobile FindNearestThreat(PlayerBot guard)
        {
            Mobile nearest = null;
            double nearestDistance = double.MaxValue;

            foreach (Mobile m in guard.GetMobilesInRange(12))
            {
                if (m == null || m.Deleted || !m.Alive || m == guard) continue;
                
                // Don't attack caravan members
                bool isCaravanMember = false;
                foreach (PlayerBot bot in m_Guards)
                {
                    if (bot == m)
                    {
                        isCaravanMember = true;
                        break;
                    }
                }
                if (!isCaravanMember)
                {
                    foreach (PlayerBot bot in m_Merchants)
                    {
                        if (bot == m)
                        {
                            isCaravanMember = true;
                            break;
                        }
                    }
                }
                if (isCaravanMember) continue;
                
                // Check if this mobile is hostile
                bool isHostile = false;
                if (m.Combatant != null)
                {
                    foreach (PlayerBot bot in m_Guards)
                    {
                        if (bot == m.Combatant)
                        {
                            isHostile = true;
                            break;
                        }
                    }
                    if (!isHostile)
                    {
                        foreach (PlayerBot bot in m_Merchants)
                        {
                            if (bot == m.Combatant)
                            {
                                isHostile = true;
                                break;
                            }
                        }
                    }
                }
                
                if (isHostile)
                {
                    double distance = guard.GetDistanceToSqrt(m);
                    if (distance < nearestDistance)
                    {
                        nearest = m;
                        nearestDistance = distance;
                    }
                }
            }

            return nearest;
        }

        private bool HasReachedDestination()
        {
            return GetDistance(m_Center, m_Destination) < 10;
        }

        private void CaravanChatter()
        {
            List<PlayerBot> allBots = new List<PlayerBot>();
            allBots.AddRange(m_Merchants);
            allBots.AddRange(m_Guards);

            if (allBots.Count == 0) return;

            PlayerBot speaker = allBots[Utility.Random(allBots.Count)];
            if (speaker == null || speaker.Deleted) return;

            string[] chatter;
            if (m_Merchants.Contains(speaker))
            {
                chatter = new string[]
                {
                    "These goods should fetch a fine price!",
                    "The roads seem safe today.",
                    "I hope we reach our destination soon.",
                    "Business has been good lately."
                };
            }
            else
            {
                chatter = new string[]
                {
                    "Stay alert, everyone.",
                    "The roads can be dangerous.",
                    "Keep the caravan moving.",
                    "I see movement ahead."
                };
            }

            speaker.SayWithHue(chatter[Utility.Random(chatter.Length)]);
        }
        #endregion

        #region Announcements
        private void AnnounceCaravanStart()
        {
            AnnounceToArea("A merchant caravan passes by, heading to distant lands.");
        }

        private void AnnounceCaravanArrival()
        {
            AnnounceToArea("The merchant caravan has reached its destination safely.");
        }

        private void AnnounceCaravanEnd()
        {
            AnnounceToArea("The merchant caravan disappears into the distance.");
        }
        #endregion

        #region Admin Methods
        /// <summary>
        /// Get the destination point for this caravan
        /// </summary>
        public Point3D Destination
        {
            get { return m_Destination; }
        }

        /// <summary>
        /// Get the progress of the caravan (0.0 to 1.0)
        /// </summary>
        public double GetProgress()
        {
            if (m_Waypoints == null || m_Waypoints.Count == 0)
                return 0.0;

            return (double)m_CurrentWaypointIndex / (double)m_Waypoints.Count;
        }

        /// <summary>
        /// Get count of merchants in the caravan
        /// </summary>
        public int GetMerchantCount()
        {
            int count = 0;
            foreach (PlayerBot merchant in m_Merchants)
            {
                if (merchant != null && !merchant.Deleted)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Get count of guards in the caravan
        /// </summary>
        public int GetGuardCount()
        {
            int count = 0;
            foreach (PlayerBot guard in m_Guards)
            {
                if (guard != null && !guard.Deleted)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Get count of pack animals in the caravan
        /// </summary>
        public int GetPackAnimalCount()
        {
            int count = 0;
            foreach (PackHorse animal in m_PackAnimals)
            {
                if (animal != null && !animal.Deleted)
                    count++;
            }
            return count;
        }
        #endregion
    }
} 