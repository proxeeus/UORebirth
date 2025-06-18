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
            InitSkills();
            InitOutfit();
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

        private void InitSkills()
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

            // InitWearable(); // robes, sashes, kilts etc. Fancy shit.

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
                    AddItem(new LeatherCap());
                    AddItem(new Boots());  // Leather armor gets standard boots
                    break;
                    
                case ArmorMaterialType.Studded:
                    AddItem(new StuddedChest());
                    AddItem(new StuddedArms());
                    AddItem(new StuddedGloves());
                    AddItem(new StuddedGorget());
                    AddItem(new StuddedLegs());
                    AddItem(new LeatherCap());  // StuddedCap doesn't exist, using LeatherCap instead
                    AddItem(new Boots());  // Studded armor gets standard boots
                    break;
                    
                case ArmorMaterialType.Chainmail:
                    AddItem(new ChainChest());
                    AddItem(new ChainLegs());
                    AddItem(new ChainCoif());
                    AddItem(new RingmailGloves());
                    AddItem(new StuddedGorget());
                    AddItem(new ThighBoots());  // Chainmail gets thigh boots for better protection
                    break;
                    
                case ArmorMaterialType.Ringmail:
                    AddItem(new RingmailChest());
                    AddItem(new RingmailLegs());
                    AddItem(new RingmailArms());
                    AddItem(new RingmailGloves());
                    AddItem(new ChainCoif());
                    AddItem(new PlateGorget());
                    AddItem(new ThighBoots());  // Ringmail gets thigh boots for better protection
                    break;
                    
                case ArmorMaterialType.Plate:
                    AddItem(new PlateChest());
                    AddItem(new PlateLegs());
                    AddItem(new PlateArms());
                    AddItem(new PlateGloves());
                    AddItem(new PlateHelm());
                    AddItem(new PlateGorget());
                    AddItem(new ThighBoots());  // Plate armor gets thigh boots for maximum protection
                    break;
            }

            // Add shield if the bot has parry skill
            if (Skills[SkillName.Parry].Base > 0)
            {
                if (Skills[SkillName.Parry].Base >= 50)
                    AddItem(new MetalShield());
                else
                    AddItem(new WoodenShield());
            }
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
                        if (m_Persona.Experience == PlayerBotPersona.PlayerBotExperience.Grandmaster && Utility.RandomBool())
                        {
                            // T2A weapons for grandmasters
                            switch (Utility.Random(4))
                            {
                                case 0:
                                    AddItem(new JukaBow());
                                    break;
                                case 1:
                                    AddItem(new RepeatingCrossbow());
                                    break;
                                case 2:
                                    AddItem(new CompositeBow());
                                    break;
                                case 3:
                                    AddItem(new HeavyCrossbow());
                                    break;
                            }
                        }
                        else
                        {
                            // Classic ranged weapons
                            if (Utility.RandomBool())
                            {
                                AddItem(new Bow());
                            }
                            else
                            {
                                AddItem(new Crossbow());
                            }
                        }
                        
                        // Add appropriate ammunition
                        if (Skills[SkillName.Archery].Base >= 50)
                        {
                            PackItem(new Arrow(Utility.Random(100, 200)));
                        }
                        else
                        {
                            PackItem(new Arrow(Utility.Random(50, 100)));
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
    }
}
