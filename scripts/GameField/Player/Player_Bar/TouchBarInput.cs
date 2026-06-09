using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TouchBarInput : AbstractBarInput {
	int Index=-1;
	void Update () {
		if (PauseManager.objRef.IsPaused ())
			return;		
		if (Index != -1) {
			var touch = Input.GetTouch (Index);
			var pos = Camera.main.ScreenToWorldPoint (touch.position);
			if (battleFieldCamera.IsInBattlePos (pos)) {
				moveComponent.SetDestination (pos.x);
				if (touch.phase == TouchPhase.Ended)
					logicComponent.BallLaunch ();
			}	
		} else
			TryGetIndex ();
		if (Index != -1 && (Input.GetTouch (Index).phase == TouchPhase.Ended || Input.GetTouch (Index).phase == TouchPhase.Canceled))
			Index = -1;
	}
	void TryGetIndex()
	{
		foreach (var t in Input.touches) {
			var pos = Camera.main.ScreenToWorldPoint (t.position);
			if (t.phase == TouchPhase.Began && battleFieldCamera.IsInBattlePos (pos)) {
				Index = t.fingerId;
				return;
			}
		}
		Index = -1;
	}
}
