using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[AddComponentMenu("LevelEditor/Condiciones/DestroyAllWinCondicion")]
public class DestroyAllWinCondicion : AbstractMissionWinCondicion {			
	public static readonly string[] StandartDescripcion = {
		"Уничтожьте все",
		"Destroy all",
		"Destoy all" };	
	public bool AutoMakeDescripcion = true;
	void OnValidate()
	{
		//TODO FIX IT
		if (AutoMakeDescripcion) {
		//	Descripcion = new TextTranslation (StandartDescripcion);
		}
	}
	protected override void ControllerInitizliation()
	{
		base.ControllerInitizliation ();			
		BattleEventsManager.Events.BlockDestroyed.AddListener ((x)=>Check());				
	}
	void Check()
	{
		var LevelObjects = BattleController.GetGameField ().GetBlocks (true);
		LevelObjects.RemoveAll (x => !x.NeedToKill);
		if (LevelObjects.Count == 0)
			SetState (MissionCondicionState.Winned);
	}
}
