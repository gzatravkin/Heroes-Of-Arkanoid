using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoadEditor : MonoBehaviour
{
	public ListOfMaps listOfMaps;
	public HeroRoad roadToEdit;
	public List<RoadElement> Selected = new List<RoadElement> ();
	private float inputLastTime=0;
	public float TimeToMoveObjects=0.1f;
	void OnDrawGizmos()
	{
		if (Selected.Count > 0) {
			Gizmos.color = Color.white;
			Gizmos.DrawWireSphere (FindButtonAssociated(Selected[0]).transform.position, 10f);
		}
	}
	void Start ()
	{
		Refresh ();
		inputLastTime = 0;
	}

	void Refresh ()
	{
		listOfMaps.ShowList (roadToEdit);
		foreach (var o in listOfMaps.buttonsData) {			
			var roadElement = o.levelAsociated;
			var btnScript = o.button.GetComponent<Button> ();
			btnScript.interactable = true;
			btnScript.onClick.AddListener (() => Select (roadElement));
		}
	}

	void Select (RoadElement level)
	{		
		if (Selected.Contains (level)) {
			Selected.Remove (level);
		} else {
			Selected.Add (level);
			Debug.Log (level.levelObj.name);
			if (Selected.Count >= 2) {
				MakeRelation (Selected [1], Selected [0]);
				Selected.Clear ();
				Refresh ();
			}
		}
	}

	public GameObject FindButtonAssociated(RoadElement element)
	{
		return listOfMaps.buttonsData.Find (x => x.levelAsociated == element).button;
	}
	public void MakeRelation (RoadElement element1, RoadElement element2)
	{
		if (element1 == element2) {			
			return;
		}
		if (element1.ContainsReference (element2)) {
			var relacion = element1.GetObjectReference (element2);
			if (!relacion.Rigid) {
				relacion.Rigid = true;
			} else {
				element1.RemoveReference (element2);
			}
		} else {		
			if (!element2.ContainsReference (element1))
				element1.AddReference (element2.UniqueCode, false);
		}	
	}
	// Update is called once per frame
	void Update ()
	{
		if (Input.GetKeyDown (KeyCode.UpArrow)) {
			inputLastTime = Time.time;
			MoveSelectedObjects (0, 0.5f);
		}
		if (Input.GetKeyDown (KeyCode.DownArrow)) {
			inputLastTime = Time.time;
			MoveSelectedObjects (0, -0.5f);
		}
		if (Input.GetKeyDown (KeyCode.RightArrow)) {
			inputLastTime = Time.time;
			MoveSelectedObjects (0.5f, 0);
		}
		if (Input.GetKeyDown (KeyCode.LeftArrow)) {
			inputLastTime = Time.time;
			MoveSelectedObjects (-0.5f, 0);
		}
		if (Time.time - TimeToMoveObjects > inputLastTime) {
			inputLastTime = Time.time;
			if (Input.GetKey (KeyCode.UpArrow)) {
				MoveSelectedObjects (0, 0.5f);
			}
			if (Input.GetKey (KeyCode.DownArrow)) {
				MoveSelectedObjects (0, -0.5f);
			}
			if (Input.GetKey (KeyCode.RightArrow)) {
				MoveSelectedObjects (0.5f, 0);
			}
			if (Input.GetKey (KeyCode.LeftArrow)) {
				MoveSelectedObjects (-0.5f, 0);
			}
		}
		if (Input.GetKeyDown (KeyCode.LeftControl)) {
			foreach (var s in Selected) {				
				s.RemoveAllReferences ();
			}
			Selected.Clear ();
			Refresh ();
		}
		if (Input.GetKeyDown (KeyCode.Space)) {
			Selected.Clear ();
			Refresh ();
		}
	}

	void MoveSelectedObjects (float x, float y)
	{
		foreach (var o in Selected) {
			o.ElementPos.x += x;
			o.ElementPos.y += y;
		}
		Refresh ();
	}

	public void SaveChanges ()
	{
		#if UNITY_EDITOR
		UnityEditor.EditorUtility.SetDirty (roadToEdit);
		UnityEditor.AssetDatabase.SaveAssets ();
		#endif

	}
}
