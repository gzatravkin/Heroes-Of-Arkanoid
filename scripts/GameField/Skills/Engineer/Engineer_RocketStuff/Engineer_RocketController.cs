using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Engineer_RocketController : MonoBehaviour {
	private float CreateTime;
	public float TimeToPermiteControll = 2f;
	private Vector2 RocketVelocity;
	public float MaxSpeed=5f;
	public float Acceleration=2f;
	public GameObject Explotion;
	public float DistanceToExplote=1f;
	public int MaxHits = 5;
	// Use this for initialization
	void Start () {
		CreateTime = TimeManager.GetTime (TimeType.RealTime);
	}
	
	// Update is called once per frame
	void Update () {
		Vector2 pointToGo = (Vector2)transform.position + Vector2.up;
		if (TimeManager.GetTime (TimeType.RealTime) - CreateTime > TimeToPermiteControll) {			
			pointToGo = Camera.main.ScreenToWorldPoint (Input.mousePosition);
		} 
		var Dirrecion = (pointToGo - (Vector2)transform.position).normalized;
		float deltaTime = TimeManager.GetDeltaTime (TimeType.RealTime);
		RocketVelocity = Vector2.MoveTowards (RocketVelocity, Dirrecion*MaxSpeed, Acceleration * deltaTime);
		if (RocketVelocity.magnitude > MaxSpeed)
			RocketVelocity = RocketVelocity.normalized * MaxSpeed;
		transform.transform.position =transform.transform.position+  (Vector3)(RocketVelocity * deltaTime);
		transform.rotation = Quaternion.FromToRotation (Vector3.up, RocketVelocity.normalized);
		var t = GameField.objRef.GetClosestBlock (transform.position, DistanceToExplote);
		if (t!=null)
		{
			Boom ();
		}
	}
	void Boom ()
	{
		var t = Instantiate (Explotion, transform.position, Quaternion.identity);
		t.transform.localScale = transform.localScale;
		t.GetComponent<BlockHitter> ().MaxHits = MaxHits;
		Destroy (this.gameObject);
	}
}
