using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WinReaction : MonoBehaviour {
	public Text Exp,Lvl, TrasurePoints;
	public static void GetPremy(Premy premy,float expCoef,float TreasurePointsCoef)
	{
		var character = Saves.SaveSystem.GetCurrentCharacterData ();
		character.AddExp(Mathf.RoundToInt(premy.exp * expCoef));
		Saves.SaveSystem.GetPlayerData ().AddTreasurePoints (Mathf.RoundToInt(premy.TreasurePoints*TreasurePointsCoef));
		character.SaveMissionAsWinned (LevelLoader.LevelNumber);
		LevelLoader.Continue (premy);
	}
}
