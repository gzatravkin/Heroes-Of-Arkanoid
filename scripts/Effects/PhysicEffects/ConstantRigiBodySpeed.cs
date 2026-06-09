using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConstantRigiBodySpeed : MonoBehaviour {
	public Vector2 speed;
	private Rigidbody2D rigi;
	// Use this for initialization
	void Start () {
		rigi = GetComponent<Rigidbody2D> ();
		rigi.velocity = speed;
	}
	
}
