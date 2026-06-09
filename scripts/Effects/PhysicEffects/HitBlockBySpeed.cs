using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitBlockBySpeed : MonoBehaviour {
	public bool HitItself=true;
	public float minSpeedToHit=3f;
	public int Damage=1;
	public float DumpFactor=2f;
	void OnCollisionEnter2D(Collision2D col)
	{
		var rigi = GetComponent<Rigidbody2D> ();
		float speed = rigi.velocity.magnitude;
		if (speed > minSpeedToHit) {
			var blockScriptOther = col.gameObject.GetComponent<BlockScript> ();
			if (blockScriptOther!=null) {
				blockScriptOther.GetHit (Damage);
				rigi.velocity = rigi.velocity/DumpFactor;
			}
			if (HitItself)
			{
			var blockScript = gameObject.GetComponent<BlockScript> ();
				if (blockScript!=null) {
					blockScript.GetHit (Damage);
			}
			}
		}
	}
}
