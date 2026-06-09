using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Effects_SaveChildOnDestroy : MonoBehaviour {
	void OnDestroy()
	{
		var child = GetComponentsInChildren<Transform> ();
		foreach (var t in child) {
			t.parent = null;
		}
	
	}
}
