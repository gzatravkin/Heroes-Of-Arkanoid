using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TimeManager : MonoBehaviour {

	public static float battleDeltaTime;
	public static float deltaTime;
	public static float unscaledDeltaTime;
	public static float battleTime;
	public static TimeManager objRef;
	public static readonly float PhysicDeltaTime = 0.02f;
	public static Timer CreateTimer(float currentValue)
	{
		var t = (GameObject)Instantiate (SO_Configuraciones.obj.timer.gameObject);
		var timer = t.GetComponent<Timer> ();
		timer.SetTime (currentValue);
		return timer;
	}
	public static void ActionWithDelay(float delay, UnityEngine.Events.UnityAction action, TimeType delayType = TimeType.RealTime)
	{				
		if (objRef == null)
			Initialization ();
		objRef.StartCoroutine(DelayedAction(delay,action,delayType));
	}
	public static IEnumerator DelayedAction(float delay, UnityEngine.Events.UnityAction action, TimeType delayType)
	{	
		
		switch (delayType)
		{
		case (TimeType.BattleTime):
			{
				float battleTimeWanted = battleTime + delay;
				while (battleTime<battleTimeWanted)
					yield return new WaitForEndOfFrame ();
				break;
			}
		case (TimeType.RealTime):
			{
				yield return new WaitForSecondsRealtime (delay);
				break;
			}
		case (TimeType.ScaledTime):
			{
				yield return new WaitForSeconds (delay);
				break;
			}		
		}
		action.Invoke ();
	}
	public static void GameInitialization()
	{
		objRef.StopAllCoroutines ();
	}
	[RuntimeInitializeOnLoadMethod]
	private static void Initialization()
	{		
		if (GameObject.FindObjectOfType<TimeManager> () == null||objRef==null) {
			GameObject timeManager = new GameObject ("TimeManager");
			objRef = timeManager.AddComponent<TimeManager> ();
			timeManager.SetParentByName ("MultyScenesManagers");
			DontDestroyOnLoad (GameObject.Find ("MultyScenesManagers"));
		}
		battleTime = 0f;
	}
	public static bool IsInBattle()
	{
		if (BattleController.GetPlayer() == null)
			return false;
		else
			return BattleController.GetPlayer ().IsInGame ();		
	}
	public void Update()
	{
		deltaTime = Time.deltaTime;
		unscaledDeltaTime = Time.unscaledDeltaTime;
		if (IsInBattle())
			battleDeltaTime = deltaTime;
		else
			battleDeltaTime = 0f;
		battleTime += battleDeltaTime;
		Time.fixedDeltaTime = Time.timeScale * PhysicDeltaTime;
	}
	public static float GetDeltaTime(TimeType timeType)
	{
		float time = 0f;
		switch (timeType)
		{
		case TimeType.BattleTime:{
				time = battleDeltaTime;
				break;
			}
		case TimeType.RealTime:{
				time = unscaledDeltaTime;
				break;
			}
		case TimeType.ScaledTime:{
				time = deltaTime;
				break;
			}
		}
		return time;
	}



	public static float GetTime(TimeType timeType)
	{
		float time = 0f;
		switch (timeType)
		{
		case TimeType.BattleTime:{
				time = battleTime;
				break;
			}
		case TimeType.RealTime:{
				time = Time.unscaledTime;
				break;
			}
		case TimeType.ScaledTime:{
				time = Time.time;
				break;
			}
		}
		return time;
	}
}
