
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public class BlockBaker : MonoBehaviour
{
	[System.Serializable]
	public struct BlockData
	{
		public Transform Parent;
		public Vector2 localPos;
		public Vector3 localScale;
		public Quaternion localRotation;
		public GameObject Prefab;
	}

	public bool Bake;
	public List<BlockData> blockData;
	public void Awake()
	{
		for (int i = 0; i < blockData.Count; i++) {
			var block = blockData [i];
			BlockLoader.LoadBlock (block.Parent, block.Prefab, block.localPos, block.localRotation, block.localScale);
		}
		//Destroy (this);
	}
	public void Check ()
	{		
		if (blockData!=null&&blockData.Count == 0) {
			if (!Bake)
				return;
			var Blocks = gameObject.GetComponentsInChildren<BlockLoader> (true);
				foreach (BlockLoader bd in Blocks) {
					if (!bd.CustomMode) {
						var blockInformacion = new BlockData ();
						blockInformacion.localPos = bd.transform.localPosition;
						blockInformacion.localRotation = bd.transform.localRotation;
						blockInformacion.localScale = bd.transform.localScale;
						blockInformacion.Parent = bd.transform.parent;
						if (bd.CustomObject!=null)						
							blockInformacion.Prefab = bd.CustomObject;
						blockData.Add (blockInformacion);
						DestroyImmediate (bd.gameObject,true);						
					}
				}
			}
		 else {							
			if (Bake)
				return;
			foreach (BlockData bd in blockData) {
				if (bd.Prefab == null)
					continue;
				GameObject temp = new GameObject (bd.Prefab.name);
				var blockLoader = temp.gameObject.AddComponent<BlockLoader> ();
				temp.gameObject.AddComponent<BlockDuplicator>();
				blockLoader.CustomObject = bd.Prefab;
				blockLoader.transform.parent = bd.Parent;
				blockLoader.transform.localPosition = bd.localPos;
				blockLoader.transform.localRotation = bd.localRotation;
				blockLoader.transform.localScale = bd.localScale;
				blockLoader.Initialization ();
			}
			blockData.Clear ();
		}
	}
}
#if UNITY_EDITOR
[CustomEditor(typeof(BlockBaker))]
public class BlockBaker_Editor : Editor {
	void OnSceneGUI()
	{
		((BlockBaker)target).Check ();
	}
}
#endif