using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(MissionWinController)),RequireComponent(typeof(LevelFitter)),RequireComponent(typeof(LevelFitter))]
public class Level : MonoBehaviour {	
	public TextTranslation Descripcion;
	public Sprite Ico;
	public MissionWinController WinController;
	public Premy premy = new Premy();
	public int Location=-1;
	public bool AutoSetLocation=false;
	[HideInInspector]
	public float Width,Height;
	public void OnValidate()
	{		
		transform.position = Vector3.zero;
		if (Location == -1 || AutoSetLocation) {
			Location = GetAutoLocation ();
			AutoSetLocation = false;
		}
		var levelFitter = GetComponent<LevelFitter> ();
		if (levelFitter!= null) {
			Width = levelFitter.x;
			Height = levelFitter.y;
		}
		if (GetComponent<MissionWinController> () == null) {
			gameObject.AddComponent<MissionWinController> ();
			Debug.LogWarning ("MissionWinContorller component requiere for correct work of level. It was autoadded to level prefab. ");
		}
		WinController = GetComponent<MissionWinController> ();
	}
	public TextTranslation GetDescripcion()
	{
		return Descripcion;
	}
	public void Initialization(GameField gameField)
	{				
		var Level = Instantiate (gameObject, Vector3.zero, Quaternion.identity) as GameObject;
		Level.SetActive (true);
		var actualWinController = Level.GetComponent<MissionWinController> ();
		var ListOfObjects = new List<GameObject> ();
		var transforms = Level.GetComponentsInChildren<Transform>(true); 
		for (int i = 0; i < transforms.Length; i++) {
			ListOfObjects.Add (transforms[i].gameObject);
		}
		gameField.SetLevelObjects (ListOfObjects,Level);
		gameField.SetPrefieredBattleSize (new Vector2 (Width,Height));
		actualWinController.Initialization ();
	}
	int GetAutoLocation()
	{
		int[] blockTypes = new int[SO_Configuraciones.obj.Locationes.Count];

			var blocks = GetComponentsInChildren<BlockLoader> ();
		foreach (BlockLoader b in blocks) {
			var location = SO_Configuraciones.obj.BlockCollectors [b.CollectorNumber].associatedLocation;
			if (location!=null)
				blockTypes [location.GetLocationIndex()]++;
		}
			var blockBaker = GetComponent<BlockBaker>();
			if (blockBaker != null) {
				if (blockBaker.blockData != null && blockBaker.blockData.Count > 0) {
				foreach (BlockBaker.BlockData b in blockBaker.blockData) {
					var location = BlockLoader.GetLocation(b.Prefab);
					if (location >=0)
						blockTypes [location]++;
				}
			}
		}
		int returnLocation = 0;
		int max = blockTypes[returnLocation ];
		for (int i = 0; i < blockTypes.Length; i++) {
			if (blockTypes [i] > max) {
				max = blockTypes [i];
				returnLocation = i;
			}
		}
		return returnLocation;
	}
}
[System.Serializable]
public class Premy
{
	public float exp;
	public int CrystalesGreen, CrystalesRed, CrystalesBlue;
	public int TreasurePoints = 0;
	public int RandomItems=0;
	public Premy ()
	{
		exp = 200;
		CrystalesGreen=10;
		CrystalesRed=10;
		CrystalesBlue=10;
	}
}