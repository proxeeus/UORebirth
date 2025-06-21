using Server.Items;
using Server.Network;
using Server.Spells;
using System;
using System.Collections.Generic;
using System.Text;


namespace Server.Mobiles
{
    public class PlayerBot : BaseCreature
    {
        static Random m_Rnd = new Random();

        private PlayerBotPersona m_Persona ;
        private bool m_IsPlayerKiller;
        private bool m_PrefersMelee;
        private SkillName m_PreferedCombatSkill;

        #region Accessors
        [CommandProperty(AccessLevel.GameMaster)]
        public PlayerBotPersona.PlayerBotProfile PlayerBotProfile { get { return m_Persona.Profile; } set { m_Persona.Profile = value; } }
        [CommandProperty(AccessLevel.GameMaster)]
        public PlayerBotPersona.PlayerBotExperience PlayerBotExperience { get { return m_Persona.Experience; } set { m_Persona.Experience = value; } }

        public override bool AlwaysMurderer { get { return m_IsPlayerKiller; } }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool PrefersMelee { get { return m_PrefersMelee; } set { m_PrefersMelee = value; } }

        [CommandProperty(AccessLevel.GameMaster)]
        public SkillName PreferedCombatSkill { get { return m_PreferedCombatSkill; } set { m_PreferedCombatSkill = value; } }
        #endregion

        public static string[] m_GuildTypes = new string[] { "", " (Chaos)", " (Order)" };

        private bool IsPlayerKiller()
        {
            if (m_Persona.Profile == PlayerBotPersona.PlayerBotProfile.PlayerKiller)
                return true;
            return false;
        }


        #region Constructors
        public PlayerBot(AIType AI) : base(AI, FightMode.Agressor, 10, 1, 0.5, 0.75)
        {
            InitPersona();
            InitBody();
            InitStats();
            InitCombatSkills();
            InitOutfit();
            InitConsumables();
        }



        [Constructable]
        public PlayerBot() : this(AIType.AI_PlayerBot)
        {
        }

        public PlayerBot(Serial serial) : base(serial)
        {
        }

        #endregion

        #region Serializers
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write((int)0); // version 

            writer.Write((int)m_Persona.Experience);
            writer.Write((int)m_Persona.Profile);
            writer.Write((bool)m_IsPlayerKiller);
            writer.Write((bool)m_PrefersMelee);
            writer.Write((int)m_PreferedCombatSkill);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            m_Persona = new PlayerBotPersona();

            m_Persona.Experience = (PlayerBotPersona.PlayerBotExperience)reader.ReadInt();
            m_Persona.Profile = (PlayerBotPersona.PlayerBotProfile)reader.ReadInt();
            m_IsPlayerKiller = reader.ReadBool();
            m_PrefersMelee = reader.ReadBool();
            m_PreferedCombatSkill = (SkillName)reader.ReadInt();
        }
        #endregion

        #region Inits
        private void InitPersona()
        {
            // Note about Titles (Karma & Professions)
            // They do not need to be set at creation time. They're handled in Titles.cs, same way as real players.
            // So it'll be possible to see "The Glorious Lord Soandso, Grandmaster Swordsman" instead of traditional NPC titles.
            m_Persona = new PlayerBotPersona();

            switch (Utility.Random(3))
            {
                case 0:
                    m_Persona.Profile = PlayerBotPersona.PlayerBotProfile.PlayerKiller;
                    break;
                case 1:
                    m_Persona.Profile = PlayerBotPersona.PlayerBotProfile.Crafter;
                    break;
                case 2:
                    m_Persona.Profile = PlayerBotPersona.PlayerBotProfile.Adventurer;
                    break;
            }

            switch(Utility.Random(4))
            {
                case 0:
                    m_Persona.Experience = PlayerBotPersona.PlayerBotExperience.Newbie;
                    break;
                case 1:
                    m_Persona.Experience = PlayerBotPersona.PlayerBotExperience.Average;
                    break;
                case 2:
                    m_Persona.Experience = PlayerBotPersona.PlayerBotExperience.Proficient;
                    break;
                case 3:
                    m_Persona.Experience = PlayerBotPersona.PlayerBotExperience.Grandmaster;
                    break;
            }

            m_IsPlayerKiller = IsPlayerKiller();
            
            // Initialize Fame and Karma based on profile and experience
            InitFameAndKarma();
        }

        private void InitFameAndKarma()
        {
            int baseFame = 0;
            int baseKarma = 0;
            
            // Set base values based on experience level
            switch (m_Persona.Experience)
            {
                case PlayerBotPersona.PlayerBotExperience.Newbie:
                    baseFame = Utility.Random(0, 100);
                    baseKarma = Utility.Random(-50, 50);
                    break;
                case PlayerBotPersona.PlayerBotExperience.Average:
                    baseFame = Utility.Random(50, 300);
                    baseKarma = Utility.Random(-100, 100);
                    break;
                case PlayerBotPersona.PlayerBotExperience.Proficient:
                    baseFame = Utility.Random(200, 600);
                    baseKarma = Utility.Random(-150, 150);
                    break;
                case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                    baseFame = Utility.Random(400, 1000);
                    baseKarma = Utility.Random(-200, 200);
                    break;
            }
            
            // Adjust based on profile
            switch (m_Persona.Profile)
            {
                case PlayerBotPersona.PlayerBotProfile.PlayerKiller:
                    // Player killers always have very negative karma
                    baseKarma = Utility.Random(-200, -120); // Dread Lord/Lady range
                    // PKs can have varying fame (infamy)
                    baseFame = Utility.Random(100, 500);
                    break;
                    
                case PlayerBotPersona.PlayerBotProfile.Crafter:
                    // Crafters tend to be more neutral to positive
                    baseKarma = Utility.Random(-50, 100);
                    baseFame = Utility.Random(50, 300);
                    break;
                    
                case PlayerBotPersona.PlayerBotProfile.Adventurer:
                    // Adventurers can have more varied fame/karma
                    baseKarma = Utility.Random(-100, 150);
                    baseFame = Utility.Random(100, 400);
                    break;
            }
            
            // Ensure values stay within bounds (-10000 to 10000)
            Fame = Math.Max(-10000, Math.Min(10000, baseFame));
            Karma = Math.Max(-10000, Math.Min(10000, baseKarma));
        }

