using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhysicExplotion : MonoBehaviour {
	public float ForceToAdd=100;
	public float MaxDistance=3f;
	private Rigidbody2D rigi;
	void Start()
	{
		rigi = GetComponent<Rigidbody2D> ();
	}

	void OnCollisionEnter2D(Collision2D col)
	{
		Hit (col.rigidbody);
	}
	void OnTriggerEnter2D(Collider2D col)
	{
		Hit (col.attachedRigidbody);
	}
	void Hit(Rigidbody2D other)
	{
		var delta = other.position-rigi.position;
		float dist = delta.magnitude;
		if (dist < MaxDistance) {
			other.AddForce (delta.normalized * (dist/MaxDistance)*ForceToAdd);
		}
	}
}
