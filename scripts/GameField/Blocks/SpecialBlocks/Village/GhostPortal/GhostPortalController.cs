using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostPortalController : MonoBehaviour {
	public int LayerNormal,LayerGhost;
	public GhostBallEffect Effect;
	void OnTriggerEnter2D(Collider2D col)
	{		
		if (col.GetComponent<BallController> () != null) {
			if (col.gameObject.layer == LayerNormal) {
				MakeItGhost (col.gameObject);
			} else if (col.gameObject.layer == LayerGhost) {
				MakeItNormal (col.gameObject);
			}
		}
	}
	public void MakeItGhost(GameObject obj)
	{
		obj.layer = LayerGhost;
		var effect = Instantiate (Effect.gameObject);
		effect.SetParentWithScaleOne (obj.transform);
	}
	public void MakeItNormal(GameObject obj)
	{
		obj.layer = LayerNormal;
		var ghostEffect = obj.GetComponentInChildren<GhostBallEffect> ();
		if (ghostEffect != null) {
			ghostEffect.RemoveIt ();
		}
	}
}
