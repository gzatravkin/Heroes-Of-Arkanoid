using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShieldStatue : AbstractStatue {
	public GameObject Shild;
	public GameObject CorruptEffect;
	public float Size=4f;
	public float SizeForLevel=1.4f;
	protected override void Hit ()
	{
		base.Hit ();
		var blocks = GameField.objRef.GetBlocksInArea (transform.position, Size + SizeForLevel * Level);
		foreach (var o in blocks) {
			if (!IsAlly ()) {
				var shield = Instantiate (Shild, o.transform.position, o.transform.rotation);
				shield.SetParentWithScaleOne (o.transform);
			} else {
				var corruptEffect = Instantiate (CorruptEffect, o.transform.position, o.transform.rotation);
				corruptEffect.SetParentWithScaleOne (o.transform);
				o.gameObject.AddComponent<BlockEffect_CorruptBlock> ();
				o.RefreshBlockEffects ();
			}
		}
	}
}
