using Server.Items;

namespace Server.Mobiles
{
    [CorpseName( "a frost troll corpse" )]
	public class FrostTroll : BaseCreature
	{
		[Constructable]
		public FrostTroll() : base( AIType.AI_Melee, FightMode.Closest, 10, 1, 0.2, 0.4 )
		{
			Name = "a frost troll";
			Body = 55;
			BaseSoundID = 461;

			SetStr( 227, 265 );
			SetDex( 66, 85 );
			SetInt( 46, 70 );

			SetHits( 140, 156 );

			SetDamage( 14, 20 );

			SetSkill( SkillName.MagicResist, 65.1, 80.0 );
			SetSkill( SkillName.Tactics, 80.1, 100.0 );
			SetSkill( SkillName.Wrestling, 80.1, 100.0 );

			Fame = 4000;
			Karma = -4000;

			VirtualArmor = 50;

			PackItem( new DoubleAxe() ); // TODO: Weapon??

            GenerateLoot();
        }

		public override void GenerateLoot()
		{
			AddLoot( LootPack.Average );
			AddLoot( LootPack.Gems );
		}

		public override int Meat{ get{ return 2; } }
		public override int TreasureMapLevel{ get{ return 1; } }

		public FrostTroll( Serial serial ) : base( serial )
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