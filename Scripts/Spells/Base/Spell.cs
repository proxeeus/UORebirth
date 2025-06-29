using System;
using Server.Items;
using Server.Network;
using Server.Targeting;
using Server.Mobiles;
using Server.Spells.Second;
using Server.Mobiles;

namespace Server.Spells
{
	public abstract class Spell : ISpell
	{
		private Mobile m_Caster;
		private Item m_Scroll;
		private SpellInfo m_Info;
		private SpellState m_State;
		private DateTime m_StartCastTime;

		public SpellState State{ get{ return m_State; } set{ m_State = value; } }
		public Mobile Caster{ get{ return m_Caster; } }
		public SpellInfo Info{ get{ return m_Info; } }
		public string Name{ get{ return m_Info.Name; } }
		public string Mantra{ get{ return m_Info.Mantra; } }
		public SpellCircle Circle{ get{ return m_Info.Circle; } }
		public Type[] Reagents{ get{ return m_Info.Reagents; } }
		public Item Scroll{ get{ return m_Scroll; } }

		private static TimeSpan NextSpellDelay = TimeSpan.Zero;
		private static TimeSpan AnimateDelay = TimeSpan.FromSeconds( 1.5 );

		public virtual SkillName CastSkill{ get{ return SkillName.Magery; } }
		public virtual SkillName DamageSkill{ get{ return SkillName.Magery; } }

		public virtual bool RevealOnCast{ get{ return true; } }
		public virtual bool ClearHandsOnCast{ get{ return false; } }

		public virtual bool DelayedDamage{ get{ return true; } }

		public Spell( Mobile caster, Item scroll, SpellInfo info )
		{
			m_Caster = caster;
			m_Scroll = scroll;
			m_Info = info;
		}

		public static int GetPreUORDamage( SpellCircle Circle )
		{
			switch ( Circle )
			{
				case SpellCircle.First: return Utility.Dice( 1,3,3 );
				case SpellCircle.Second:return Utility.Dice( 1,8,4 );
				case SpellCircle.Third:	return Utility.Dice( 4,4,4 );
				case SpellCircle.Fourth:return Utility.Dice( 3,8,5 );
				case SpellCircle.Fifth:	return Utility.Dice( 5,8,6 );
				case SpellCircle.Sixth:	return Utility.Dice( 6,8,8 );
				case SpellCircle.Seventh:return Utility.Dice(7,8,10);
				case SpellCircle.Eighth:return Utility.Dice( 7,8,10);
			}

			return 1;
		}

		public int GetPreUORDamage()
		{
			return GetPreUORDamage( Circle );
		}

		public virtual bool IsCasting{ get{ return m_State == SpellState.Casting; } }

		public virtual void OnCasterHurt()
		{
			// this function is only called on players when they survive the 
			// first disrupt attempt ( called from playermobile.damage) and the core calls this again.

			//if ( Caster.Player && IsCasting )
			//	Console.WriteLine( "Caution: OnCasterHurt called on Player with NO damage value sent!!" );
		}

		public virtual void OnCasterHurt( int damage )
		{
			if ( IsCasting )
			{
				// so for 4th circle it'd be around 3*100/7=42 base + damage*2 (for example, 20 dmg) +/- 20
				// to lightning taking 20 dmg would be a check of 62 to 102
				int circle = ( this.Scroll != null ? ((int)this.Circle) - 2 : (int)this.Circle);
				
				if ( this is Fourth.RecallSpell || this is Fourth.GreaterHealSpell || this is First.HealSpell )
					circle += 2;

				if ( circle < 0 )
					circle = 0;
				else if ( circle > 7 )
					circle = 7;

				double sk = (((double)( circle ) * 100.0 )/7.0) + ((double)(damage) * 8.0) - 20.0;
				if ( ( ( Caster.Skills[SkillName.Magery].Value - sk ) / 40.0 ) < Utility.RandomDouble() )
					Disturb( DisturbType.Hurt, true, false );

				if ( Caster.AccessLevel == AccessLevel.Administrator )
				{
					try
					{
						Caster.SendMessage( "Sk = {0:#0.00} ( circle = {1:#0.00}, damage = {2:#0.00} )", sk, circle, damage );
					}
					catch
					{
					}
				}
			}
		}

		public virtual void OnCasterKilled()
		{
			Disturb( DisturbType.Kill );
		}

		public virtual void OnConnectionChanged()
		{
			FinishSequence();
		}

