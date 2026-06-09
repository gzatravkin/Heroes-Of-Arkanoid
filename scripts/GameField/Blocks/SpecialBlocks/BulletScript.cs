using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletScript : MonoBehaviour {
	public int Damage=10;
	public int maxHits = 1;
	public OnFinEffect onFinEffect;
	public void OnCollisionEnter2D(Collision2D col)
	{		
		Hit (col.gameObject);
	}
	void OnTriggerEnter2D(Collider2D col)
	{
		Hit (col.gameObject);
	}
	void Hit(GameObject obj)
	{
		if (this.enabled) {
			var playerObj = obj.GetComponent<AbstractPlayerController> ();
			if (playerObj != null) {
				playerObj.GetDamage (Damage);
				maxHits--;
			}
			if (maxHits <= 0) {
				onFinEffect.Finish (this);
			}
		}
	}
}
