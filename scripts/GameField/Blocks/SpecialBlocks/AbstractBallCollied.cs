using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AbstractBallCollied : MonoBehaviour {
	protected virtual void OnCollisionEnter2D(Collision2D ball)
	{
		if (ball.gameObject.GetComponent<BallController> () != null)
			Hit ();		
	}
	protected virtual void Hit()
	{
	}
}
