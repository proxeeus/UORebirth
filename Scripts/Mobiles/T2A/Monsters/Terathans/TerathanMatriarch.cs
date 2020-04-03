using Server.Items;

namespace Server.Mobiles
{
    [CorpseName( "a terathan matriarch corpse" )]
	public class TerathanMatriarch : BaseCreature
	{
		[Constructable]
		public TerathanMatriarch() : base( AIType.AI_Mage, FightMode.Closest, 10, 1, 0.2, 0.4 )
		{
			Name = "a terathan matriarch";
			Body = 72;
			BaseSoundID = 599;

			SetStr( 316, 405 );
			SetDex( 96, 115 );
			SetInt( 366, 455 );

			SetHits( 190, 243 );

			SetDamage( 11, 14 );

			SetSkill( SkillName.EvalInt, 90.1, 100.0 );
			SetSkill( SkillName.Magery, 90.1, 100.0 );
			SetSkill( SkillName.MagicResist, 90.1, 100.0 );
			SetSkill( SkillName.Tactics, 50.1, 70.0 );
			SetSkill( SkillName.Wrestling, 60.1, 80.0 );

			Fame = 10000;
			Karma = -10000;

			PackItem( new SpidersSilk( 5 ) );
			GenerateLoot();
		}

		public void GenerateLoot()
		{
			AddLoot( LootPack.Rich );
			AddLoot( LootPack.Average, 2 );
			AddLoot( LootPack.MedScrolls, 2 );
			AddLoot( LootPack.Potions );
		}

		public override int TreasureMapLevel{ get{ return 4; } }

        public override OppositionGroup OppositionGroup
        {
            get { return OppositionGroup.TerathansAndOphidians; }
        }

        public TerathanMatriarch( Serial serial ) : base( serial )
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