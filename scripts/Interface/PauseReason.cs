using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseReason : MonoBehaviour {

	// Use this for initialization
	void OnEnable () {
		if (PauseManager.objRef!=null)			
			PauseManager.objRef.PauseReasons.Add (this);	
	}

}
