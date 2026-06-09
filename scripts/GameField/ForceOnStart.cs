using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForceOnStart : MonoBehaviour {
	public float ForceToAdd=15;
	public bool AddForceOnStart = true;
	public float minAngleSpeed = -500;
	public float maxAngleSpeed = 500f;

	private MyRigidBody2D myRigidBody;
	void Start()
	{
		if (myRigidBody == null)
			myRigidBody = gameObject.AddComponent<MyRigidBody2D> ();
		myRigidBody.Initialization (transform);
		if (AddForceOnStart) {
			myRigidBody.Velocity = Random.insideUnitCircle * ForceToAdd;
			myRigidBody.AngularVelocity = Random.Range (minAngleSpeed, maxAngleSpeed);
		}
	}
	void Update()
	{
		myRigidBody.Update ();
	}
}
