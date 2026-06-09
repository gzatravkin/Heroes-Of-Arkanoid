using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockEffect_CreateObjectOnDie : AbstractBlockEffect {
	public DieAnimation dieAnimation;
	public override void Die ()
	{
		base.Die ();
		dieAnimation.Die (this.gameObject);
	}
}
