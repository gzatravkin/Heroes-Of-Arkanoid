using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/ItemFlask")]
public class SO_ItemFlask : SO_AbstractItem {
	public float[] HpReg = new float[]{0f,0.01f,0.015f,0.02f};
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		BattleEventsManager.Events.BlockDestroyed.AddListener((arg0) => Check());
	}
	void Check()
	{
		LifeManager.AddHp (HpReg[Level]*LifeManager.maxLifes);
	}
}
