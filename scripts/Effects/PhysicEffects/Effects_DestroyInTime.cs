using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Effects_DestroyInTime : MonoBehaviour {
	private float Counter;
	public float TimeToDestroy;
	public TimeType timeType=TimeType.BattleTime;
	public DieAnimation dieAnimation;
	// Update is called once per frame
	void Update () {
		Counter += TimeManager.GetDeltaTime (timeType);
		if (Counter >= TimeToDestroy) {
			dieAnimation.Die (gameObject);
			Destroy (this.gameObject);
		}
	}
}
