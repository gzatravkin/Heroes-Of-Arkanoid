using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallVelocityController : MonoBehaviour {
	public Player_BarController barContronller;
	private BallBehavior ballBehavior;
	// Use this for initialization
	void Start () {
		ballBehavior = GetComponent<BallBehavior> ();
	}
	void Update()
	{		
		ballBehavior.Velocity = barContronller.CurrentBallSpeed;
	}


}
