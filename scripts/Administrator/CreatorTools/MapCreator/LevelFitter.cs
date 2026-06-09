using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[AddComponentMenu("LevelEditor/LevelSizeController")]
public class LevelFitter : MonoBehaviour {
	public float x,y;
	#if UNITY_EDITOR
	private Camera cam;
	public void OnValidate()
	{		
		if (UnityEditor.PrefabUtility.GetPrefabParent(gameObject)==null&&UnityEditor.PrefabUtility.GetPrefabObject(gameObject)!=null)
			return;//return if its prefab
		transform.position = Vector3.zero;
		if (x < 1)
			x = 1;
		if (y < 1)
			y = 1;
		if (cam == null)
			cam = GameObject.FindObjectOfType<Camera> ();				
		var batleFieldCameraController = cam.GetComponent<BattleFieldCameraController> ();
		if (batleFieldCameraController !=null)
			batleFieldCameraController .SetSize (new Vector2 (x, y), SpellPanel_List.GetWidthPart ());
		}
		#endif
	}
	

