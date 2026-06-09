using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleRedirict : MonoBehaviour {
	public GameObject LevelObj;
	// Use this for initialization
	void OnValidate()
	{
		if (LevelObj == null) {
			var levelFitter = GameObject.FindObjectOfType<LevelFitter> ();
			if (levelFitter!=null)
				LevelObj = GameObject.FindObjectOfType<LevelFitter> ().gameObject;
		}
	}
	void Awake () {
		var t = GameObject.FindObjectOfType<Level> ();
		DontDestroyOnLoad (LevelObj);
		var Fitter = GameObject.FindObjectOfType<LevelFitter> ();
		t.Width = Fitter.x;
		t.Height = Fitter.y;
		LevelObj.transform.position = new Vector3 (100, 100, 100);
		LevelObj.gameObject.SetActive (false);
		LevelLoader.LoadLevel (t,false);
	}
	

}
