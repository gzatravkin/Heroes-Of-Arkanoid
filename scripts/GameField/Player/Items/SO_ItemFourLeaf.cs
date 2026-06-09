using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/FourLeaf")]
public class SO_ItemFourLeaf : SO_AbstractItem {
	public float[] Chance = new float[]{0f,0.07f,0.1f,0.15f};
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		BattleEventsManager.Events.TryLoosed.AddListener (() => Check ());
	}
	void Check()
	{
		if (Random.Range (0, 1f) < Chance [Level])
			LifeManager.AddBalls (1);
	}
}
