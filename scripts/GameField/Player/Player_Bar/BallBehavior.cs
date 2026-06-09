using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallBehavior : MonoBehaviour {
	public float Velocity = 9f;
	public float MaxCoefXYVel=5f;
	private Rigidbody2D rigi;
	// Use this for initialization
	void Start () {
		rigi = GetComponent<Rigidbody2D> ();
	}	
	void FixedUpdate()
	{		
		if (!rigi.isKinematic) {
			rigi.velocity = CorrectDir (rigi.velocity, MaxCoefXYVel, 0.01f).normalized * Velocity;
		}
	}
	public Vector2 CorrectDir(Vector2 dir, float CorrectDirValue, float RandomCoef)
	{		
		dir = dir + Random.insideUnitCircle * (dir.magnitude+1f)*RandomCoef;
		float xP = Mathf.Abs(dir.x);
		float yP = Mathf.Abs(dir.y);
		dir.x = Mathf.Sign(dir.x)* Mathf.Clamp (xP, yP / CorrectDirValue, yP * CorrectDirValue);
		dir.y = Mathf.Sign(dir.y)* Mathf.Clamp (yP, xP / CorrectDirValue, xP * CorrectDirValue);
		return dir;
	}
}
