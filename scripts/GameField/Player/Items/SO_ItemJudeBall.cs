using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/JudeBall")]
public class SO_ItemJudeBall : SO_AbstractItem {
	public float[] Chance = new float[]{0,0.05f,0.1f,0.15f};
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		BattleEventsManager.Events.BallBlockCollision.AddListener((arg0) => Check(arg0));
	}
	void Check(BlockScript block)
	{
		if (Random.Range (0, 1f) < Chance [Level])
			block.GetHit (1);
	}
}