        public virtual void InitBody()
        {
            Hue = Utility.RandomSkinHue();

            if (Body == 0 && (Name == null || Name.Length <= 0))
            {
                if (Female = Utility.RandomBool())
                {
                    Body = 401;
                    Name = NameList.RandomName("female");
                }
                else
                {
                    Body = 400;
                    Name = NameList.RandomName("male");
                }
            }
        }

        private void InitStats()
        {
            switch(m_Persona.Experience)
            {
                case PlayerBotPersona.PlayerBotExperience.Newbie:
                    SetStr(30, 35);
                    SetDex(30, 35);
                    SetInt(30, 35);
                    break;
                case PlayerBotPersona.PlayerBotExperience.Average:
                    SetStr(45, 65);
                    SetDex(45, 65);
                    SetInt(45, 65);
                    break;
                case PlayerBotPersona.PlayerBotExperience.Proficient:
                    SetStr(70, 85);
                    SetDex(70, 85);
                    SetInt(70, 85);
                    break;
                case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                    SetStr(95, 100);
                    SetDex(95, 100);
                    SetInt(95, 100);
                    break;
            }

            SetHits(Str);

        }

        private void InitCombatSkills()
        {
            m_PrefersMelee = Utility.RandomBool();
            var preferedMeleeSkill = Utility.Random(4);
            SkillName skill = 0;

            if(m_PrefersMelee)
            {
                if (preferedMeleeSkill == 0)
                    skill = SkillName.Swords;
                else if (preferedMeleeSkill == 1)
                    skill = SkillName.Macing;
                else if (preferedMeleeSkill == 2)
                    skill = SkillName.Fencing;
                else if (preferedMeleeSkill == 3)
                    skill = SkillName.Wrestling;
            }
            else
            {
                skill = SkillName.Archery;
            }

            PreferedCombatSkill = skill;

            switch(m_Persona.Experience)
            {
                case PlayerBotPersona.PlayerBotExperience.Newbie:
                    SetSkill(SkillName.Tactics, 15, 35.5);
                    SetSkill(SkillName.MagicResist, 15, 35.5);
                    SetSkill(SkillName.Parry, 15, 35.5);
                    SetSkill(skill, 15, 35.5);
                    break;
                case PlayerBotPersona.PlayerBotExperience.Average:
                    SetSkill(SkillName.Tactics, 45, 55.5);
                    SetSkill(SkillName.MagicResist, 45, 55.5);
                    SetSkill(SkillName.Parry, 45, 55.5);
                    SetSkill(skill, 45, 55.5);
                    break;
                case PlayerBotPersona.PlayerBotExperience.Proficient:
                    SetSkill(SkillName.Tactics, 65.5, 85);
                    SetSkill(SkillName.MagicResist, 65.5, 85);
                    SetSkill(SkillName.Parry, 65.5, 85);
                    SetSkill(skill, 65.5, 85);
                    break;
                case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                    SetSkill(SkillName.Tactics, 95, 100);
                    SetSkill(SkillName.MagicResist, 95, 100);
                    SetSkill(SkillName.Parry, 95, 100);
                    SetSkill(skill, 95, 100);
                    break;
            }
        }

        public virtual void InitOutfit()
        {
            InitHair();
            InitWeapon();
            InitArmor();
            InitWearable();
        }

        private void InitArmor()
        {
            // Determine armor type based on skills and stats
            ArmorMaterialType armorType = DetermineArmorType();
            
            // Add armor pieces based on the determined type
            switch (armorType)
            {
                case ArmorMaterialType.Leather:
                    AddItem(new LeatherChest());
                    AddItem(new LeatherArms());
                    AddItem(new LeatherGloves());
                    AddItem(new LeatherGorget());
                    AddItem(new LeatherLegs());
                    AddItem(GenerateHelmet(armorType));
                    AddItem(new Boots());  // Leather armor gets standard boots
                    break;
                    
                case ArmorMaterialType.Studded:
                    AddItem(new StuddedChest());
                    AddItem(new StuddedArms());
                    AddItem(new StuddedGloves());
                    AddItem(new StuddedGorget());
                    AddItem(new StuddedLegs());
                    AddItem(GenerateHelmet(armorType));
                    AddItem(new Boots());  // Studded armor gets standard boots
                    break;
                    
                case ArmorMaterialType.Chainmail:
                    AddItem(new ChainChest());
                    AddItem(new ChainLegs());
                    AddItem(new ChainCoif());
                    AddItem(new RingmailGloves());
                    AddItem(new StuddedGorget());
                    AddItem(GenerateHelmet(armorType));
                    AddItem(new ThighBoots());  // Chainmail gets thigh boots for better protection
                    break;
                    
                case ArmorMaterialType.Ringmail:
                    AddItem(new RingmailChest());
                    AddItem(new RingmailLegs());
                    AddItem(new RingmailArms());
                    AddItem(new RingmailGloves());
                    AddItem(new ChainCoif());
                    AddItem(new PlateGorget());
                    AddItem(GenerateHelmet(armorType));
                    AddItem(new ThighBoots());  // Ringmail gets thigh boots for better protection
                    break;
                    
                case ArmorMaterialType.Plate:
                    AddItem(new PlateChest());
                    AddItem(new PlateLegs());
                    AddItem(new PlateArms());
                    AddItem(new PlateGloves());
                    AddItem(new PlateGorget());
                    AddItem(GenerateHelmet(armorType));
                    AddItem(new ThighBoots());  // Plate armor gets thigh boots for maximum protection
                    break;
            }

            // Add shield if the bot has parry skill
            if (Skills[SkillName.Parry].Base > 0)
            {
                BaseShield shield = GenerateShield();
                if (shield != null)
                    AddItem(shield);
            }
        }

