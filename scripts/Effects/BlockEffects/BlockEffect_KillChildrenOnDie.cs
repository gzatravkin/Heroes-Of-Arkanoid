using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockEffect_KillChildrenOnDie : AbstractBlockEffect {
	public override void Die ()
	{
		base.Die ();
		var blocks = GetComponentsInChildren<BlockScript> ();
		foreach (var block in blocks) {
			if (block.gameObject!=gameObject)
				block.Die ();
		}
	}
}
