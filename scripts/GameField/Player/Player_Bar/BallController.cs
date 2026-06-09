using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallController : MonoBehaviour {
	public const int BallDamage = 1;
	[HideInInspector]
	public UnityEvent_Block BlockHitted = new UnityEvent_Block ();
	public Rigidbody2D rigi;
	public void Awake()
	{
		rigi = GetComponent<Rigidbody2D> ();
	}
	public bool IsVisible()
	{
		return true;
	}
	void TryToHitBlock(GameObject obj)
	{
		var blockScript = obj.GetComponent<BlockScript> ();
		if (blockScript != null) {
			BattleEventsManager.Events.BallBlockCollision.Invoke (blockScript);
			blockScript.GetHit (BallDamage,DamageType.Ball);
			BlockHitted.Invoke (blockScript);
		}
	}
	void OnTriggerEnter2D(Collider2D col)
	{
		TryToHitBlock (col.gameObject);
	}
	public void OnCollisionEnter2D(Collision2D col)
	{		
		TryToHitBlock (col.gameObject);
		if (col.gameObject.tag == GameWallsManager.LeftWallTag)
			BattleEventsManager.Events.Ball_LeftWallCollision.Invoke (transform.position);
		if (col.gameObject.tag == GameWallsManager.RightWallTag)
			BattleEventsManager.Events.Ball_RightWallCollision.Invoke (transform.position);
		if (col.gameObject.tag == GameWallsManager.TopWallTag)
			BattleEventsManager.Events.Ball_TopWallCollision.Invoke (transform.position);
	}

}