        private BaseArmor GenerateHelmet(ArmorMaterialType armorType)
        {
            // Create a pool of appropriate helmets based on armor type
            var helmetPool = new List<BaseArmor>();
            
            switch (armorType)
            {
                case ArmorMaterialType.Leather:
                    helmetPool.Add(new LeatherCap());
                    helmetPool.Add(new OrcHelm());
                    break;
                    
                case ArmorMaterialType.Studded:
                    helmetPool.Add(new LeatherCap());
                    helmetPool.Add(new Bascinet());
                    helmetPool.Add(new OrcHelm());
                    break;
                    
                case ArmorMaterialType.Chainmail:
                    helmetPool.Add(new ChainCoif());
                    helmetPool.Add(new Bascinet());
                    helmetPool.Add(new CloseHelm());
                    helmetPool.Add(new NorseHelm());
                    helmetPool.Add(new OrcHelm());
                    break;
                    
                case ArmorMaterialType.Ringmail:
                    helmetPool.Add(new Bascinet());
                    helmetPool.Add(new CloseHelm());
                    helmetPool.Add(new NorseHelm());
                    helmetPool.Add(new OrcHelm());
                    helmetPool.Add(new PlateHelm());
                    break;
                    
                case ArmorMaterialType.Plate:
                    helmetPool.Add(new PlateHelm());
                    helmetPool.Add(new CloseHelm());
                    helmetPool.Add(new NorseHelm());
                    helmetPool.Add(new Bascinet());
                    helmetPool.Add(new OrcHelm());
                    break;
            }
            
            // Return a random helmet from the pool, or LeatherCap as fallback
            if (helmetPool.Count > 0)
            {
                return helmetPool[Utility.Random(helmetPool.Count)];
            }
            
            return new LeatherCap(); // Fallback
        }

        private ArmorMaterialType DetermineArmorType()
        {
            // Get base stats
            int str = this.Str;
            int dex = this.Dex;
            
            // Get relevant skills
            double parry = Skills[SkillName.Parry].Base;
            double armsLore = Skills[SkillName.ArmsLore].Base;
            
            // Consider experience level
            switch (m_Persona.Experience)
            {
                case PlayerBotPersona.PlayerBotExperience.Newbie:
                    return ArmorMaterialType.Leather;
                    
                case PlayerBotPersona.PlayerBotExperience.Average:
                    if (str >= 40 && dex >= 40)
                        return ArmorMaterialType.Studded;
                    return ArmorMaterialType.Leather;
                    
                case PlayerBotPersona.PlayerBotExperience.Proficient:
                    if (str >= 60 && dex >= 40)
                        return ArmorMaterialType.Chainmail;
                    if (str >= 50 && dex >= 50)
                        return ArmorMaterialType.Studded;
                    return ArmorMaterialType.Leather;
                    
                case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                    if (str >= 80 && dex >= 40)
                        return ArmorMaterialType.Plate;
                    if (str >= 70 && dex >= 50)
                        return ArmorMaterialType.Ringmail;
                    if (str >= 60 && dex >= 60)
                        return ArmorMaterialType.Chainmail;
                    return ArmorMaterialType.Studded;
                    
                default:
                    return ArmorMaterialType.Leather;
            }
        }

