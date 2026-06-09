using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[AddComponentMenu("LevelEditor/Condiciones/DestroySomethingWinCondicion")]
public class DestroySomethingWinController: AbstractMissionWinCondicion {			
	public List <GameObject> ToDestroy;
	protected override void ControllerInitizliation ()
	{
		base.ControllerInitizliation ();

	}
	void OnValidate()
	{
		for (int i=0;i<ToDestroy.Count;i++) {
			if (ToDestroy [i].GetComponent<BlockDuplicator> () != null) {
				Debug.LogWarning ("Cannot add object with BlockDuplicator component because it will be autodestroyed at start of the game. To select some blocks set them parent");
			}
		}
	}
	void Update()
	{
		for (int i = 0; i < ToDestroy.Count; i++) {
			if (ToDestroy [i] == null)
				ToDestroy.RemoveAt (i);
		}
		if (ToDestroy.Count == 0)
			SetState (MissionCondicionState.Winned);
	}
}
