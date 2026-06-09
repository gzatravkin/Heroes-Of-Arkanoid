using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockHitter : MonoBehaviour {
	public int MaxHits = 5;
	public int Damage = 1;
	public bool onStayHit=false;
	public OnFinEffect onFinEffect=new OnFinEffect();
	void OnTriggerEnter2D(Collider2D col)
	{
		Hit (col.gameObject);
	}
	void OnTriggerStay2D(Collider2D col)
	{
		Hit (col.gameObject);
	}
	void OnCollisionEnter2D(Collision2D col)
	{
		Hit (col.gameObject);
	}
	void OnCollisionStay2D(Collision2D col)
	{
		Hit (col.gameObject);
	}
	void Hit(GameObject obj)
	{
		if (this.enabled) {
			var block = obj.GetComponent<BlockScript> ();
			if (block != null&&block.Killed==false) {
				block.GetHit (Damage);
				MaxHits--;
				if (MaxHits <= 0)
					Fin ();
			}
		}
	}
	void Fin()
	{
		onFinEffect.Finish (this);
	}
}
