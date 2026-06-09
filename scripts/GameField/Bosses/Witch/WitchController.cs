using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class WitchController : AnimatorController {
	public GameObject target;
	public List<string> actions;
	//Calls from animator
	void StarFly()
	{
		target.GetComponent<Rigidbody2D> ().isKinematic = false;
		target.GetComponent<Rigidbody2D> ().velocity= new Vector2(1,1);
		target.GetComponent<BallBehavior> ().enabled = true;
	}
	//Calls from animator
	void EndFly()
	{
		target.GetComponent<Rigidbody2D> ().isKinematic = true;
		target.GetComponent<Rigidbody2D> ().velocity= Vector2.zero;
		target.GetComponent<BallBehavior> ().enabled = false;
	}
	protected override void OnAnimationStackFinished ()
	{
		base.OnAnimationStackFinished ();
		var act = actions.GetRandomElement ();
		base.SetAnimation (act);
	}

}
