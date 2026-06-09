using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/Phoenix")]
public class SO_ItemPhoenix : SO_AbstractItem {
	public float[] Coef;
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		LifeManager.AddMaxHP (LifeManager.maxLifes * Coef [Level]);
	}	
}
