using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/Torch")]
public class SO_ItemTorch : SO_AbstractItem {
	public float[] SpeedBonus;

	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		BattleController.GetCurrentPlayerObject ().AddBonus (new BonusVelocity (SpeedBonus[Level], -1));
	}
}
