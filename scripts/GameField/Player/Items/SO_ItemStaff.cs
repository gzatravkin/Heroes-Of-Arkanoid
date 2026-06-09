using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/Staff")]
public class SO_ItemStaff : SO_AbstractItem {
	public float[] Coef;
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		SO_SpellController.objRef.MaxRecource += SO_SpellController.objRef.deafultMaxRecource * Coef [Level];
	}
}