        private void InitWeapon()
        {
            switch (m_Persona.Experience)
            {
                case PlayerBotPersona.PlayerBotExperience.Newbie:
                case PlayerBotPersona.PlayerBotExperience.Average:
                case PlayerBotPersona.PlayerBotExperience.Proficient:
                case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                    if (m_PrefersMelee)
                    {
                        if(PreferedCombatSkill != SkillName.Wrestling)
                        {
                            BaseWeapon weapon = GenerateWeapon();
                            if (weapon != null)
                            {
                                AddItem(weapon);
                            }
                        }    
                    }
                    else
                    {
                        // Add appropriate ranged weapon based on experience level
                        BaseRanged rangedWeapon = null;
                        
                        if (m_Persona.Experience == PlayerBotPersona.PlayerBotExperience.Grandmaster && Utility.RandomBool())
                        {
                            // T2A weapons for grandmasters
                            switch (Utility.Random(4))
                            {
                                case 0:
                                    rangedWeapon = new JukaBow();
                                    break;
                                case 1:
                                    rangedWeapon = new RepeatingCrossbow();
                                    break;
                                case 2:
                                    rangedWeapon = new CompositeBow();
                                    break;
                                case 3:
                                    rangedWeapon = new HeavyCrossbow();
                                    break;
                            }
                        }
                        else
                        {
                            // Classic ranged weapons
                            if (Utility.RandomBool())
                            {
                                rangedWeapon = new Bow();
                            }
                            else
                            {
                                rangedWeapon = new Crossbow();
                            }
                        }
                        
                        AddItem(rangedWeapon);
                        
                        // Add appropriate ammunition based on weapon type
                        if (rangedWeapon is Bow || rangedWeapon is CompositeBow || rangedWeapon is JukaBow)
                        {
                            // Bow-type weapons use arrows
                            PackItem(new Arrow(Utility.Random(100, 200)));
                        }
                        else if (rangedWeapon is Crossbow || rangedWeapon is HeavyCrossbow || rangedWeapon is RepeatingCrossbow)
                        {
                            // Crossbow-type weapons use bolts
                            PackItem(new Bolt(Utility.Random(100, 200)));
                        }
                        
                        // Add backup weapon
                        PackItem(new Dagger());
                    }
                    break;
            }
        }

        private BaseWeapon GenerateWeapon()
        {
            var weaponPool = new List<BaseWeapon>();
            BaseWeapon weapon = null;

            if (PreferedCombatSkill == SkillName.Swords)
            {
                if (Str >= 25)
                {
                    weaponPool.Add(new Broadsword());
                    weaponPool.Add(new Cutlass());
                    weaponPool.Add(new Katana());
                    weaponPool.Add(new Scimitar());
                    weaponPool.Add(new Kryss());
                    weaponPool.Add(new Axe());
                    weaponPool.Add(new BattleAxe());
                }
                if (Str >= 30)
                {
                    weaponPool.Add(new DoubleAxe());
                    weaponPool.Add(new LargeBattleAxe());
                }
                if (Str >= 35)
                {
                    weaponPool.Add(new Longsword());
                    weaponPool.Add(new ExecutionersAxe());
                    weaponPool.Add(new TwoHandedAxe());
                    weaponPool.Add(new Bardiche());
                    weaponPool.Add(new Halberd());
                }
                if (Str >= 40)
                {
                    weaponPool.Add(new VikingSword());
                    weaponPool.Add(new WarAxe());
                }
            }
            else if (PreferedCombatSkill == SkillName.Macing)
            {
                if(Str >= 10)
                {
                    weaponPool.Add(new Club());
                }
                if(Str >= 20)
                {
                    weaponPool.Add(new Mace());
                    weaponPool.Add(new Maul());
                }
                if(Str >= 30)
                {
                    weaponPool.Add(new WarMace());
                }
                if(Str >= 35)
                {
                    weaponPool.Add(new HammerPick());
                }
                if(Str >= 40)
                {
                    weaponPool.Add(new Scepter());
                    weaponPool.Add(new WarHammer());
                }
            }
            else if (PreferedCombatSkill == SkillName.Fencing)
            {
                if(Str >= 10)
                {
                    weaponPool.Add(new Pitchfork());
                }
                if(Str >= 15)
                {
                    weaponPool.Add(new ShortSpear());
                }
                if(Str >= 30)
                {
                    weaponPool.Add(new Spear());
                    weaponPool.Add(new BladedStaff());
                    weaponPool.Add(new DoubleBladedStaff());
                    weaponPool.Add(new TribalSpear());
                }
                if(Str >= 35)
                {
                    weaponPool.Add(new WarFork());
                }
                if(Str >= 50)
                {
                    weaponPool.Add(new Pike());
                }
            }
            else if (PreferedCombatSkill == SkillName.Wrestling)
            {
                // Wrestling doesn't use weapons, but we could add some utility items
                // For now, we'll skip weapon assignment for wrestlers
                return null;
            }
            else if (PreferedCombatSkill == SkillName.Archery)
            {
                // Archery is handled in InitWeapon(), not here
                return null;
            }

            // Add Knives for utility (lower strength requirements)
            if (Str >= 10)
            {
                weaponPool.Add(new Dagger());
                weaponPool.Add(new ButcherKnife());
                weaponPool.Add(new Cleaver());
            }
            if (Str >= 15)
            {
                weaponPool.Add(new SkinningKnife());
            }

            // Add Staves (magical weapons)
            if (Str >= 10)
            {
                weaponPool.Add(new QuarterStaff());
            }
            if (Str >= 20)
            {
                weaponPool.Add(new BlackStaff());
                weaponPool.Add(new GnarledStaff());
            }
            if (Str >= 25)
            {
                weaponPool.Add(new ShepherdsCrook());
            }

            // Add Pole Arms (high strength requirements)
            if (Str >= 35)
            {
                weaponPool.Add(new Scythe());
            }

            if (weaponPool.Count > 0)
            {
                var weaponIndex = m_Rnd.Next(weaponPool.Count);
                weapon = weaponPool[weaponIndex];
            }
            
            return weapon;
        }