		public virtual bool OnCasterMoving( Direction d )
		{
			if ( IsCasting && BlocksMovement && m_Caster.Player )
			{
				m_Caster.SendLocalizedMessage( 500111 ); // You are frozen and can not move.
				return false;
			}

			return true;
		}

		public virtual bool OnCasterEquiping( Item item )
		{
			//if ( IsCasting )
			//	Disturb( DisturbType.EquipRequest );

			return true;
		}

		public virtual bool OnCasterUsingObject( object o )
		{
			//if ( m_State == SpellState.Sequencing )
			//	Disturb( DisturbType.UseRequest );

			return true;
		}

		public virtual bool OnCastInTown( Region r )
		{
			return m_Info.AllowTown;
		}

		public virtual bool ConsumeReagents()
		{
			if ( m_Scroll != null || (!m_Caster.Player && !(m_Caster is PlayerBot && ((PlayerBot)m_Caster).ConsumesReagents)) )
				return true;

			if ( AosAttributes.GetValue( m_Caster, AosAttribute.LowerRegCost ) > Utility.Random( 100 ) )
				return true;

			Container pack = m_Caster.Backpack;

			if ( pack == null )
				return false;

			if ( pack.ConsumeTotal( m_Info.Reagents, m_Info.Amounts ) == -1 )
				return true;

			if ( ArcaneGem.ConsumeCharges( m_Caster, 1 + (int)Circle ) )
				return true;

			return false;
		}

		public virtual bool CheckResisted( Mobile target )
		{
			// approximate the damage as circle*5 (4th=20, 5th=25, 6th=30, 7th=35)
			// this is close to the avg damage for these circles
			// better to use the other function if the damage is known
			return CheckResisted( target, GetPreUORDamage() * GetDamageScalar( target ) ); 
		}

		public static bool CheckResisted( Mobile target, double damage )
		{
			if ( damage <= 1 )
				return true;

			double sk = damage * 2.5; 
			if ( sk > 124.9 )
				sk = 124.9;
			return target.CheckSkill( SkillName.MagicResist, sk - 25.0, sk + 25.0 );
		}

		public virtual bool CheckResistedEasy( Mobile target )
		{
			if ( target.Region is Regions.GuardedRegion && !((Regions.GuardedRegion)target.Region).IsDisabled() )
			{
				int sk = (1 + (int)Circle) * 5; // easy resist for mana vamp, poison, etc
				if ( !target.Player && Utility.RandomBool() )
					sk *= 2;
				return target.CheckSkill( SkillName.MagicResist, sk - 20, sk + 20 );
			}
			else
			{
				return true;
			}
		}

		public virtual double GetDamage( Mobile target )
		{
			return GetDamage( target, true );
		}

		public virtual double GetDamage( Mobile target, bool checkResist )
		{
			double damage = GetPreUORDamage() * GetDamageScalar( target );
			if ( damage < 0 )
				damage = 0;
			if ( checkResist )
			{
				if ( CheckResisted( target, damage ) )
				{
					damage *= 0.5;
					target.SendLocalizedMessage( 501783 ); // You feel yourself resisting magical energy.
				}
			}
			return damage;
		}

		public virtual double GetDamageScalar( Mobile target )
		{
			// magery damage 'bonus'
			double scalar = ( 0.5 + m_Caster.Skills[CastSkill].Value / 100.0 ); // was 0.5 + ( (magery/100) * 0.45 )

			// final damage is DIVIDED BY 2 in SpellHelper.Damage (as per OSI patch notes which say spell damage was halved)
			// so don't forget to change that AND CheckResisted / CheckResistedEasy if you change these values!!!

			if ( target is BaseCreature )
				((BaseCreature)target).AlterDamageScalarFrom( m_Caster, ref scalar );

			if ( m_Caster is BaseCreature )
				((BaseCreature)m_Caster).AlterDamageScalarTo( target, ref scalar );

			target.Region.SpellDamageScalar( m_Caster, target, ref scalar );
			return scalar;
		}

		public virtual void DoFizzle()
		{
			m_Caster.LocalOverheadMessage( MessageType.Regular, 0x3B2, 502632 ); // The spell fizzles.

			if ( m_Caster.Player )
			{
				if ( Core.AOS )
					m_Caster.FixedParticles( 0x3735, 1, 30, 9503, EffectLayer.Waist );
				else
					m_Caster.FixedEffect( 0x3735, 6, 30 );

				m_Caster.PlaySound( 0x5C );
			}
		}

