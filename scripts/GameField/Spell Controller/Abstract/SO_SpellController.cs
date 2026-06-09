using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SO_SpellController : ScriptableObject {
	private SO_Hero Hero;
	[HideInInspector]
	public float Recource = 100f;
	public float MaxRecource=100f;
	public float deafultMaxRecource=100f;
	public static SO_SpellController objRef;
	public int FreeCasts = 0;
	private RecourcePanel RecourceShower;
	public virtual void GameInitialization(SO_Hero Hero, RecourcePanel recourcePanel)
	{
		objRef = this;
		MaxRecource = deafultMaxRecource;
		ServiceLocator.spellController = this;
		this.Hero = Hero;
		FreeCasts = 0;
	}
	public void AddFreeCasts(int number)
	{
		FreeCasts += number;
	}
	public void AddRecource(float value)
	{
		Recource += value;
		if (Recource > MaxRecource)
			Recource = MaxRecource;
	}
	public SO_VisibleSkill[] GetSpells()
	{
		return Hero.Spells;
	}
	public virtual bool CanCast (float Recourse)
	{
		if (FreeCasts > 0)
			return true;
		if (Recourse < this.Recource)
			return true;
		return false;
	}
	public virtual void CastSpell (SO_VisibleSkill Spell)
	{				
		if (CanCast (Spell.Activate_Recourse)) {			
			if (FreeCasts > 0)
				FreeCasts--;
			else {
				BattleEventsManager.Events.MpLoosed.Invoke (Spell.Activate_Recourse);
				Recource -= Spell.Activate_Recourse;
			}
			Spell.Cast ();
		}
	}
	public bool IsOpen()
	{
		return true;
	}
}
