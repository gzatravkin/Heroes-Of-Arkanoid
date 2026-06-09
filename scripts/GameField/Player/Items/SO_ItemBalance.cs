using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//Добавляет хп при потери мп, добавляет мп при потери хп
[CreateAssetMenu(menuName="ScriptableObject/Items/ItemBalance")]
public class SO_ItemBalance : SO_AbstractItem {	
	public float[] Coef = new float[]{0,0.05f,0.1f,0.15f};
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		BattleEventsManager.Events.MpLoosed.AddListener ((arg0) => Check (arg0,0));
		BattleEventsManager.Events.HpLoosed.AddListener ((arg0) => Check (0,arg0));
	}
	void Check(float mpLoosed, float hpLoosed)
	{
		LifeManager.AddHp (mpLoosed*Coef[Level]);
		SO_SpellController.objRef.AddRecource(hpLoosed*Coef[Level]);
	}
}
