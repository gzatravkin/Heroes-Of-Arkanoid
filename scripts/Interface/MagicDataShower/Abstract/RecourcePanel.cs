using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class RecourcePanel : MonoBehaviour {
	public static RecourcePanel objRef;
	protected SO_SpellController spellSystem;
	protected virtual void Initialization()
	{		
		
	}
	void Awake()
	{
		objRef = this;
	}
	public void GameInitialization(SO_SpellController spellSystem)
	{
		Initialization ();
		this.spellSystem = spellSystem;
		BattleEventsManager.Events.SpellCasted.AddListener (() => ShowRecource ());
	}

	public virtual void ShowRecource()
	{		
		
	}
}
