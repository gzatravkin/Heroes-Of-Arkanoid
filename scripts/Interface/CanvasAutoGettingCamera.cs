using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanvasAutoGettingCamera : MonoBehaviour {
	private Canvas canvas;
	// Use this for initialization
	void Start () {
		GetComponent<Canvas> ().worldCamera = Camera.main;
	}
	void Update()
	{
		if (canvas == null)
			canvas = GetComponent<Canvas> ();
		if (canvas.worldCamera == null)
			canvas.worldCamera = Camera.main;
	}
	void OnValidate()
	{
		GetComponent<Canvas> ().worldCamera = Camera.main;
	}

}
