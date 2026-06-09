using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GameObjectExtended 
{
	public static void KillAllChilds (this GameObject gameObject)
	{		
		{
			var ToKill = new List<GameObject> ();
			for (int i = 0; i < gameObject.transform.childCount; i++) {
				ToKill.Add (gameObject.transform.GetChild (i).gameObject);
			}
			for (int i = 0; i < ToKill.Count; i++) {
				MonoBehaviour.Destroy (ToKill [i]);
			}
		}
	}
	public static void SetX(this GameObject gameObject, float x)
	{
		var v3 = gameObject.transform.position;
		v3.x = x;
		gameObject.transform.position = v3;
	}
	public static GameObject Copy(this GameObject gameObject)
	{
		var Duplicado = MonoBehaviour.Instantiate (gameObject, gameObject.transform.position, gameObject.transform.rotation);
		Duplicado.transform.parent = gameObject.transform.parent;
		Duplicado.transform.localScale = gameObject.transform.localScale;
		Duplicado.name = gameObject.name;
		return Duplicado;
	}
	///<summary>
	///Also set identity local rotaiton
	///</summary>
	public static void SetParentWithScaleOne(this GameObject gameObject, Transform parent, Vector3 localPos =  new Vector3())
	{
		gameObject.transform.SetParent(parent);
		gameObject.transform.localScale = Vector3.one;
		gameObject.transform.localPosition = localPos;
		gameObject.transform.localRotation = Quaternion.identity;
	}
	public static void SetParentByName(this GameObject gameObject, string name)
	{
		var t = GameObject.Find (name);
		if (t == null)
			t = new GameObject (name);
		gameObject.transform.SetParent (t.transform);
	}
	public static void SetY(this GameObject gameObject, float y)
	{
		var v3 = gameObject.transform.position;
		v3.y = y;
		gameObject.transform.position = v3;
	}
	public static void SetZ(this GameObject gameObject, float z)
	{
		var v3 = gameObject.transform.position;
		v3.z = z;
		gameObject.transform.position = v3;
	}
}
