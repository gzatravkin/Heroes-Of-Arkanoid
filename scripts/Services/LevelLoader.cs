using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public static class LevelLoader
{
	private static GameField gameField;
	public const string LevelName = "BattleField";
	public static Level LevelToLoad;
	public static int LevelNumber;
	private static HeroRoad roadActual;

	public static GameField GetGameField ()
	{
		return gameField;
	}

	public static void Initialization ()
	{
		var t = new GameField ();
		if (LevelToLoad == null) {
			int clase = Saves.SaveSystem.GetCurrentCharacterData ().claseIndex;
			FonManager.SetFon (SO_Configuraciones.obj.Locationes [0].SPRITE_FON);
			LevelToLoad = SO_Configuraciones.obj.Heroes [clase].road.levels [0].levelObj;
		}
		FonManager.SetFon (SO_Configuraciones.obj.Locationes [LevelToLoad.Location].SPRITE_FON);
		LevelToLoad.Initialization (t);
		gameField = t;
	}
	public static void ChoiseDestination(List<RoadElement> choiseList, string ChoiseName,string ChoiseDescripcion="")
	{
		var buttonData = new List<PalletButtonData> ();
		foreach (var o in choiseList) {
			buttonData.Add (new PalletButtonData (o.levelObj.name, () => {
				SceneManager.LoadScene (LevelName);
				LoadLevel (roadActual, o.UniqueCode);
			}));
		}
		PalletsCreator.CreateCustomPallet (ChoiseName,ChoiseDescripcion,null,PalletStyles.Standart, buttonData.ToArray ());		
	}
	public static void Continue (Premy premyToShow)
	{								
		var character = Saves.SaveSystem.GetCurrentCharacterData ();
		var levelsToConinue = roadActual.GetRelativeLevels (LevelNumber).FindAll(x=>x.IsOpen(character));
		if (levelsToConinue.Count == 1) {			
			LoadLevel (roadActual, levelsToConinue [0].UniqueCode,false,premyToShow);
		} else if (levelsToConinue.Count > 1) {			
			ChoiseDestination (levelsToConinue, "Есть развилка!");		
		}
		else if (levelsToConinue.Count == 0) {		
			var openLevels = roadActual.GetOpennedAndUnwinnedLevels (character);
			if (openLevels.Count > 0) {
				ChoiseDestination (openLevels, "Вы закончили развилку!", "Вы можете вернуться и пойти по другому пути: ");
			}
			else {
				PalletsCreator.CreateCustomPallet ("Больше нет ни одного уровня",
					"Попробуйте сыграть другим классом(вас ждет новая кампания) или пройти старые уровни заново.",
					null,
					PalletStyles.Standart, 
					new PalletButtonData ("Главное меню", () => MySceneManager.LoadSceneAnimated ("MainMenu", -1, 0)));
			}
		}
	}

	public static void LoadLevel (HeroRoad road, int levelCode, bool animated = true, Premy premyToShow=null)
	{						
		roadActual = road;
		LevelNumber = levelCode;
		LevelToLoad = road.GetRoadElementByKey (levelCode).levelObj;
		if (animated)
			MySceneManager.LoadSceneAnimated (LevelName, 1, 1, new UnityAction (() => PalletsCreator.CreatePallet_MissionOpen (LevelToLoad,premyToShow)));
		else {
			MySceneManager.LoadScene_WithCallback (LevelName, new UnityAction (() => PalletsCreator.CreatePallet_MissionOpen (LevelToLoad,premyToShow)));
		}
	}

	public static void LoadLevel (Level level, bool animated = true, Premy premyToShow=null)
	{				
		LevelNumber = 0;
		LevelToLoad = level;
		if (animated)
			MySceneManager.LoadSceneAnimated (LevelName, 1, 1, new UnityAction (() => PalletsCreator.CreatePallet_MissionOpen (level,premyToShow)));
		else {			
			MySceneManager.LoadScene_WithCallback (LevelName, new UnityAction (() => PalletsCreator.CreatePallet_MissionOpen (level,premyToShow)));
		}
	}
}