        private void InitConsumables()
        {
            // Base consumables for all bots
            PackItem(new Bandage(Utility.Random(10, 25)));
            
            // Food items
            PackItem(new Apple(Utility.Random(3, 8)));
            PackItem(new BreadLoaf(Utility.Random(2, 5)));
            
            // Experience-based consumables
            switch (m_Persona.Experience)
            {
                case PlayerBotPersona.PlayerBotExperience.Newbie:
                    // Basic supplies for newbies
                    PackItem(new Bandage(Utility.Random(5, 15)));
                    PackItem(new Apple(Utility.Random(2, 4)));
                    PackItem(new BreadLoaf(Utility.Random(1, 3)));
                    break;
                    
                case PlayerBotPersona.PlayerBotExperience.Average:
                    // Moderate supplies
                    PackItem(new Bandage(Utility.Random(15, 30)));
                    PackItem(new Apple(Utility.Random(4, 8)));
                    PackItem(new BreadLoaf(Utility.Random(2, 5)));
                    PackItem(new CheeseWheel(Utility.Random(1, 3)));
                    PackItem(new CookedBird(Utility.Random(1, 3)));
                    
                    // Basic potions
                    if (Utility.RandomBool())
                        PackItem(new LesserHealPotion());
                    if (Utility.RandomBool())
                        PackItem(new LesserCurePotion());
                    break;
                    
                case PlayerBotPersona.PlayerBotExperience.Proficient:
                    // Good supplies
                    PackItem(new Bandage(Utility.Random(25, 50)));
                    PackItem(new Apple(Utility.Random(6, 12)));
                    PackItem(new BreadLoaf(Utility.Random(3, 7)));
                    PackItem(new CheeseWheel(Utility.Random(2, 5)));
                    PackItem(new CookedBird(Utility.Random(2, 5)));
                    PackItem(new Ham(Utility.Random(1, 3)));
                    PackItem(new RoastPig(Utility.Random(1, 2)));
                    
                    // Potions
                    PackItem(new HealPotion());
                    PackItem(new CurePotion());
                    if (Utility.RandomBool())
                        PackItem(new AgilityPotion());
                    if (Utility.RandomBool())
                        PackItem(new StrengthPotion());
                    break;
                    
                case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                    // Premium supplies
                    PackItem(new Bandage(Utility.Random(50, 100)));
                    PackItem(new Apple(Utility.Random(10, 20)));
                    PackItem(new BreadLoaf(Utility.Random(5, 10)));
                    PackItem(new CheeseWheel(Utility.Random(3, 7)));
                    PackItem(new CookedBird(Utility.Random(3, 7)));
                    PackItem(new Ham(Utility.Random(2, 5)));
                    PackItem(new RoastPig(Utility.Random(2, 4)));
                    PackItem(new Sausage(Utility.Random(1, 3)));
                    
                    // Premium potions
                    PackItem(new GreaterHealPotion());
                    PackItem(new GreaterCurePotion());
                    PackItem(new GreaterAgilityPotion());
                    PackItem(new GreaterStrengthPotion());
                    if (Utility.RandomBool())
                        PackItem(new RefreshPotion());
                    if (Utility.RandomBool())
                        PackItem(new LesserPoisonPotion());
                    break;
            }
            
            // Persona-based additional items
            switch (m_Persona.Profile)
            {
                case PlayerBotPersona.PlayerBotProfile.PlayerKiller:
                    // PKs carry more combat-oriented supplies
                    PackItem(new Bandage(Utility.Random(10, 25)));
                    if (m_Persona.Experience >= PlayerBotPersona.PlayerBotExperience.Proficient)
                    {
                        PackItem(new GreaterHealPotion());
                        PackItem(new GreaterCurePotion());
                        if (Utility.RandomBool())
                            PackItem(new GreaterAgilityPotion());
                    }
                    break;
                    
                case PlayerBotPersona.PlayerBotProfile.Crafter:
                    // Crafters carry more food and basic supplies
                    PackItem(new Apple(Utility.Random(5, 10)));
                    PackItem(new BreadLoaf(Utility.Random(3, 6)));
                    PackItem(new CheeseWheel(Utility.Random(2, 4)));
                    break;
                    
                case PlayerBotPersona.PlayerBotProfile.Adventurer:
                    // Adventurers carry balanced supplies
                    PackItem(new Bandage(Utility.Random(15, 30)));
                    PackItem(new CookedBird(Utility.Random(2, 5)));
                    PackItem(new Ham(Utility.Random(1, 3)));
                    if (m_Persona.Experience >= PlayerBotPersona.PlayerBotExperience.Average)
                    {
                        PackItem(new HealPotion());
                        PackItem(new CurePotion());
                    }
                    break;
            }
        }

        private void InitHair()
        {
            var hairHue = Utility.RandomHairHue();

            Utility.AssignRandomHair(this, hairHue);

            if (!Female)
            {
                if (Utility.RandomBool())
                    AddRandomFacialHair(hairHue);   // Keep facial hair hue consistent with base hair hue
            }
        }