		private CastTimer m_CastTimer;
		private AnimTimer m_AnimTimer;

		public void Disturb( DisturbType type )
		{
			Disturb( type, true, false );
		}

		public virtual bool CheckDisturb( DisturbType type, bool firstCircle, bool resistable )
		{
			return true;
		}

		public void Disturb( DisturbType type, bool firstCircle, bool resistable )
		{
			if ( !CheckDisturb( type, firstCircle, resistable ) )
				return;

			if ( m_State == SpellState.Casting )
			{
				if ( !firstCircle && Circle == SpellCircle.First && !Core.AOS )
					return;

				m_State = SpellState.None;
				m_Caster.Spell = null;

				OnDisturb( type, true );

				if ( m_CastTimer != null )
					m_CastTimer.Stop();

				if ( m_AnimTimer != null )
					m_AnimTimer.Stop();

				m_Caster.NextSpellTime = DateTime.Now + GetDisturbRecovery();
			}
			/*else if ( m_State == SpellState.Sequencing )
			{
				if ( !firstCircle && Circle == SpellCircle.First && !Core.AOS )
					return;

				m_State = SpellState.None;
				m_Caster.Spell = null;

				OnDisturb( type, false );

				Targeting.Target.Cancel( m_Caster );

				if ( Core.AOS && m_Caster.Player && type == DisturbType.Hurt )
					DoHurtFizzle();
			}*/
		}

		public virtual void DoHurtFizzle()
		{
			m_Caster.FixedEffect( 0x3735, 6, 30 );
			m_Caster.PlaySound( 0x5C );
		}

		public virtual void OnDisturb( DisturbType type, bool message )
		{
			if ( message )
				m_Caster.SendLocalizedMessage( 500641 ); // Your concentration is disturbed, thus ruining thy spell.
		}

		public virtual bool CheckCast()
		{
			return true;
		}

		public virtual void SayMantra()
		{
			if ( m_Info.Mantra != null && m_Info.Mantra.Length > 0 && (m_Caster.Player || m_Caster.Body.IsHuman) )
			{
				m_Caster.PublicOverheadMessage( MessageType.Spell, m_Caster.SpeechHue, true, m_Info.Mantra, false );

				IPooledEnumerable eable = m_Caster.GetItemsInRange( 6 );
				foreach ( Item i in eable )
				{
					if ( i is ComCrystal && i.HandlesOnSpeech )
						((ComCrystal)i).Comm( m_Caster, m_Caster.SpeechHue, m_Info.Mantra );
				}
				eable.Free();
			}
		}

		public virtual bool BlockedByHorrificBeast{ get{ return true; } }
		public virtual bool BlocksMovement{ get{ return true; } }

		public virtual bool CheckNextSpellTime{ get{ return false; } }//return !(m_Scroll is BaseWand); } }

		public bool Cast()
		{
			m_StartCastTime = DateTime.Now;

			if ( !m_Caster.CheckAlive() )
			{
				return false;
			}
			else if ( m_Caster.Spell != null && m_Caster.Spell.IsCasting )
			{
				m_Caster.SendLocalizedMessage( 502642 ); // You are already casting a spell.
			}
			else if ( m_Caster.Paralyzed || m_Caster.Frozen )
			{
				m_Caster.SendLocalizedMessage( 502643 ); // You can not cast a spell while frozen.
			}
			else if ( CheckNextSpellTime && DateTime.Now < m_Caster.NextSpellTime )
			{
				m_Caster.SendLocalizedMessage( 502644 ); // You must wait for that spell to have an effect.
			}
			else if ( m_Caster.Mana >= ScaleMana( GetMana() ) )
			{
				if ( m_Caster.Spell == null && m_Caster.CheckSpellCast( this ) && CheckCast() && m_Caster.Region.OnBeginSpellCast( m_Caster, this ) )
				{
					m_State = SpellState.Casting;
					m_Caster.Spell = this;

					if ( RevealOnCast )
						m_Caster.RevealingAction();

					SayMantra();

					TimeSpan castDelay = this.GetCastDelay();

					if ( m_Caster.Body.IsHuman )
					{
						int count = (int)Math.Ceiling( castDelay.TotalSeconds / AnimateDelay.TotalSeconds );

						if ( count != 0 )
						{
							m_AnimTimer = new AnimTimer( this, count );
							m_AnimTimer.Start();
						}

						if ( m_Info.LeftHandEffect > 0 )
							Caster.FixedParticles( 0, 10, 5, m_Info.LeftHandEffect, EffectLayer.LeftHand );

						if ( m_Info.RightHandEffect > 0 )
							Caster.FixedParticles( 0, 10, 5, m_Info.RightHandEffect, EffectLayer.RightHand );
					}

					if ( ClearHandsOnCast )
						m_Caster.ClearHands();

					m_CastTimer = new CastTimer( this, castDelay );
					m_CastTimer.Start();

					OnBeginCast();

					return true;
				}
				else
				{
					return false;
				}
			}
			else
			{
				m_Caster.LocalOverheadMessage( MessageType.Regular, 0x22, 502625 ); // Insufficient mana
			}

			return false;
		}

