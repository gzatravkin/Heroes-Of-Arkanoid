using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/BarEngine")]
public class SO_ItemBarSpeed : SO_AbstractItem {
	public float[] Speed;
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		BattleController.GetCurrentPlayerObject().AddBonus(new BonusVelocityOfBar(Speed[Level],-1f));
	}
}
