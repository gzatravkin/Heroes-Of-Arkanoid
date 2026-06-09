using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameIniciacion : MonoBehaviour {
	public static GameIniciacion obj;
	public static void ReIniciacion()
	{
		obj.Iniciacion ();
	}
	public void Iniciacion()
	{				
		LevelLoader.Initialization ();
		Saves.SaveSystem.GetCurrentCharacterData ().ReloadSpellLevels ();
		var gameField = LevelLoader.GetGameField ();
		var hero = ServiceLocator.GetConfiguraciones ().Heroes [Saves.SaveSystem.GetCurrentCharacterData().claseIndex];
		hero.GameInitialization (Saves.SaveSystem.GetCurrentCharacterData().spellData.GetRawData().ToArray());
		BattleController.Initialization (gameField,hero);
		Camera.main.GetComponent<BattleFieldCameraController> ().Initialization (); //Initizization from first update, with GameWallsManager
		GameWallsManager.Initialization ();
		TimeManager.GameInitialization ();
		foreach (var item in SO_Configuraciones.obj.Items)
			item.GameInitialization ();
	}
	void Start()
	{
		Iniciacion ();
		obj = this;
	}
}
