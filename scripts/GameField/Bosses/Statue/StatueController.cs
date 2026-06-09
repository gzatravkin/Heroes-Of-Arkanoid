using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StatueController : AnimatorController {
	public List<string> Actions = new List<string>();
	protected override void OnAnimationStackFinished ()
	{
		base.OnAnimationStackFinished ();
		SetAnimation (Actions.GetRandomElement ());
	}
}
