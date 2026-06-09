using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockLoader : MonoBehaviour
{
	public bool CustomMode = false;
	public int CollectorNumber = -1;
	public int BlockNumber = -1;
	public GameObject CustomObject;

	public void TryToGetData ()
	{
		if (CustomObject != null&&CollectorNumber==-1&&BlockNumber==-1) {
			for (int i = 0; i < SO_Configuraciones.obj.BlockCollectors.Count; i++) {
				for (int b = 0; b < SO_Configuraciones.obj.BlockCollectors [i].Blocks.Length; b++) {	
					if (SO_Configuraciones.obj.BlockCollectors [i].Blocks [b] == CustomObject) {
						CollectorNumber = i;
						BlockNumber = b;
						break;
					}
				}
			}
		}
	}
	public void Initialization()
	{
		TryToGetData ();
		SetIco ();
	}
	public void OnValidate ()
	{		
		if (CustomObject == null)
			TryToGetData ();
		SetIco ();
	}
	public static int GetLocation(GameObject prefab)
	{
		for (int i = 0; i < SO_Configuraciones.obj.BlockCollectors.Count; i++) {
			for (int b = 0; b < SO_Configuraciones.obj.BlockCollectors [i].Blocks.Length; b++) {	
				if (SO_Configuraciones.obj.BlockCollectors [i].Blocks [b] == prefab) {
					return i;
				}
			}
		}
		return -1;
	}
	public static void LoadBlock(Transform parent, GameObject prefab, Vector2 localPos, Quaternion localRotation, Vector3 localScale)
	{		
		if (prefab == null)
			return;
		var objToLoad = prefab;
		var t = Instantiate (objToLoad, Vector3.zero, Quaternion.identity);
		t.transform.parent = parent;
		t.transform.localPosition = localPos;
		t.transform.localRotation = localRotation;
		t.name = objToLoad.name;
		t.transform.localScale = localScale;
	}
	public void Awake ()
	{		
		LoadBlock (transform.parent,CustomObject,transform.localPosition,transform.localRotation,transform.localScale);
		Destroy (this.gameObject);

	}

	private static GameObject GetBlock (int CollectorNumber, int blockNumber)
	{
		return SO_Configuraciones.obj.BlockCollectors [CollectorNumber].Blocks [blockNumber];
	}
	public void SetBlock(GameObject Prefab)
	{
		CustomObject = Prefab;
		TryToGetData ();
		SetIco ();
	}
	public void SetBlock(int BlockCollector, int BlockNumber)
	{
		this.CollectorNumber = BlockCollector;
		this.BlockNumber = BlockNumber;
		if (SO_Configuraciones.obj.BlockCollectors.Count > CollectorNumber && SO_Configuraciones.obj.BlockCollectors [CollectorNumber].Blocks.Length > BlockNumber && CollectorNumber >= 0 && BlockNumber >= 0) 
		SetBlock(SO_Configuraciones.obj.BlockCollectors[CollectorNumber].Blocks[BlockNumber]);
	}
	public void RefreshBlock()
	{
		SetBlock (CollectorNumber,BlockNumber);
	}
	public void SetIco ()
	{
		var spriteRenderer = GetComponent<SpriteRenderer> ();
		if (spriteRenderer == null)
			spriteRenderer = gameObject.AddComponent<SpriteRenderer> ();		
		var block = CustomObject;
		if (block != null) {
			var spriteRendererToUse = block.GetComponent<SpriteRenderer> ();
			if (spriteRendererToUse == null)
				spriteRendererToUse = block.GetComponentInChildren<SpriteRenderer> ();
			if (spriteRendererToUse != null) {
				spriteRenderer.sprite = spriteRendererToUse.sprite;
				Color colorToSet = spriteRendererToUse.color;
				//colorToSet.a = spriteRenderer.color.a;
				spriteRenderer.color = colorToSet;			
			}
			gameObject.name = block.name;			
		}
	}
}
