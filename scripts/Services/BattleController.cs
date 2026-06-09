using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class BattleController {
	private static GameField GameField;
	private static AbstractPlayerController Player;
	private static SO_Hero Hero;
	public static void Initialization(GameField gameField, SO_Hero hero)
	{
		GameField = gameField;
		Player = hero.GetActualBar();
		Hero = hero;
	}
	public static void Win()
	{
		MissionWinController.objRef.Win ();
	}
	public static void Loose()
	{
		MissionWinController.objRef.Loose ();
	}
	public static GameField GetGameField()
	{
		return GameField;
	}
	//Desarollar
	public static SO_Hero GetCurrentHero()
	{
		return Hero;
	}
	public static AbstractPlayerController GetCurrentPlayerObject()
	{
		return Hero.GetActualBar();
	}
	//Desarollar
	public static AbstractPlayerController GetPlayer()
	{
		return Player;
	}
	public static void DestroyBlockWithCallback(BlockScript blockToDestroy)
	{
		if (blockToDestroy != null) {			
			MonoBehaviour.Destroy (blockToDestroy.gameObject);
		}
		BattleEventsManager.Events.BlockDestroyed.Invoke (blockToDestroy);
	}
}
