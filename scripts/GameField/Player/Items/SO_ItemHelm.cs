using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/Helm")]
public class SO_ItemHelm : SO_AbstractItem {
	public float[] Chance = new float[]{0f,0.07f,0.1f,0.15f};
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		BattleEventsManager.Events.HpLoosed.AddListener((arg0) => Check(arg0));
	}
	void Check(float HP)
	{
		if (Random.Range (0, 1f) < Chance [Level])
			LifeManager.AddHp (HP);
	}
}
