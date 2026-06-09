using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class MarkToBlocks : MonoBehaviour {
	public int Key=0;
	public int orderIndex=0;
	public static List<MarkToBlocks> marks = new List<MarkToBlocks>();
	public float Size;
	#if UNITY_EDITOR
	void OnDrawGizmos()
	{
		if (GetComponent<SpriteRenderer>()!=null)
			Gizmos.color = GetComponent<SpriteRenderer>().color;
		Gizmos.DrawWireSphere (transform.position, Size);
	}
	#endif
	// Use this for initialization
	void Awake () {
		if (marks == null)
			marks = new List<MarkToBlocks> ();
		marks.Add (this);
		Destroy (GetComponent<SpriteRenderer>());	
	}
	
}
