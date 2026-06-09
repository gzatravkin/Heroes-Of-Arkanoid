using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SleepingBatController : MonoBehaviour {

	void OnTriggerEnter2D(Collider2D Col)
	{
		if (Col.GetComponent<BlockScript> () != null&&Col.GetComponent<SleepingBatController>()==null) {
			transform.parent = Col.gameObject.transform;
			if (Col.GetComponent<BlockEffect_KillChildrenOnDie> () == null) {
				Col.gameObject.AddComponent<BlockEffect_KillChildrenOnDie> ();
				Col.GetComponent<BlockScript> ().RefreshBlockEffects ();
			}
		}
	}
}
