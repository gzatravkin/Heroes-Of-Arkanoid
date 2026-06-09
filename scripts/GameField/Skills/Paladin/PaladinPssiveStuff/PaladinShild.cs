using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaladinShild : MonoBehaviour {
	public float reflectionSpeed;
	public int MaxHits;
	private Rigidbody2D rigi;
	private GraficEffect animaObj;
	public void Start()
	{
		rigi = GetComponent<Rigidbody2D> ();
		animaObj = GetComponent<GraficEffect> ();
	}
	void OnTriggerEnter2D(Collider2D col)
	{		
		var Bullet = col.GetComponent<BulletScript>();
		if (Bullet!=null)
		{
			animaObj.BeginAnimation ();
			var otherRigi = col.attachedRigidbody;
			otherRigi.isKinematic = true;
			Vector2 offest = otherRigi.position-rigi.position;
			otherRigi.velocity = offest.normalized * reflectionSpeed;
			otherRigi.gameObject.AddComponent<DieWhenItInvisible> ();
			var hitter = col.gameObject.AddComponent<BlockHitter> ();
			hitter.MaxHits = MaxHits;
		}
	}
}
