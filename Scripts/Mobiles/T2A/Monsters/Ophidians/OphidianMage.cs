namespace Server.Mobiles
{
    [CorpseName( "an ophidian corpse" )]
	[TypeAlias( "Server.Mobiles.OphidianShaman" )]
	public class OphidianMage : BaseCreature
	{
		private static string[] m_Names = new string[]
			{
				"an ophidian apprentice mage",
				"an ophidian shaman"
			};

		[Constructable]
		public OphidianMage() : base( AIType.AI_Mage, FightMode.Closest, 10, 1, 0.2, 0.4 )
		{
			Name = m_Names[Utility.Random( m_Names.Length )];
			Body = 85;
			BaseSoundID = 639;

			SetStr( 181, 205 );
			SetDex( 191, 215 );
			SetInt( 96, 120 );

			SetHits( 109, 123 );

			SetDamage( 5, 10 );

			SetSkill( SkillName.EvalInt, 85.1, 100.0 );
			SetSkill( SkillName.Magery, 85.1, 100.0 );
			SetSkill( SkillName.MagicResist, 75.0, 97.5 );
			SetSkill( SkillName.Tactics, 65.0, 87.5 );
			SetSkill( SkillName.Wrestling, 20.2, 60.0 );

			Fame = 4000;
			Karma = -4000;

			VirtualArmor = 30;

			PackReg( 10 );
			GenerateLoot();
		}

		public override void GenerateLoot()
		{
			AddLoot( LootPack.Average );
			AddLoot( LootPack.LowScrolls );
			AddLoot( LootPack.MedScrolls );
			AddLoot( LootPack.Potions );
		}

		public override int Meat{ get{ return 1; } }
		public override int TreasureMapLevel{ get{ return 2; } }

		public override OppositionGroup OppositionGroup
		{
			get{ return OppositionGroup.TerathansAndOphidians; }
		}

		public OphidianMage( Serial serial ) : base( serial )
		{
		}

		public override void Serialize( GenericWriter writer )
		{
			base.Serialize( writer );
			writer.Write( (int) 0 );
		}

		public override void Deserialize( GenericReader reader )
		{
			base.Deserialize( reader );
			int version = reader.ReadInt();
		}
	}
}