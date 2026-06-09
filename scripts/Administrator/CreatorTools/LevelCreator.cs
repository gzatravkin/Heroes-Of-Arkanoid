using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelCreator : MonoBehaviour {
	#if UNITY_EDITOR
	[UnityEditor.MenuItem( "GameObject/Create Other/Level" ) ]
	public static void CreateLevel()
	{
		var lvl = new GameObject ("Level");
		lvl.AddComponent<Level> ().OnValidate();

		var fitter = lvl.GetComponent<LevelFitter> ();
		fitter.x = 17;
		fitter.y = 7;
		fitter.OnValidate ();
		lvl.AddComponent<DestroyAllWinCondicion> ();
		if (UnityEditor.Selection.activeGameObject != null)
			lvl.transform.SetParent (UnityEditor.Selection.activeGameObject.transform);
		UnityEditor.Selection.objects = new Object[]{ lvl };
	}
	#endif
	#if UNITY_EDITOR
	[UnityEditor.MenuItem( "GameObject/Create Other/Block" ) ]
	public static void CreateBlock()
	{
		var block = new GameObject ("Block");
		block.AddComponent<BlockLoader> ();
		block.AddComponent<BlockDuplicator> ();
		if (UnityEditor.Selection.activeGameObject != null)
			block.transform.SetParent (UnityEditor.Selection.activeGameObject.transform);
		UnityEditor.Selection.objects = new Object[]{ block };
	}
	#endif

}
