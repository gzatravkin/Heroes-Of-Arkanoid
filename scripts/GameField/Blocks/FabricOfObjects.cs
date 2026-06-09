using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FabricOfObjects : MonoBehaviour {
	public GameObject sample;
	public TimeType timeType;
	public float TimeToCreate;
	public float StartTime=0f;
	public bool RandomizeStartTime;
	public Vector3 localPos;
	public float SizeMultiplayer=1f;
	public bool MakeChild;
	public float randomCircleRadius;
	public int MaxObjects=4;
	public bool IgnoreCollisonWithChild=true;
	private List<GameObject> liveObjects = new List<GameObject> ();
	public UnityEngine.Events.UnityEvent Prepared = new UnityEngine.Events.UnityEvent ();
	public float TimeAfterPreparedToMake = 0.5f;
	private float Counter = 0f;
	private bool Evented=false;
	private Collider2D OwnCollider;
	void Start()
	{
		OwnCollider = GetComponent<Collider2D> ();
		if (RandomizeStartTime)
			Counter = Random.Range (0, TimeToCreate - TimeAfterPreparedToMake);
		else
			Counter = StartTime;
	}
	void OnValidate()
	{
		if (RandomizeStartTime)
			StartTime = Random.Range (0, TimeToCreate-TimeAfterPreparedToMake);
	}
	void Update()
	{		
		Counter += TimeManager.GetDeltaTime (timeType);
		if (Counter > TimeToCreate - TimeAfterPreparedToMake&&Evented==false) {
			Prepared.Invoke ();
			Evented = true;
		}
		if (Counter > TimeToCreate) {
			liveObjects.RemoveAll (x => x == null);
			if (liveObjects.Count<MaxObjects)
				{
				Counter = 0;
				Evented = false;
				var t = Instantiate (sample);
				if (IgnoreCollisonWithChild) {
					var col = t.GetComponent<Collider2D> ();
					if (col != null)
						Physics2D.IgnoreCollision (OwnCollider, col);
				}
				t.SetParentWithScaleOne(transform,localPos+((Vector3)(Random.insideUnitCircle*randomCircleRadius)));	
				t.transform.localScale = t.transform.localScale * SizeMultiplayer;
				if (!MakeChild)
					t.SetParentByName("FabricObjects");
				liveObjects.Add (t);
				}
		}
	}
}
