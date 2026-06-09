using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
[AddComponentMenu("LevelEditor/Condiciones/TimerWinCondicion")]
public class TimerWinCondicion : AbstractMissionWinCondicion {
	public static readonly string[] SurviveDescripcion = {
		"Переживите {0} секунд",
		"Survive {0} seconds",
		"Survive {0} secundos" };
	public static readonly string[] LimiteDescripcion = {
		"Уложитель в  {0} секунд",
		"You have {0} seconds",
		"Tenes {0} secundos" };
	public TimeType timeType = TimeType.BattleTime;
	public MissionCondicionState StateAfterTimer = MissionCondicionState.Winned;
	public float TimeLimite=20f;
	private Timer Timer;
	private float TimeCounter = 0f;
	public bool AutoMakeDescripcion = true;
	void OnValidate()
	{
		if (AutoMakeDescripcion) {
			//TODO make it work
			//if (StateAfterTimer==MissionCondicionState.Winned)
				//Descripcion = new TextTranslation (SurviveDescripcion);
			//if (StateAfterTimer==MissionCondicionState.Loosed)
				//Descripcion = new TextTranslation (LimiteDescripcion);
		}
	}
	public override string GetDescripcion ()
	{
		return Descripcion.SetParms(TimeLimite);
	}
	public void ShowTimer()
	{
		if (Timer!=null)
		Timer.SetTime (TimeLimite - TimeCounter);		
	}
	protected override void ControllerInitizliation()
	{
		base.ControllerInitizliation ();
		TimeCounter = 0f;
		Timer = TimeManager.CreateTimer (TimeLimite);
	}
	void Update()
{	
		TimeCounter += TimeManager.GetDeltaTime(timeType);
		ShowTimer ();
		if (TimeCounter > TimeLimite) {
			SetState (StateAfterTimer);
		}
	}
}
