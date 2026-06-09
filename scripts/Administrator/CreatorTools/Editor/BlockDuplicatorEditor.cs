using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor (typeof(BlockDuplicator)),CanEditMultipleObjects]
public class BlockDuplicatorEditor : Editor
{
	public static readonly float minDistance = 0.1f;
	public List<BlockDuplicator> Selected;
	public static bool SymmetricHorizontal , SymmetricVertical ;
	public override void OnInspectorGUI ()
	{
		serializedObject.Update ();
		DrawDefaultInspector ();
		Selected = new List<BlockDuplicator> ();
		Debug.Log (targets.Length);
		for (int i = 0; i < targets.Length; i++) {			
			Selected.Add ((BlockDuplicator)targets [i]);
		}
		if (GUILayout.Button ("Copy Up")) {			
			CopyCommand (0, 1);
		}
		EditorGUILayout.BeginHorizontal ();
		if (GUILayout.Button ("Copy Left")) {			
			CopyCommand (-1, 0);
		}
		if (GUILayout.Button ("Copy Right")) {			
			CopyCommand (1, 0);
		}
		EditorGUILayout.EndHorizontal ();
		if (GUILayout.Button ("Copy Down")) {			
			CopyCommand (0, -1);
		}
		if (SymmetricHorizontal)
			GUILayout.Label ("Symetry horizontal: ON");
		if (SymmetricVertical)
			GUILayout.Label ("Symetry vertical: ON");
			
		serializedObject.ApplyModifiedProperties ();
	}

	void OnSceneGUI ()
	{
		if (Selected == null || Selected.Count == 0)
			return;
		Event e = Event.current;
		if (e.type == EventType.KeyDown) {
			Debug.Log (e.keyCode);
			var keyCode = e.keyCode;
			if (keyCode == KeyCode.W) {			
				CopyCommand (0, 1);
			}
			if (keyCode == KeyCode.A) {			
				CopyCommand (-1, 0);
			}
			if (keyCode == KeyCode.D) {			
				CopyCommand (1, 0);
			}
			if (keyCode == KeyCode.S) {			
				CopyCommand (0, -1);
			}
			if (keyCode == KeyCode.Z) {			
					SymmetricHorizontal = !SymmetricHorizontal;
			}
			if (keyCode == KeyCode.X) {			
				foreach (BlockDuplicator bD in Selected)
					SymmetricVertical = !SymmetricVertical;
			}
		}
	}

	public void CopyCommand (int x, int y)
	{
		var p = CopyElements (x, y);
		if (p.Count > 0) {
			Selection.objects = p.ToArray ();
		}
	}

	public List<GameObject> CopyElements (int x, int y)
	{
		var newObjects = new List<GameObject> ();

		for (int i = 0; i < Selected.Count; i++) {			
			var GO = Selected [i].gameObject;
			var Vec = new Vector3 (x, y, 0);
			var spr = GO.GetComponent<SpriteRenderer> ();
			if (spr != null) {
				Vec.x = spr.sprite.bounds.size.x * GO.transform.lossyScale.x * x;
				Vec.y = spr.sprite.bounds.size.y * GO.transform.lossyScale.y * y;
			}
			var pos = GO.transform.position+GO.transform.rotation * Vec;
			if (IsLibrePoint(pos))
			{
				var Duplicado = GO.Copy ();
				Duplicado.transform.position = Duplicado.transform.position + GO.transform.rotation * Vec;
				newObjects.Add (Duplicado);
				var goSymetric = CreateSym (Duplicado, SymmetricHorizontal, SymmetricVertical);
				foreach (var go in goSymetric)
					Undo.RegisterCreatedObjectUndo (go,"Create "+go.name);
			}
		}
		foreach (var go in newObjects)
			Undo.RegisterCreatedObjectUndo (go,"Create "+go.name);	
		return newObjects;
	}

	public bool IsLibrePoint (Vector3 pos)
	{
		var level = Selected [0].gameObject.GetComponentInParent<Level> ();
		if (level == null)
			return true;
		else {
			var objects = level.GetComponentsInChildren<BlockDuplicator> ();
			foreach (var o in objects) {
				if (o.transform.position.sqadDistance2D (pos) < minDistance * minDistance)
					return false;
			}
		}
		return true;
	}

	public List<GameObject> CreateSym (GameObject Original, bool horizontal, bool vertical)
	{
		var t = new List<GameObject> ();
		if (horizontal) {
			var v3 = Original.transform.position;
			v3.x = -v3.x;
			if (IsLibrePoint (v3)) {
				var GO = Original.Copy ();			
				GO.transform.position = v3;
				//GO.transform.rotation = Quaternion.Euler (new Vector3 (0, 0, -angle));
				t.Add (GO);
			}
		}
		if (vertical) {
			var v3 = Original.transform.position;
			v3.y = -v3.y;
			if (IsLibrePoint (v3)) {
				var GO = Original.Copy ();			
				GO.transform.localPosition = v3;
				//GO.transform.rotation = Quaternion.Euler (new Vector3 (0, 0, angle - 90));
				t.Add (GO);
			}
		}
		if (vertical && horizontal) {
			var v3 = Original.transform.position;
			v3.x = -v3.x;
			v3.y = -v3.y;
			if (IsLibrePoint (v3)) {
			var GO = Original.Copy ();
			GO.transform.localPosition = v3;
			//GO.transform.rotation = Quaternion.Euler (new Vector3 (0, 0, angle + 180));
			t.Add (GO);
			}
		}
		return t;
	}
}
