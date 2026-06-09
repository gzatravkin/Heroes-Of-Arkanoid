using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseBarInput : AbstractBarInput {
	void Update () {
		if (PauseManager.objRef.IsPaused ())
			return;	
			var pos = Camera.main.ScreenToWorldPoint (Input.mousePosition);
			if (battleFieldCamera.IsInBattlePos (pos)) {
			moveComponent.SetDestination (pos.x);
				if (Input.GetMouseButtonUp (0))
					logicComponent.BallLaunch ();
			}	
	}
}
