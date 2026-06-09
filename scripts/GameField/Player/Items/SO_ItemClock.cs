using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/ItemClock")]
public class SO_ItemClock : SO_AbstractItem {
	public float[] vel = new float[]{0,70,100,100};
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		BattleEventsManager.Events.BlockDestroyed.AddListener((arg0) => Check());
	}
	void Check()
	{
		if (PartOfBlock.CurrentBlocksNumber>15)
			Time.timeScale = Mathf.MoveTowards(Time.timeScale,0.3f,vel[Level]*TimeManager.unscaledDeltaTime);
	}
}
