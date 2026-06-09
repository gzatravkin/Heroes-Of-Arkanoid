using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//TODO RENAME TO BAR COLISION CONTROLLER
public class PlayerBar_CollisionController : MonoBehaviour {	
	public float MaxAngle;
	void OnCollisionEnter2D(Collision2D col)
	{
		var size  = GetComponent<BoxCollider2D> ().size.x;
		float normalizedPos = (col.contacts [0].point.x-transform.position.x)/size;
		col.rigidbody.velocity = new Vector3(normalizedPos*1.5f,1f)*col.rigidbody.velocity.magnitude;
		if (col.rigidbody.velocity.y < 0 && col.transform.position.y>transform.position.y)
			col.rigidbody.velocity = new Vector2 (col.rigidbody.velocity.x,-col.rigidbody.velocity.y);
		if (col.gameObject.tag == "Ball")
			BattleEventsManager.Events.Ball_BarCollision.Invoke (col.gameObject.GetComponent<BallController>(), normalizedPos);
	}
}
