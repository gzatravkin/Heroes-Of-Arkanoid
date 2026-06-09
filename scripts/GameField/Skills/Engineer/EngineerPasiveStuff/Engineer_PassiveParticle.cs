using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Engineer_PassiveParticle : MonoBehaviour {
	public UnityEngine.Events.UnityEvent BarLanded = new UnityEngine.Events.UnityEvent();
	public void OnTriggerEnter2D(Collider2D col)
	{
		if (col.GetComponent<AbstractPlayerController>()!=null)
		{
			Destroy (this.gameObject);
			BarLanded.Invoke ();
		}
	}

}
