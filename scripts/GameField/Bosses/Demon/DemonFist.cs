using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemonFist : MonoBehaviour {
	public float Damage;
	public bool CanHit = false;
	public bool OnlyOneHit=true;
	public GameObject HitEffect;
	public float EffectDuration = 5f;
	void OnTriggerEnter2D(Collider2D col)
	{
		if (CanHit)
		{
		var player = col.GetComponent<AbstractPlayerController> ();
			if (player != null) {
				player.GetDamage (Damage);
				if (HitEffect != null) {
					var t = (GameObject)Instantiate (HitEffect, player.gameObject.transform.position, Quaternion.identity);
					Destroy (t, EffectDuration);
				}
				if (OnlyOneHit)
					CanHit=false;				
			}
		}
	}
}
