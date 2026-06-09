using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarksFollower : MonoBehaviour {
	public bool AutoMoveWithoutWay=false;
	public List<MarkToBlocks> Way = new List<MarkToBlocks>();
	public Vector3 RotateFromItToSpeedVector;
	public float Speed=2f;
	public TimeType timeType;
	public int currentPoint=1;
	public bool InverseVerticalWithXPositive;
	void Start()
	{
		Way = new List<MarkToBlocks> ();
		var marks = MarkToBlocks.marks;
		marks.RemoveAll (x => x == null);
		int Key = -1;
		foreach (var m in marks) {
			float Distance = Vector2.Distance (m.transform.position, transform.position);
			if (Distance < m.Size) {
				Key = m.Key;			
				break;
			}
		}
		marks.Sort ((x, y) => (x.orderIndex - y.orderIndex));
		Way = marks.FindAll (x => x.Key == Key);
		currentPoint = Random.Range (1, Way.Count);
		if (Way.Count > 0) {
			transform.position = Vector3.Lerp (Way [currentPoint - 1].transform.position, Way [currentPoint].transform.position, Random.Range (0, 1f));
		} else if (AutoMoveWithoutWay){			
			Destroy (this);
			var ballBehavior = gameObject.AddComponent<BallBehavior> ();
			var rigi = gameObject.GetComponent<Rigidbody2D> ();
			if (rigi == null)
				rigi = gameObject.AddComponent<Rigidbody2D> ();
			rigi.isKinematic = false;
			rigi.gravityScale = -0.3f;
			ballBehavior.Velocity = Speed;
			if (gameObject.GetComponent<Enemy_Ball_MovimientController>()==null)
				gameObject.AddComponent<Enemy_Ball_MovimientController> ();
		}
	}

	void Update()
	{	
		if (Way.Count > 0) {	
			float deltaTime = TimeManager.GetDeltaTime (timeType);
			var oldPos = transform.position;
			transform.position = Vector3.MoveTowards (transform.position, Way [currentPoint].transform.position, Speed * deltaTime);
			var deltaPos = transform.position - oldPos;
			if (Vector3.Distance (transform.position, Way [currentPoint].transform.position) < 0.05f) {
				currentPoint++;
				if (currentPoint >= Way.Count)
					currentPoint = 0;
			}
			if (InverseVerticalWithXPositive) {

				if (deltaPos.x < 0) {
					if (transform.localScale.y < 0)
						transform.localScale = new Vector3 (transform.localScale.x, -transform.localScale.y, transform.localScale.z);
				}

				if (deltaPos.x > 0) {
					if (transform.localScale.y > 0)
						transform.localScale = new Vector3 (transform.localScale.x, -transform.localScale.y, transform.localScale.z);
				}
			}
			transform.rotation = Quaternion.FromToRotation (RotateFromItToSpeedVector, deltaPos.normalized);
		}
	}

}
