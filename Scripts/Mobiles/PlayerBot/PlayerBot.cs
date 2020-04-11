using Server.Items;
using System;
using System.Collections.Generic;
using System.Text;

namespace Server.Mobiles.PlayerBot
{
    public class PlayerBot : BaseCreature
    {
        private PlayerBotPersona m_Persona;

        #region Constructors
        public PlayerBot(AIType AI) : base(AI, FightMode.Agressor, 10, 1, 0.5, 0.75)
        {
            InitPersona();
            InitBody();
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

            // Here should go all private shit that needs to be serialized
            //writer.Write((int)m_Bank);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            // Here should go all pricate shit that needs to be deserialized
            //m_Bank = reader.ReadInt();

            //if (this.Controled)
            //{
            //    m_Timer = new PayTimer(this);
            //    m_Timer.Start();
            //}
        }
        #endregion

        private void InitPersona()
        {

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

        public virtual void InitOutfit()
        {
            var hairHue = Utility.RandomHairHue();

            switch (Utility.Random(6))
            {
                case 0: AddItem(new ShortHair(hairHue)); break;
                case 1: AddItem(new TwoPigTails(hairHue)); break;
                case 2: AddItem(new ReceedingHair(hairHue)); break;
                case 3: AddItem(new KrisnaHair(hairHue)); break;
                case 4: AddItem(new LongHair(hairHue)); break;
                case 5: AddItem(new PageboyHair(hairHue)); break;
            }

            if(!Female)
            {
                if (Utility.RandomBool())
                    AddRandomFacialHair(hairHue);   // Keep facial hair hue consistent with base hair hue
            }
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
