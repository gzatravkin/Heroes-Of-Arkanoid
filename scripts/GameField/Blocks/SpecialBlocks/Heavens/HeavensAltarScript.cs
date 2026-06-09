using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeavensAltarScript : AbstractBallCollied{
	public float time = 15f;
	protected override void Hit ()
	{
		base.Hit ();
		AbstractStatue.AddAllyTimeToAll (time);
	}
}
