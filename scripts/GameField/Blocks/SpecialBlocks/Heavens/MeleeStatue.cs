using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeleeStatue : AbstractStatue {	
	public GameObject[] Bullets;
	public GameObject[] AllyBullets;

	protected override void Hit()
	{		
		if (IsAlly ()) {
			Instantiate (AllyBullets [Mathf.Clamp (Level, 0, AllyBullets.Length - 1)], transform.position, transform.rotation);
		} else {
			Instantiate (Bullets[Mathf.Clamp(Level,0,Bullets.Length-1)], transform.position, transform.rotation);
		}

	}
}
