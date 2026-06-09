using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LavaBlockEater : MonoBehaviour {
	public bool Eated=false;
	void OnCollisionEnter2D(Collision2D col)
	{
		if (!Eated) {
			var lavaBlockEater = col.gameObject.GetComponent<LavaBlockEater> ();
			if (lavaBlockEater != null)
				Eat (lavaBlockEater);
		}
	}
	void Eat(LavaBlockEater other)
	{
		if (other.Eated == false) {
			other.Eated = true;
			Destroy (other.GetComponent<Rigidbody2D> ());
			Destroy (other.GetComponent<BallBehavior> ());
			Destroy (other.GetComponent<Enemy_Ball_MovimientController> ());
			var otherBlockScript = other.GetComponent<BlockScript> ();
			GetComponent<BlockScript> ().HP += otherBlockScript.HP;
			other.transform.parent = transform;
		}
	}
}
