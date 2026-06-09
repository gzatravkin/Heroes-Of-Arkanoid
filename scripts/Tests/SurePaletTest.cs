using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SurePaletTest : MonoBehaviour {

	// Use this for initialization
	void Start () {
		PalletsCreator.CreateSurePallet (() => {
			Debug.Log ("BUTON OK WORKS");
		});
	}
	

}
