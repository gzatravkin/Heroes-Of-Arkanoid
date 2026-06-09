using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EditorGizmo : MonoBehaviour {
	public GameObject BarObject;
	// Use this for initialization
	void OnDrawGizmos()
	{
		Gizmos.color = Color.red;
		var cam = GameObject.FindObjectOfType<Camera> ();
		var LeftPos = SO_Configuraciones.obj.StartBarPosition-Vector3.left*45;
		Gizmos.DrawLine (LeftPos,LeftPos+Vector3.left*90f);
		if (BarObject != null) {
			BarObject.transform.position = SO_Configuraciones.obj.StartBarPosition;
			BarObject.SetX (cam.transform.position.x);
		}

		Gizmos.color = Color.blue;
		float xsize = cam.orthographicSize * cam.aspect;
		float WidthPart = SpellPanel_List.GetWidthPart ();
		Vector3 leftDownPoint = new Vector3(cam.transform.position.x - xsize,cam.transform.position.y-50f);
		Vector3 leftUpPoint = new Vector3(cam.transform.position.x - xsize,cam.transform.position.y+50f);
		var down = leftDownPoint;
		var top = leftUpPoint;
		Gizmos.DrawLine (top+Vector3.right*2*xsize*WidthPart,down+Vector3.right*2*xsize*WidthPart);
		if (BarObject != null) {
			BarObject.transform.position = SO_Configuraciones.obj.StartBarPosition;
			BarObject.SetX (cam.transform.position.x);
		}
	}
}
