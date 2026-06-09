using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DragAndDropCamera : MonoBehaviour {	
	bool drag = false;
	public float Speed;
	Vector2 LastPos;
	public Vector2 MinPos;
	public Vector2 MaxPos;
	public bool X,Y;
	void Start()
	{
		LastPos = Input.mousePosition;
	}
	// Update is called once per frame
	void Update () {		
		if (Input.GetMouseButtonDown (0)) {
			drag = true;
			LastPos = Input.mousePosition;
		}
		if (Input.GetMouseButtonUp (0)) {
			drag = false;
		}
		if (drag) {
			var deltaMouse = (Vector2)Input.mousePosition - LastPos;
			Vector2 deltaCamPos = deltaMouse * Speed;
			if (!X)
				deltaCamPos.x = 0;
			if (!Y)
				deltaCamPos.y = 0;
			Vector3 newPos = transform.position + (Vector3)deltaCamPos;
			newPos.x = Mathf.Clamp (newPos.x,MinPos.x, MaxPos.x);
			newPos.y = Mathf.Clamp (newPos.y,MinPos.y, MaxPos.y);
			transform.position = newPos;
			LastPos = Input.mousePosition;
		}
	}
}
