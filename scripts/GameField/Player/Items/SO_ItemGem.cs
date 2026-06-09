using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Items/Gem")]
public class SO_ItemGem : SO_AbstractItem {
	public float[] Coef = new float[]{0f,1.1f,1.2f,1.3f};
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		var t = SO_SpellController.objRef as SO_SpellController_Mana;
		if (t != null)
			t.MpRegeneration = t.DefaultMpRegenreation * Coef[Level];
	}
}