        // Helper for mostly realistic, sometimes wonky hues
        private int GetClothHue()
        {
            // 80% realistic, 20% wonky
            if (Utility.RandomDouble() < 0.8)
                return Utility.RandomNeutralHue(); // Realistic
            else
                return Utility.RandomList(33, 38, 1150, 1175, 1281, 1359, 1372, 1401, 2117, 2125, 1153, 1161, 1166, 1170, 1172, 1177, 1195, 1196, 1197, 1198, 1199, 1200, 1201, 1202, 1203, 1204, 1205, 1206, 1207, 1208, 1209, 1210, 1211, 1212, 1213, 1214, 1215, 1216, 1217, 1218, 1219, 1220, 1221, 1222, 1223, 1224, 1225, 1226, 1227, 1228, 1229, 1230, 1231, 1232, 1233, 1234, 1235, 1236, 1237, 1238, 1239, 1240, 1241, 1242, 1243, 1244, 1245, 1246, 1247, 1248, 1249, 1250, 1251, 1252, 1253, 1254, 1255, 1256, 1257, 1258, 1259, 1260, 1261, 1262, 1263, 1264, 1265, 1266, 1267, 1268, 1269, 1270, 1271, 1272, 1273, 1274, 1275, 1276, 1277, 1278, 1279, 1280, 1281, 1282, 1283, 1284, 1285, 1286, 1287, 1288, 1289, 1290, 1291, 1292, 1293, 1294, 1295, 1296, 1297, 1298, 1299, 1300, 1301, 1302, 1303, 1304, 1305, 1306, 1307, 1308, 1309, 1310, 1311, 1312, 1313, 1314, 1315, 1316, 1317, 1318, 1319, 1320, 1321, 1322, 1323, 1324, 1325, 1326, 1327, 1328, 1329, 1330, 1331, 1332, 1333, 1334, 1335, 1336, 1337, 1338, 1339, 1340, 1341, 1342, 1343, 1344, 1345, 1346, 1347, 1348, 1349, 1350, 1351, 1352, 1353, 1354, 1355, 1356, 1357, 1358, 1359, 1360, 1361, 1362, 1363, 1364, 1365, 1366, 1367, 1368, 1369, 1370, 1371, 1372, 1373, 1374, 1375, 1376, 1377, 1378, 1379, 1380, 1381, 1382, 1383, 1384, 1385, 1386, 1387, 1388, 1389, 1390, 1391, 1392, 1393, 1394, 1395, 1396, 1397, 1398, 1399, 1400, 1401, 1402, 1403, 1404, 1405, 1406, 1407, 1408, 1409, 1410, 1411, 1412, 1413, 1414, 1415, 1416, 1417, 1418, 1419, 1420, 1421, 1422, 1423, 1424, 1425, 1426, 1427, 1428, 1429, 1430, 1431, 1432, 1433, 1434, 1435, 1436, 1437, 1438, 1439, 1440, 1441, 1442, 1443, 1444, 1445, 1446, 1447, 1448, 1449, 1450, 1451, 1452, 1453, 1454, 1455, 1456, 1457, 1458, 1459, 1460, 1461, 1462, 1463, 1464, 1465, 1466, 1467, 1468, 1469, 1470, 1471, 1472, 1473, 1474, 1475, 1476, 1477, 1478, 1479, 1480, 1481, 1482, 1483, 1484, 1485, 1486, 1487, 1488, 1489, 1490, 1491, 1492, 1493, 1494, 1495, 1496, 1497, 1498, 1499, 1500, 1501, 1502, 1503, 1504, 1505, 1506, 1507, 1508, 1509, 1510, 1511, 1512, 1513, 1514, 1515, 1516, 1517, 1518, 1519, 1520, 1521, 1522, 1523, 1524, 1525, 1526, 1527, 1528, 1529, 1530, 1531, 1532, 1533, 1534, 1535, 1536, 1537, 1538, 1539, 1540, 1541, 1542, 1543, 1544, 1545, 1546, 1547, 1548, 1549, 1550, 1551, 1552, 1553, 1554, 1555, 1556, 1557, 1558, 1559, 1560, 1561, 1562, 1563, 1564, 1565, 1566, 1567, 1568, 1569, 1570, 1571, 1572, 1573, 1574, 1575, 1576, 1577, 1578, 1579, 1580, 1581, 1582, 1583, 1584, 1585, 1586, 1587, 1588, 1589, 1590, 1591, 1592, 1593, 1594, 1595, 1596, 1597, 1598, 1599, 1600, 1601, 1602, 1603, 1604, 1605, 1606, 1607, 1608, 1609, 1610, 1611, 1612, 1613, 1614, 1615, 1616, 1617, 1618, 1619, 1620, 1621, 1622, 1623, 1624, 1625, 1626, 1627, 1628, 1629, 1630, 1631, 1632, 1633, 1634, 1635, 1636, 1637, 1638, 1639, 1640, 1641, 1642, 1643, 1644, 1645, 1646, 1647, 1648, 1649, 1650, 1651, 1652, 1653, 1654, 1655, 1656, 1657, 1658, 1659, 1660, 1661, 1662, 1663, 1664, 1665, 1666, 1667, 1668, 1669, 1670, 1671, 1672, 1673, 1674, 1675, 1676, 1677, 1678, 1679, 1680, 1681, 1682, 1683, 1684, 1685, 1686, 1687, 1688, 1689, 1690, 1691, 1692, 1693, 1694, 1695, 1696, 1697, 1698, 1699, 1700, 1701, 1702, 1703, 1704, 1705, 1706, 1707, 1708, 1709, 1710, 1711, 1712, 1713, 1714, 1715, 1716, 1717, 1718, 1719, 1720, 1721, 1722, 1723, 1724, 1725, 1726, 1727, 1728, 1729, 1730, 1731, 1732, 1733, 1734, 1735, 1736, 1737, 1738, 1739, 1740, 1741, 1742, 1743, 1744, 1745, 1746, 1747, 1748, 1749, 1750, 1751, 1752, 1753, 1754, 1755, 1756, 1757, 1758, 1759, 1760, 1761, 1762, 1763, 1764, 1765, 1766, 1767, 1768, 1769, 1770, 1771, 1772, 1773, 1774, 1775, 1776, 1777, 1778, 1779, 1780, 1781, 1782, 1783, 1784, 1785, 1786, 1787, 1788, 1789, 1790, 1791, 1792, 1793, 1794, 1795, 1796, 1797, 1798, 1799, 1800, 1801, 1802, 1803, 1804, 1805, 1806, 1807, 1808, 1809, 1810, 1811, 1812, 1813, 1814, 1815, 1816, 1817, 1818, 1819, 1820, 1821, 1822, 1823, 1824, 1825, 1826, 1827, 1828, 1829, 1830, 1831, 1832, 1833, 1834, 1835, 1836, 1837, 1838, 1839, 1840, 1841, 1842, 1843, 1844, 1845, 1846, 1847, 1848, 1849, 1850, 1851, 1852, 1853, 1854, 1855, 1856, 1857, 1858, 1859, 1860, 1861, 1862, 1863, 1864, 1865, 1866, 1867, 1868, 1869, 1870, 1871, 1872, 1873, 1874, 1875, 1876, 1877, 1878, 1879, 1880, 1881, 1882, 1883, 1884, 1885, 1886, 1887, 1888, 1889, 1890, 1891, 1892, 1893, 1894, 1895, 1896, 1897, 1898, 1899, 1900, 1901, 1902, 1903, 1904, 1905, 1906, 1907, 1908, 1909, 1910, 1911, 1912, 1913, 1914, 1915, 1916, 1917, 1918, 1919, 1920, 1921, 1922, 1923, 1924, 1925, 1926, 1927, 1928, 1929, 1930, 1931, 1932, 1933, 1934, 1935, 1936, 1937, 1938, 1939, 1940, 1941, 1942, 1943, 1944, 1945, 1946, 1947, 1948, 1949, 1950, 1951, 1952, 1953, 1954, 1955, 1956, 1957, 1958, 1959, 1960, 1961, 1962, 1963, 1964, 1965, 1966, 1967, 1968, 1969, 1970, 1971, 1972, 1973, 1974, 1975, 1976, 1977, 1978, 1979, 1980, 1981, 1982, 1983, 1984, 1985, 1986, 1987, 1988, 1989, 1990, 1991, 1992, 1993, 1994, 1995, 1996, 1997, 1998, 1999, 2000, 2001, 2002, 2003, 2004, 2005, 2006, 2007, 2008, 2009, 2010, 2011, 2012, 2013, 2014, 2015, 2016, 2017, 2018, 2019, 2020, 2021, 2022, 2023, 2024, 2025, 2026, 2027, 2028, 2029, 2030, 2031, 2032, 2033, 2034, 2035, 2036, 2037, 2038, 2039, 2040, 2041, 2042, 2043, 2044, 2045, 2046, 2047, 2048, 2049, 2050, 2051, 2052, 2053, 2054, 2055, 2056, 2057, 2058, 2059, 2060, 2061, 2062, 2063, 2064, 2065, 2066, 2067, 2068, 2069, 2070, 2071, 2072, 2073, 2074, 2075, 2076, 2077, 2078, 2079, 2080, 2081, 2082, 2083, 2084, 2085, 2086, 2087, 2088, 2089, 2090, 2091, 2092, 2093, 2094, 2095, 2096, 2097, 2098, 2099, 2100, 2101, 2102, 2103, 2104, 2105, 2106, 2107, 2108, 2109, 2110, 2111, 2112, 2113, 2114, 2115, 2116, 2117, 2118, 2119, 2120, 2121, 2122, 2123, 2124, 2125); // Wonky
        }

