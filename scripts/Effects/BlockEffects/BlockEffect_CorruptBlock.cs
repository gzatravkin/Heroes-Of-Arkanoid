using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//TODO RENAME TO BLOCK EFFECTS
public class BlockEffect_CorruptBlock : AbstractBlockEffect {
	public float Radius = 1f;
	public int Damage = 1;
	public int key = 0;
	public float Delay=0.1f;
	public override void Die ()
	{
		base.Die ();
		var blocks = GameField.objRef.GetBlocksInArea (transform.position, Radius);
		foreach (BlockScript b in blocks) {
			var destroyOthers = b.GetComponent<BlockEffect_CorruptBlock> ();
			var blockToHit = b;
			if (destroyOthers != null && destroyOthers.key == this.key) {
				TimeManager.ActionWithDelay (Delay, () => {
					if (blockToHit !=null)
						blockToHit.GetHit(Damage);
				}, TimeType.ScaledTime);
			}
		}
	}
}
