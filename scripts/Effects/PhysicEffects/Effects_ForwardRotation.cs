using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Effects_ForwardRotation : MonoBehaviour {
	private Rigidbody2D rigi;
	public float offset=0;
	private MyRigidBody2D MyRigi;
	// Use this for initialization
	void Start () {
		rigi = GetComponent<Rigidbody2D> ();
		MyRigi = GetComponent<MyRigidBody2D> ();
	}
	
	// Update is called once per frame
	void LateUpdate () {	
		if (rigi != null) {
			if (rigi.velocity != Vector2.zero) {
				Vector2 diff = rigi.velocity;
				float rot_z = Mathf.Atan2 (diff.y, diff.x) * Mathf.Rad2Deg;
				rigi.rotation = rot_z - 90+offset;
			}
		} else if (MyRigi != null) {
			Vector2 diff = MyRigi.Velocity;
			float rot_z = Mathf.Atan2 (diff.y, diff.x) * Mathf.Rad2Deg;
			transform.rotation = Quaternion.Euler (0, 0, rot_z - 90+offset);
		} else {
			transform.rotation = Quaternion.identity;
		}
	}
}
