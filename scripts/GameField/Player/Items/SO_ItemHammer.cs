using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/Hammer")]
public class SO_ItemHammer : SO_AbstractItem {
	public float[] SpeedBonus;

	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		BattleEventsManager.Events.Ball_BarCollision.AddListener((arg0,arg1) => SetBallEffect());
	}

	public void SetBallEffect()
	{		
		BattleController.GetCurrentPlayerObject ().AddBonus (new BonusVelocity (SpeedBonus[Level], 2));
	}
}