		public abstract void OnCast();

		public virtual void OnBeginCast()
		{
		}

		public virtual void GetCastSkills( out double min, out double max )
		{
			int circle = (int)Circle;

			if ( m_Scroll != null )
				circle -= 2;

			double avg = 100.0 * circle / 7;

			min = avg - 20;
			max = avg + 20;
		}

		public virtual bool CheckFizzle()
		{
			double minSkill, maxSkill;

			GetCastSkills( out minSkill, out maxSkill );

			return Caster.CheckSkill( CastSkill, minSkill, maxSkill );
		}

		public static int[] m_ManaTable = new int[]{ 4, 6, 9, 11, 14, 20, 40, 50 };

		public virtual int GetMana()
		{
			return m_ManaTable[(int)Circle];
		}

		public virtual int ScaleMana( int mana )
		{
			double scalar = 1.0;

			scalar -= (double)AosAttributes.GetValue( m_Caster, AosAttribute.LowerManaCost ) / 100;

			return (int)(mana * scalar);
		}

		public virtual TimeSpan GetDisturbRecovery()
		{
			if ( Core.AOS )
				return TimeSpan.Zero;

			double delay = 1.0 - Math.Sqrt( (DateTime.Now - m_StartCastTime).TotalSeconds / GetCastDelay().TotalSeconds );

			if ( delay < 0.2 )
				delay = 0.2;

			return TimeSpan.FromSeconds( delay );
		}

		public virtual int CastRecoveryBase{ get{ return 6; } }
		public virtual int CastRecoveryCircleScalar{ get{ return 0; } }
		public virtual int CastRecoveryFastScalar{ get{ return 1; } }
		public virtual int CastRecoveryPerSecond{ get{ return 4; } }
		public virtual int CastRecoveryMinimum{ get{ return 0; } }

		public virtual TimeSpan GetCastRecovery()
		{
			if ( !Core.AOS )
				return NextSpellDelay;

			int fcr = AosAttributes.GetValue( m_Caster, AosAttribute.CastRecovery );

			int circleDelay = CastRecoveryCircleScalar * (1 + (int)Circle); // Note: Circle is 0-based so we must offset
			int fcrDelay = -(CastRecoveryFastScalar * fcr);

			int delay = CastRecoveryBase + circleDelay + fcrDelay;

			if ( delay < CastRecoveryMinimum )
				delay = CastRecoveryMinimum;

			return TimeSpan.FromSeconds( (double)delay / CastRecoveryPerSecond );
		}

		public virtual int CastDelayBase{ get{ return 3; } }
		public virtual int CastDelayCircleScalar{ get{ return 1; } }
		public virtual int CastDelayFastScalar{ get{ return 1; } }
		public virtual int CastDelayPerSecond{ get{ return 4; } }
		public virtual int CastDelayMinimum{ get{ return 1; } }

		public virtual TimeSpan GetCastDelay()
		{
			return TimeSpan.FromSeconds( 0.5 + ((int)Circle)*0.5 );
		}

		public virtual void FinishSequence()
		{
			m_State = SpellState.None;

			if ( m_Caster.Spell == this )
				m_Caster.Spell = null;
		}

		public virtual int ComputeKarmaAward()
		{
			return 0;
		}

