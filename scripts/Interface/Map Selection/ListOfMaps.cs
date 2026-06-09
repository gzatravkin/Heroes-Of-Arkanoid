using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ListOfMaps : MonoBehaviour {
	private int LevelCodeChoised;
	public float SizeCoef = 10f;
	private bool Initializated=false;
	public GameObject mapButton;
	public Text MissionDescripcion;
	public Image MissionIco;
	public GameObject selectElement;
	public struct ButtonData
	{
		public GameObject button;
		public RoadElement levelAsociated;
	}
	public List<ButtonData> buttonsData = new List<ButtonData> ();
	#if UNITY_EDITOR
	void OnDrawGizmos()
	{				
		var style = new GUIStyle();
		style.fontSize = 40;
		foreach (var b in buttonsData)
		{
			UnityEditor.Handles.Label (b.button.transform.position, b.levelAsociated.UniqueCode.ToString(),style);
			var Entry = b.levelAsociated.GetEntry ();
			var associatedButtons = buttonsData.FindAll (x => Entry.Contains(x.levelAsociated));
			foreach (var a in associatedButtons) {
				if (b.levelAsociated.GetObjectReference (a.levelAsociated).Rigid)
					Gizmos.color = Color.red;
				else
					Gizmos.color = Color.green;
				var delta = a.button.transform.position - b.button.transform.position;
				delta = delta.normalized * 10f;
				Gizmos.DrawLine (a.button.transform.position, b.button.transform.position+delta);
				Gizmos.DrawWireSphere (b.button.transform.position+delta,1.5f);
			}
		}
	}
	#endif
	// Use this for initialization
	void Start () {
		if (!Initializated) {
			ShowList (Saves.SaveSystem.GetCurrentHero().road);
		}
		GlobalEvents.ClassChanged.AddListener (() => ShowList (Saves.SaveSystem.GetCurrentHero ().road));
	}
	public void ShowList(HeroRoad road)
	{
		GameObject target = this.gameObject;
		target.KillAllChilds ();
		buttonsData.Clear ();
		var levels = road.levels;
		for (int i = 0; i<levels.Count;i++) {
			var mapElement = Instantiate (mapButton);
			mapElement.SetParentWithScaleOne (target.transform, levels [i].ElementPos * SizeCoef);
			mapElement.GetComponent<MapButton> ().SetMap (levels [i].levelObj);
			var buttonData = new ButtonData ();
			buttonData.button = mapElement;
			buttonData.levelAsociated = levels[i];
			buttonsData.Add (buttonData);

			bool IsOpenned = levels[i].IsOpen(Saves.SaveSystem.GetCurrentCharacterData ());
			var button = mapElement.AddComponent<Button> ();
			var colors = button.colors;
			colors.disabledColor = new Color (0.2f, 0.2f, 0.2f, 1);
			button.colors = colors;
			int location =  levels[i].levelObj.Location;
			int buttonNumber = i;
			button.onClick.AddListener (() => {
				Select(buttonNumber);
				FonManager.SetFon(SO_Configuraciones.obj.Locationes[location].SPRITE_FON);
			});
			button.interactable = IsOpenned;
			if (IsOpenned)
				button.onClick.Invoke ();
		}
		Initializated = true;
	}
	public void Select(int buttonNumber)
	{
		var clase = Saves.SaveSystem.GetCurrentCharacterData ().claseIndex;
		var level = buttonsData [buttonNumber].levelAsociated;
		MissionDescripcion.text = level.levelObj.GetDescripcion ();			
		MissionIco.sprite = level.levelObj.Ico;
		LevelCodeChoised = level.UniqueCode;
		selectElement.SetParentWithScaleOne (buttonsData [buttonNumber].button.transform);
	}

	public void OpenMap()
	{		
		LevelLoader.LoadLevel (Saves.SaveSystem.GetCurrentHero().road,LevelCodeChoised);
	}

}
