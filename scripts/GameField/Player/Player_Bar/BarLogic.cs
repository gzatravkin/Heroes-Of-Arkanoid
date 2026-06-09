using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarLogic : MonoBehaviour
{
	public GameObject BallSample;
	public List<BallController> Balls;
	public Vector2 localPos = new Vector2 (0, 0.5f);
	public float MinPosOfBall = -1f;

	public Vector2 LaunchVel;
	public BallController BallLocked;
	public float MinX;
	private Rigidbody2D rigi;
	private Player_BarController barController;

	void Start ()
	{
		rigi = GetComponent<Rigidbody2D> ();
		barController = GetComponent<Player_BarController> ();
		if (Input.touchSupported)
			gameObject.AddComponent<TouchBarInput> ();
		else
			gameObject.AddComponent<MouseBarInput> ();
		CreateBall ();
	}

	void LateUpdate ()
	{		
		if (BallLocked != null) {
			BallLocked.transform.position = (transform.position + (Vector3)localPos);
			BallLocked.transform.rotation = Quaternion.identity;
		}
		for (int i = 0; i < Balls.Count; i++) {
			if (Balls[i]==null|| Balls [i].rigi.position.y < MinPosOfBall || !Balls [i].IsVisible ())
				BallLoose (i);
		}
	}

	public void BallLoose (int i)
	{	
		if (Balls[i]!=null)
			Destroy (Balls [i].gameObject);	
		BattleEventsManager.Events.BallDropped.Invoke ();
		if (Balls.Count == 1) {
			if (LifeManager.GetCurrentBalls () > 0) {
				LifeManager.GetBall ();
				BattleEventsManager.Events.TryLoosed.Invoke ();
			}
			else
				BattleController.Loose ();			
			CreateBall ();
		}
		Balls.RemoveAt (i);
	}

	public BallController CreateBall ()
	{		
		var ball = ((GameObject)Instantiate (BallSample, transform.position, Quaternion.identity)).GetComponent<BallController> ();
		ball.GetComponent<BallVelocityController> ().barContronller = barController;
		Balls.Add (ball);
		if (BallLocked == null)
			HoldBall (ball);	
		BattleEventsManager.Events.BallAdded.Invoke (ball);
		return ball;
	}

	public void AddBall (BallController ball)
	{
		if (Balls.IndexOf (ball) < 0) {
			Balls.Add (ball);
			if (ball.rigi.isKinematic)
				UnlockBall (ball);
		}
	}

	private void UnlockBall (BallController ball)
	{
		ball.rigi.velocity = LaunchVel;
		ball.rigi.isKinematic = false;
	}

	public void BallLaunch ()
	{
		if (BallLocked != null) {
			UnlockBall (BallLocked);
			BallLocked = null;
		}
	}

	public void HoldBall (BallController ball)
	{
		ball.rigi.position = rigi.position + localPos;
		ball.rigi.velocity = new Vector2 (0, 0);
		ball.transform.rotation = Quaternion.identity;
		BallLocked = ball;
		BallLocked.rigi.isKinematic = true;
	}
}
