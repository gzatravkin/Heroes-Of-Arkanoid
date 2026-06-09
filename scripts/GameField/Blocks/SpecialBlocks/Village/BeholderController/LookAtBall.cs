using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtBall : MonoBehaviour {
	public float maxDistance;
	public float maxSpeed=0.1f;
	private BallController ball;
	public Transform eye;
	// Update is called once per frame
	void LateUpdate () {
		if (eye!=null)
		{
		ball = ((Player_BarController)BattleController.GetCurrentPlayerObject ()).GetRandomBall();
		if (ball != null) {			
				eye.position = Vector3.MoveTowards(eye.position,transform.position+(ball.transform.position-transform.position).normalized*maxDistance,TimeManager.deltaTime);
		}
		}
	}
}
