using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_SpellController_Mana : SO_SpellController {	
	public float OrigenValue=0;
	public float MpRegeneration;
	public float DefaultMpRegenreation=1f;
	public override void GameInitialization (SO_Hero Hero, RecourcePanel RecourceShower)
	{
		base.GameInitialization (Hero, RecourceShower);
		MpRegeneration = DefaultMpRegenreation;
		Recource = OrigenValue;
		BattleEventsManager.Events.UpdateEvent.AddListener (() => Update ());
	}
	public void Update()
	{
		Recource += MpRegeneration*TimeManager.battleDeltaTime;
		if (Recource > MaxRecource)
			Recource = MaxRecource;
	}



}
