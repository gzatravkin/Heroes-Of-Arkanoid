using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnionOfSticks : MonoBehaviour
{
	public bool United = false;
	public GameObject unionObj;

	public class UnionOfert
	{
		public UnionOfSticks obj;
		public Rect pos;
		public List<UnionOfSticks> wanted;

		public UnionOfert (UnionOfSticks obj, Rect pos)
		{
			this.obj = obj;
			this.pos = pos;
			wanted = new List<UnionOfSticks> ();
		}
	}

	public static List<UnionOfert> wanted = new List<UnionOfert> ();
	public UnionOfert myOfert;
	public float Tolerance = 1.01f;

	void Awake ()
	{		
		OffertInitialization ();
		if (transform.parent != null && transform.GetComponent<Rigidbody2D> () != null)
			MakeItChild (gameObject, transform.parent.gameObject);
	}

	void OffertInitialization ()
	{
		var box = GetComponent<BoxCollider2D> ();
		var rect = new Rect (transform.position, transform.rotation * box.bounds.size);
		box.enabled = false;
		rect.size = rect.size * Tolerance;
		myOfert = new UnionOfert (this, rect);
		wanted.Add (myOfert);
	}

	bool Overlaps (Rect one, Rect two)
	{
		var b = one.Overlaps (two);
		return b;
	}

	void Start ()
	{
		var box = GetComponent<BoxCollider2D> ();
		box.enabled = true;
		if (United == false) {
			wanted.RemoveAll (x => x == null || x.obj == null);
			wanted.ForEach (x => x.pos.center = x.obj.transform.position);
			List<UnionOfert> recrussiveBlocks = new List<UnionOfert> ();
			recrussiveBlocks.Add (myOfert);
			bool flag = true;
			while (flag) {
				List<UnionOfert> range = wanted.FindAll (x => (
				                             x != null &&
				                             recrussiveBlocks.Find (y => Overlaps (x.pos, y.pos)) != null));
				if (range.Count == 0)
					flag = false;
				recrussiveBlocks.AddRange (range);
				wanted.RemoveAll (x => range.Find (y => x == y) != null);
			}

			var unitedOfert = recrussiveBlocks.Find (x => x.obj.unionObj != null);
			if (unitedOfert != null) {
				for (int i = 0; i < recrussiveBlocks.Count; i++) {
					if (recrussiveBlocks [i].obj != unitedOfert.obj)
						Unite (unitedOfert.obj, recrussiveBlocks [i].obj);
				}		
			} else if (recrussiveBlocks.Count > 1) {
				for (int i = 1; i < recrussiveBlocks.Count; i++) {
					Unite (this, recrussiveBlocks [i].obj);
				}
				United = true;
			}
		}
	}

	public static void Unite (UnionOfSticks first, UnionOfSticks second)
	{
		if (!first.United && second.United) {
			var t = first;
			first = second;
			second = t;
		}
					
		if (second.unionObj != null)
			MakeItChild (first.gameObject, second.unionObj);
		else {
			if (first.unionObj == null) {
				if (first.transform.parent != null && first.transform.parent.GetComponent<Rigidbody2D> () != null)
					first.unionObj = first.transform.parent.GetComponent<Rigidbody2D> ().gameObject;
				else {
					first.unionObj = new GameObject ("Union");	
					first.unionObj.transform.parent = first.transform.parent;
					first.unionObj.AddComponent<Rigidbody2D> ();
				}
				MakeItChild (first.gameObject, first.unionObj);
			}
			MakeItChild (second.gameObject, first.unionObj);
		}
	}

	public static void MakeItChild (GameObject obj, GameObject unionObj)
	{
		if (obj.GetComponent<Rigidbody2D> () != null)
			Destroy (obj.GetComponent<Rigidbody2D> ());				
		obj.transform.parent = unionObj.transform;
		if (obj.GetComponent<UnionOfSticks> () != null) {
			obj.GetComponent<UnionOfSticks> ().United = true;
			obj.GetComponent<UnionOfSticks> ().unionObj = unionObj;
		}
	}
}
