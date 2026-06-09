using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Ball_MovimientController : MonoBehaviour {
	private Rigidbody2D rigi;
	public float MinDistanceToBar=4;
	void Start()
	{
		rigi = GetComponent<Rigidbody2D> ();
	}

	// Update is called once per frame
	void Update () {
		if (rigi != null) {
			if (transform.position.y < SO_Configuraciones.obj.StartBarPosition.y + MinDistanceToBar)
				rigi.velocity = new Vector2 (rigi.velocity.x, Mathf.Abs (rigi.velocity.y));
		}
	}
}
