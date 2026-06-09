using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[AddComponentMenu("LevelEditor/Streched Level")]
public class StrechedLevel : MonoBehaviour {
	public List<BlockScript> blocks;
	public float MinPos=3f;
	#if UNITY_EDITOR
	void OnDrawGizmos()
	{
		Gizmos.color = Color.green;
		Gizmos.DrawLine (new Vector3(-50,MinPos,0),new Vector3(50,MinPos,0));
	}
	#endif
	public float Velocity=1f;
	// Use this for initialization
	void Start () {
		blocks.Clear ();
		blocks.AddRange(GetComponentsInChildren<BlockScript> ());
		blocks.Sort ((x, y) => Comparer.floatSortFunction( (x.transform.position.y),(y.transform.position.y) ));
		BattleEventsManager.Events.BlockDestroyed.AddListener ((x)=>Clean ());
	}
	void Clean()
	{
		blocks.RemoveAll (x => x == null||x.Killed);
	}
	// Update is called once per frame
	void Update () {
		Clean ();
		if (blocks.Count > 0) {			
			var delta = blocks [0].transform.position.y - MinPos;
			if (delta > 0) {
				for (int i = 0; i < blocks.Count; i++) {
					blocks [i].gameObject.SetY (blocks [i].transform.position.y - Velocity * TimeManager.deltaTime);
				}
			}
		}
			
	}
}
