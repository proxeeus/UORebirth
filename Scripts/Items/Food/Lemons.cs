namespace Server.Items
{
    public class Lemons : Food
    {
        [Constructable]
        public Lemons() : this( 1 )
        {
        }

        [Constructable]
        public Lemons( int amount ) : base( amount, 0x1729 )
        {
            this.Weight = 1.0;
            this.FillFactor = 1;
        }

        public Lemons( Serial serial ) : base( serial )
        {
        }
        public override void Serialize( GenericWriter writer )
        {
            base.Serialize( writer );

            writer.Write( (int) 0 ); // version
        }

        public override void Deserialize( GenericReader reader )
        {
            base.Deserialize( reader );

            int version = reader.ReadInt();
        }
    }
}