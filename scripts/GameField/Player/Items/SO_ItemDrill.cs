using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/ItemDrill")]
public class SO_ItemDrill : SO_AbstractItem {
	public float[] Coef = new float[]{0f,0.1f,0.15f,0.2f};
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		MissionWinController.TreasurePointsCoef += Coef [Level];
	}
}
