using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothDestroyInTimeWithParticles : MonoBehaviour {
	public float TimeToDestroy=10f;
	public TimeType timeType=TimeType.ScaledTime;
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		TimeToDestroy += -TimeManager.GetDeltaTime (timeType);
		if (TimeToDestroy <= 0) {
			var particles = GetComponentsInChildren<ParticleSystem> ();
			foreach (var o in particles) {
				o.transform.parent = null;
				o.Stop ();
				Destroy (o.gameObject, o.main.startLifetime.constant);
			}
			Destroy (this.gameObject);
		}
	}
}
