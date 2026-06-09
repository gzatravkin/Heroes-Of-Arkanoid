using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WindMasterScript : MonoBehaviour {
	public float force=10;
	public float DistanceToZeroForce = 7f;
	void OnTriggerStay2D(Collider2D col)
	{
		float sqadZeroDistance = DistanceToZeroForce * DistanceToZeroForce;
		float distance = transform.position.sqadDistance2D (col.transform.position);
		Vector2 vectorForce = (col.attachedRigidbody.position - (Vector2)transform.position).normalized * force * TimeManager.GetDeltaTime (TimeType.ScaledTime);
		vectorForce = Vector2.Lerp (vectorForce, Vector2.zero, distance / sqadZeroDistance);
		col.attachedRigidbody.AddForce (vectorForce,ForceMode2D.Impulse);
	}
}
