using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BlockLoader)),CanEditMultipleObjects]
public class BlockLoader_Editor : Editor {	
	public List<BlockLoader> Selected;
	void OnSceneGUI()
	{		
		if (Selected==null||Selected.Count == 0)
			return;
		Event e = Event.current;
		if (e.type == EventType.KeyDown) {
			var keyCode = e.keyCode;	
			if(keyCode==KeyCode.C) {			
				foreach (BlockLoader bD in Selected) {
					bD.CollectorNumber++;			
					if (bD.CollectorNumber >= GetCounOfCollectors ()) 
						bD.CollectorNumber = 0;					
					bD.RefreshBlock ();
					bD.SetIco ();
					Selected = null;
				}
			}
			if(keyCode==KeyCode.V) {			
				foreach (BlockLoader bD in Selected) {
					bD.BlockNumber++;
					if (bD.BlockNumber >= GetMaxBlock (bD))
						bD.BlockNumber = 0;
					bD.RefreshBlock ();
					bD.SetIco ();
					Selected = null;
				}
			}
		}

	}		
	public static int GetCounOfCollectors()
	{
		return SO_Configuraciones.obj.BlockCollectors.Count;
	}
	public static int GetMaxBlock(BlockLoader Obj)
	{
		return SO_Configuraciones.obj.BlockCollectors [Obj.CollectorNumber].Blocks.Length;
	}
	void ShowListOfBlocks(GameObject[] Blocks)
	{
		int columnNumber = 3;
		float btnWidth = EditorGUIUtility.currentViewWidth/columnNumber-17;
		var texture = new Texture2D(1, 1);

		for (int b = 0; b <= Blocks.Length/columnNumber; b++) {
			EditorGUILayout.BeginHorizontal ();
			for (int i = b*columnNumber; i < (b*columnNumber)+columnNumber&&i<Blocks.Length; i++) {										
				var spriteRenderer = Blocks [i].gameObject.GetComponentInChildren<SpriteRenderer> ();
				if (spriteRenderer!=null&&spriteRenderer.sprite != null)
					texture = spriteRenderer.sprite.texture;
				EditorGUI.BeginDisabledGroup (Selected[0].BlockNumber==i&&Selected.Count==1);
				if (GUILayout.Button (texture, GUILayout.Width (btnWidth), GUILayout.Height (50))) {										
					foreach (BlockLoader bL in Selected) {			
						Undo.RecordObject (bL,"Setting type of block");
						bL.BlockNumber = i;
						bL.CollectorNumber = Selected [0].CollectorNumber;
						bL.SetBlock (Selected [0].CollectorNumber, i);
					}
			
				}
				EditorGUI.EndDisabledGroup ();
			}
			EditorGUILayout.EndHorizontal ();
		}
	}
	void ShowListOfBlockCollectors(SO_BlocksCollector[] Locations)
	{
		float btnWidth = EditorGUIUtility.currentViewWidth/Locations.Length-12;
		EditorGUILayout.BeginHorizontal ();
		for (int i = 0; i < Locations.Length; i++) {
			var texture = new Texture2D(1024, 1024);
			if (Locations[i].Ico!=null)
				texture = Locations[i].Ico.texture;
			EditorGUI.BeginDisabledGroup (Selected[0].CollectorNumber==i&&Selected.Count==1);
				if (GUILayout.Button (texture, GUILayout.Width (btnWidth), GUILayout.Height (50))) {				
					foreach (BlockLoader bL in Selected) {												
					Undo.RecordObject (bL,"Setting location of block");
					bL.CollectorNumber = i;
					bL.SetBlock (i, Selected [0].BlockNumber);
					}
				}
				EditorGUI.EndDisabledGroup ();
			}
			EditorGUILayout.EndHorizontal ();
	}
	public void DrawStandart()
	{		
		var Obj = Selected [0];
		var BlockCollectors = SO_Configuraciones.obj.BlockCollectors;
		GameObject[] Blocks = new GameObject[0];
		if (Obj.CollectorNumber >= 0) {
			Blocks = BlockCollectors [Obj.CollectorNumber].Blocks;
		}
		if (Obj.BlockNumber >= Blocks.Length)
			Obj.BlockNumber = Blocks.Length - 1;
		var listLocationes = new string[BlockCollectors.Count];
		for (int i = 0; i < listLocationes.Length; i++) {
			listLocationes [i] = BlockCollectors [i].name;
		}
		var listBlocks = new string[Blocks.Length];
		for (int i = 0; i < listBlocks.Length; i++) {
			listBlocks [i] = Blocks [i].name;
		}
		var AllSameLocation = Selected.TrueForAll (x => x.CollectorNumber == Selected [0].CollectorNumber);
		var AllSameBlock = Selected.TrueForAll (x => x.BlockNumber == Selected [0].BlockNumber);
		if (!AllSameLocation)
			EditorGUILayout.HelpBox("Some blocks have distant location. All will be the same if you change location number value.",MessageType.Info);
		if (!AllSameBlock)
			EditorGUILayout.HelpBox("Some blocks have distant block type. All will be the same if you change block value.",MessageType.Info);				
		EditorGUILayout.BeginHorizontal ();

		EditorGUI.BeginDisabledGroup (true);
		EditorGUILayout.Popup (Obj.CollectorNumber, listLocationes);
		EditorGUILayout.Popup (Obj.BlockNumber, listBlocks);
		EditorGUI.EndDisabledGroup ();
		EditorGUILayout.EndHorizontal ();
		ShowListOfBlockCollectors (BlockCollectors.ToArray());
		EditorGUILayout.Space ();
		ShowListOfBlocks (Blocks);
		//if (Selected [0].CollectorNumber != CollectorNumber || Selected [0].BlockNumber != BlockNumber)
				//}
	}
	void TryToBreakPrefabConection(GameObject obj)
	{
		if (UnityEditor.PrefabUtility.GetPrefabParent (obj) == null && UnityEditor.PrefabUtility.GetPrefabObject (obj) != null)
			return;
	}
	public override void OnInspectorGUI()
	{

		serializedObject.Update ();
		Selected = new List<BlockLoader> ();
		foreach (Object o in targets) {
			Selected.Add ((BlockLoader)o);
			TryToBreakPrefabConection (((BlockLoader)o).gameObject);
		}
		Selected.Sort ((BlockLoader x, BlockLoader y) => (x.transform.GetSiblingIndex () - y.transform.GetSiblingIndex ()));
		bool customMode = Selected [0].CustomMode;
		if (!customMode)
			DrawStandart ();
		else
			EditorGUILayout.PropertyField(serializedObject.FindProperty("CustomObject"));
		EditorGUILayout.PropertyField(serializedObject.FindProperty("CustomMode"));
		serializedObject.ApplyModifiedProperties ();
	}

}