		public virtual bool CheckSequence()
		{
			int mana = ScaleMana( GetMana() );

			Item oneHanded = m_Caster.FindItemOnLayer( Layer.OneHanded );
			Item twoHanded = m_Caster.FindItemOnLayer( Layer.TwoHanded );

			if ( (oneHanded != null && !oneHanded.AllowEquipedCast( m_Caster )) || (twoHanded != null && !twoHanded.AllowEquipedCast( m_Caster )) )
			{
				//m_Caster.SendLocalizedMessage( 502626 ); // Your hands must be free to cast spells or meditate
				m_Caster.SendAsciiMessage( "Your hands must be free to cast spells." );
				return false;
			}
			else if ( m_Caster.Deleted || !m_Caster.Alive || m_Caster.Spell != this || m_State != SpellState.Sequencing )
			{
				DoFizzle();
			}
			else if ( m_Scroll != null && ( m_Scroll.Amount <= 0 || m_Scroll.Deleted || m_Scroll.RootParent != m_Caster ) )
			{
				DoFizzle();
			}
			else if ( !ConsumeReagents() )
			{
				m_Caster.LocalOverheadMessage( MessageType.Regular, 0x22, 502630 ); // More reagents are needed for this spell.
			}
			else if ( m_Caster.Mana < mana )
			{
				m_Caster.LocalOverheadMessage( MessageType.Regular, 0x22, 502625 ); // Insufficient mana for this spell.
			}
			else if ( Core.AOS && (m_Caster.Frozen || m_Caster.Paralyzed) )
			{
				m_Caster.SendLocalizedMessage( 502646 ); // You cannot cast a spell while frozen.
				DoFizzle();
			}
			else if ( CheckFizzle() )
			{
				m_Caster.Mana -= mana;

				if ( m_Scroll is SpellScroll )
					m_Scroll.Consume();

				if ( ClearHandsOnCast )
					m_Caster.ClearHands();

				return true;
			}
			else
			{
				DoFizzle();
			}

			return false;
		}

		public bool CheckBSequence( Mobile target )
		{
			return CheckBSequence( target, false );
		}

		public bool CheckBSequence( Mobile target, bool allowDead )
		{
			if ( !target.Alive && !allowDead )
			{
				m_Caster.SendLocalizedMessage( 501857 ); // This spell won't work on that!
				return false;
			}
			else if ( Caster.CanBeBeneficial( target, true, allowDead ) && CheckSequence() )
			{
				Caster.DoBeneficial( target );
				return true;
			}
			else
			{
				return false;
			}
		}

		public bool CheckHSequence( Mobile target )
		{
			if ( !target.Alive )
			{
				m_Caster.SendLocalizedMessage( 501857 ); // This spell won't work on that!
				return false;
			}
			else if ( Caster.CanBeHarmful( target ) && CheckSequence() )
			{
				Caster.DoHarmful( target );
				return true;
			}
			else
			{
				return false;
			}
		}

		private class AnimTimer : Timer
		{
			private Spell m_Spell;

			public AnimTimer( Spell spell, int count ) : base( TimeSpan.Zero, AnimateDelay, count )
			{
				m_Spell = spell;

				Priority = TimerPriority.FiftyMS;
			}

			protected override void OnTick()
			{
				if ( m_Spell.State != SpellState.Casting || m_Spell.m_Caster.Spell != m_Spell )
				{
					Stop();
					return;
				}

				if ( !m_Spell.Caster.Mounted && m_Spell.Caster.Body.IsHuman && m_Spell.m_Info.Action >= 0 )
					m_Spell.Caster.Animate( /*m_Spell.m_Info.Action*/ 16, 7, 1, true, false, 0 );

				if ( !Running )
					m_Spell.m_AnimTimer = null;
			}
		}

		private class CastTimer : Timer
		{
			private Spell m_Spell;

			public CastTimer( Spell spell, TimeSpan castDelay ) : base( castDelay )
			{
				m_Spell = spell;

				Priority = TimerPriority.TwentyFiveMS;
			}

			protected override void OnTick()
			{
				if ( m_Spell.m_State == SpellState.Casting && m_Spell.m_Caster.Spell == m_Spell )
				{
					m_Spell.m_State = SpellState.Sequencing;
					m_Spell.m_CastTimer = null;
					m_Spell.m_Caster.OnSpellCast( m_Spell );
					m_Spell.m_Caster.Region.OnSpellCast( m_Spell.m_Caster, m_Spell );
					m_Spell.m_Caster.NextSpellTime = DateTime.Now + m_Spell.GetCastRecovery();// Spell.NextSpellDelay;

					Target originalTarget = m_Spell.m_Caster.Target;

					m_Spell.OnCast();

					if ( m_Spell.m_Caster.Player && m_Spell.m_Caster.Target != originalTarget )
						m_Spell.m_Caster.Target.BeginTimeout( m_Spell.m_Caster, TimeSpan.FromSeconds( 60.0 ) );

					m_Spell.m_CastTimer = null;
				}
			}
		}
	}
}

