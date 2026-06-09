using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BatController : MonoBehaviour {

	private BallController ballAtached;
	public float TimeToKeep=3f;

	public float SpeedBonus=4f;
	public float TimeBonus=0.5f;
	void OnCollisionEnter2D(Collision2D col)
	{
		var ballController = col.gameObject.GetComponent<BallController> ();
		if (ballController != null&&col.rigidbody.isKinematic==false)			
			AttachIt (ballController);
		if (col.gameObject.GetComponent<BatController> () != null)
			Physics2D.IgnoreCollision (GetComponent<Collider2D> (), col.collider);
	}
	void AttachIt(BallController ball)
	{
		Physics2D.IgnoreCollision (GetComponent<Collider2D> (), ball.GetComponent<Collider2D> ());
		ballAtached = ball;	
		ball.rigi.isKinematic = true;
	}

	void LetItGo(BallController ball)
	{		
		BattleController.GetCurrentPlayerObject ().AddBonus (new BonusVelocity (SpeedBonus, TimeBonus));
		ball.rigi.isKinematic = false;
		ballAtached = null;
	}

	void FlyAway()
	{
		if (ballAtached != null)
			LetItGo (ballAtached);
		var collider = GetComponent<Collider2D> ();
		if (collider!=null)
			collider.isTrigger = true;		
		var ballMovControll = GetComponent<Enemy_Ball_MovimientController> ();
		if (ballMovControll != null)
			Destroy (ballMovControll);
		var followMarks = GetComponent<MarksFollower> ();
		if (followMarks != null)
			Destroy (followMarks );
		GetComponent<Rigidbody2D> ().gravityScale = -2f;
	}
	// Update is called once per frame
	void Update () {
		if (ballAtached!=null)
		{
			ballAtached.transform.position = transform.position;
			TimeToKeep += -TimeManager.GetDeltaTime (TimeType.ScaledTime);
			if (TimeToKeep < 0) {
				LetItGo (ballAtached);
				FlyAway ();
			}
		}
	}
}
