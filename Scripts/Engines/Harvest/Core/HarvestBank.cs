using System;
using Server.Regions;
using System.Collections; using System.Collections.Generic;

namespace Server.Engines.Harvest
{
	public class HarvestBank
	{
		private int m_Current;
		private int m_Maximum;
		private DateTime m_NextRespawn;
		private HarvestVein m_Vein, m_DefaultVein;


        public string GetDebugData
        {
            get
            {
                return this.m_Current.ToString() + " -- NEXT ONE A " + this.m_NextRespawn.ToString();
            }
            
        }

        public int Current
        {
            get
            {
                CheckRespawn();
                return m_Current;
            }
        }

        public HarvestVein Vein
		{
			get
			{
				CheckRespawn();
				return m_Vein;
			}
			set
			{
				m_Vein = value;
			}
		}

		public HarvestVein DefaultVein
		{
			get
			{
				CheckRespawn();
				return m_DefaultVein;
			}
		}

		public void CheckRespawn()
		{
			if ( m_Current == m_Maximum || m_NextRespawn > DateTime.Now )
            {
                //Console.WriteLine("CheckRespawn: m_Current == " + m_Current.ToString() + "; Next Respawn == " + m_NextRespawn.ToString());
                return;
            }
				

			m_Current = m_Maximum;
			m_Vein = m_DefaultVein;
		}

		public void Consume( HarvestDefinition def, int amount )
		{
			CheckRespawn();

			if ( m_Current == m_Maximum )
			{
				int min = (int)def.MinRespawn.TotalSeconds;
				int max = (int)def.MaxRespawn.TotalSeconds;

				m_Current = m_Maximum - amount;
				m_NextRespawn = DateTime.Now + TimeSpan.FromSeconds( Utility.RandomMinMax( min, max ) );
			}
			else
			{
				m_Current -= amount;
			}

			if ( m_Current < 0 )
				m_Current = 0;
		}

		public HarvestBank( HarvestDefinition def, HarvestVein defaultVein )
		{
			m_Maximum = Utility.RandomMinMax( def.MinTotal, def.MaxTotal );
			m_Current = m_Maximum;
			m_DefaultVein = defaultVein;
			m_Vein = m_DefaultVein;
		}
	}
}