        private void InitWearable()
        {
            // 30% chance to be plain (no extra wearables)
            if (Utility.RandomDouble() < 0.3)
                return;

            // Pool of wearable types (robes, sashes, kilts, surcoats, etc.)
            var wearableOptions = new List<Action>()
            {
                () => AddItem(new Cloak(GetClothHue())),
                () => AddItem(new Robe(GetClothHue())),
                () => AddItem(new Surcoat(GetClothHue())),
                () => AddItem(new Tunic(GetClothHue())),
                () => AddItem(new BodySash(GetClothHue())),
                () => AddItem(new Kilt(GetClothHue())),
                () => AddItem(new Skirt(GetClothHue())),
                () => AddItem(new FancyDress(GetClothHue())),
                () => AddItem(new PlainDress(GetClothHue())),
                () => AddItem(new JesterSuit(GetClothHue())),
                () => AddItem(new Doublet(GetClothHue())),
                () => AddItem(new HalfApron(GetClothHue())),
                () => AddItem(new FullApron(GetClothHue())),
            };

            // 1-3 extra wearables, randomly chosen, no duplicates
            int count = Utility.RandomMinMax(1, 3);
            var used = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int idx;
                do { idx = Utility.Random(wearableOptions.Count); } while (used.Contains(idx));
                used.Add(idx);
                wearableOptions[idx]();
            }
        }

        #endregion

        #region Overrides
        public override void OnSingleClick(Mobile from)
        {
            if (Deleted || (AccessLevel == AccessLevel.Player && DisableHiddenSelfClick && Hidden && from == this))
                return;

            if (Mobile.GuildClickMessage)
            {
                Server.Guilds.Guild guild = this.Guild as Server.Guilds.Guild;

                if (guild != null && (this.DisplayGuildTitle || guild.Type != Server.Guilds.GuildType.Regular))
                {
                    string title = GuildTitle;
                    string type;

                    if (title == null)
                        title = "";
                    else
                        title = title.Trim();

                    if (guild.Type >= 0 && (int)guild.Type < m_GuildTypes.Length)
                        type = m_GuildTypes[(int)guild.Type];
                    else
                        type = "";

                    string text = String.Format(title.Length <= 0 ? "[{1}]{2}" : "[{0}, {1}]{2}", title, guild.Abbreviation, type);

                    PrivateOverheadMessage(MessageType.Regular, SpeechHue, true, text, from.NetState);
                }
            }

            int hue;

            if (NameHue != -1)
                hue = NameHue;
            else if (AccessLevel > AccessLevel.Player)
                hue = 11;
            else
                hue = Notoriety.GetHue(Notoriety.Compute(from, this));

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if ((Karma >= (int)Noto.LordLady || Karma <= (int)Noto.Dark))
                sb.Append(Female ? "Lady " : "Lord ");

            sb.Append(Name);

            if (ClickTitle && Title != null && Title.Length > 0)
            {
                sb.Append(' ');
                sb.Append(Title);
            }

            if (Frozen || Paralyzed || (this.Spell != null && this.Spell is Spell && this.Spell.IsCasting && ((Spell)this.Spell).BlocksMovement))
                sb.Append(" (frozen)");

            if (Blessed)
                sb.Append(" (invulnerable)");

            PrivateOverheadMessage(MessageType.Label, hue, Mobile.AsciiClickMessage, sb.ToString(), from.NetState);
        }

        public override bool HandlesOnSpeech(Mobile from)
        {
            return true;
        }
        public override bool CanBeRenamedBy(Mobile from)
        {
            return false;
        }
        public override void OnSpeech(SpeechEventArgs e)
        {
            if (!e.Handled && e.Mobile.InRange(this, 4))
            {
                if (e.HasKeyword(0x003B) || e.HasKeyword(0x0162))
                {
                    e.Handled = true;
                    if (this.Controled)
                    {
                        if (this.ControlMaster == e.Mobile)
                        {
                        }
                        else
                        {
                            Say("I don't think I've agreed to work with you...yet?");
                        }
                    }
                }
            }

            base.OnSpeech(e);
        }

        #endregion

        public virtual Mobile GetOwner()
        {
            if (!Controled || Deleted)
                return null;
            Mobile owner = ControlMaster;
            if (owner == null || owner.Deleted)
            {
                Say(1005653); // Hmmm.  I seem to have lost my master. 
                Delta(MobileDelta.Noto);
                SetControlMaster(null);
                SummonMaster = null;

                BondingBegin = DateTime.MinValue;
                OwnerAbandonTime = DateTime.MinValue;
                IsBonded = false;
                return null;
            }
            else
            {
                return owner;
            }
        }

        public virtual bool AddHire(Mobile m)
        {
            Mobile owner = GetOwner();

            if (owner != null)
            {
                m.SendLocalizedMessage(1043283, owner.Name); // I am following ~1_NAME~. 
                return false;
            }
            return SetControlMaster(m);
        }

        private BaseShield GenerateShield()
        {
            var shieldPool = new List<BaseShield>();
            
            // Get base stats for shield selection
            int str = this.Str;
            
            // Add shields based on strength requirements and experience
            if (str >= 20)
            {
                shieldPool.Add(new Buckler());        // Str 20, Armor 7
                shieldPool.Add(new WoodenShield());   // Str 20, Armor 8
                shieldPool.Add(new WoodenKiteShield()); // Str 20, Armor 12
            }
            
            if (str >= 35)
            {
                shieldPool.Add(new BronzeShield());   // Str 35, Armor 10
            }
            
            if (str >= 45)
            {
                shieldPool.Add(new MetalShield());    // Str 45, Armor 11
                shieldPool.Add(new MetalKiteShield()); // Str 45, Armor 16
            }
            
            if (str >= 90)
            {
                shieldPool.Add(new HeaterShield());   // Str 90, Armor 23
            }
            
            // Experience-based preferences
            switch (m_Persona.Experience)
            {
                case PlayerBotPersona.PlayerBotExperience.Newbie:
                    // Newbies prefer lighter shields
                    if (str >= 20)
                    {
                        shieldPool.Clear();
                        shieldPool.Add(new Buckler());
                        shieldPool.Add(new WoodenShield());
                    }
                    break;
                    
                case PlayerBotPersona.PlayerBotExperience.Average:
                    // Average players get a mix, but avoid the heaviest
                    if (str >= 90)
                    {
                        shieldPool.RemoveAll(s => s is HeaterShield);
                    }
                    break;
                    
                case PlayerBotPersona.PlayerBotExperience.Proficient:
                    // Proficient players can handle most shields
                    break;
                    
                case PlayerBotPersona.PlayerBotExperience.Grandmaster:
                    // Grandmasters prefer the best available shields
                    if (str >= 90)
                    {
                        shieldPool.Clear();
                        shieldPool.Add(new HeaterShield());
                        shieldPool.Add(new MetalKiteShield());
                    }
                    else if (str >= 45)
                    {
                        shieldPool.Clear();
                        shieldPool.Add(new MetalKiteShield());
                        shieldPool.Add(new MetalShield());
                    }
                    break;
            }
            
            // Return a random shield from the pool, or null if none available
            if (shieldPool.Count > 0)
            {
                return shieldPool[Utility.Random(shieldPool.Count)];
            }
            
            return null;
        }
    }
}
