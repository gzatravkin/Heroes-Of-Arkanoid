using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarMove : MonoBehaviour {
	[HideInInspector]
	public float defaultMovimientSpeed=5f;
	public float MovimientSpeed=5f;
	private float DestinationXCoor;
	[HideInInspector]		
	public Rigidbody2D rigi;
	private BoxCollider2D boxCollider;
	public void Update()
	{
		var size = boxCollider.size.x;
		float minPosX = GameWallsManager.GetGameFieldRect().xMin+size/2f;
		float maxPosX = GameWallsManager.GetGameFieldRect().xMax-size/2f;
		DestinationXCoor = Mathf.Clamp (DestinationXCoor, minPosX, maxPosX);
		rigi.MovePosition(Vector3.MoveTowards(transform.position, new Vector3(DestinationXCoor,transform.position.y,transform.position.z),Time.deltaTime*MovimientSpeed));
	}
	void Awake()
	{
		defaultMovimientSpeed = MovimientSpeed;
	}
	void Start()
	{
		boxCollider = GetComponent<BoxCollider2D> ();
		//ServiceLocator.GetMissionController().
	}
	public void OnValidate()
	{
		rigi = GetComponent<Rigidbody2D> ();
		if (rigi == null) {
			Debug.LogWarning ("BarMove is disened to use rigibody. Autoadding of it. ");
			rigi = gameObject.AddComponent<Rigidbody2D> ();
		}
	}
	public void SetDestination(float x)
	{
		DestinationXCoor = x;
	}

}
