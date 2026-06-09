using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NecromantSphere : MonoBehaviour {

	void OnTriggerEnter2D(Collider2D col)
	{
		Hit (col.gameObject);
	}
	void OnCollisionEnter2D(Collision2D col)
	{
		Hit (col.gameObject);
	}
	void Hit(GameObject obj)
	{
		var t = obj.GetComponent<RecuperableObject> ();
		if (t != null) {
			t.Recuperate ();
		}
	}
}
