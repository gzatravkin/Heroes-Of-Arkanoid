using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
[AddComponentMenu("LevelEditor/MissionWinController")]
public class MissionWinController : MonoBehaviour {
	[System.Serializable]
	public enum CheckerState
	{
		RequiereWinAll, RequiereWinOne, RequiereWinOneWithToleranceToLoose
	}
	public enum MissionState
	{
		InProgress,Loosed,Winned
	}
	public CheckerState checkerState;
	[HideInInspector]
	public MissionState missionState;
	public List<AbstractMissionWinCondicion> WinCondicions;
	public static float ExpCoef=1f;
	public static float TreasurePointsCoef=1f;
	public static MissionWinController objRef;
	void Awake()
	{
		ExpCoef = 1f;
		TreasurePointsCoef = 1f;
		objRef = this;
		ServiceLocator.winController = this;
	}
	void Start()
	{
		BattleEventsManager.Events.BattleStarted.Invoke ();
	}
	void OnValidate()
	{
		WinCondicions.Clear ();
		WinCondicions.AddRange(GetComponents<AbstractMissionWinCondicion> ());
	}
	public void CondicionStateChanged(AbstractMissionWinCondicion winCondicion, MissionCondicionState state)
	{
		Check ();
	}
	public void Check()
	{
		if (missionState!=MissionState.InProgress)
			return;
		for (int i = 0; i < WinCondicions.Count; i++) {
			if (WinCondicions [i].GetState () == MissionCondicionState.Canceled) {
				Destroy (WinCondicions [i]);			
				WinCondicions.RemoveAt (i);
			}
		}
		switch (checkerState) {
		case CheckerState.RequiereWinAll:
			{
				missionState = WinAllCheck ();
				break;
			}
		case CheckerState.RequiereWinOne:
			{
				missionState = WinOneCheck ();
				break;
			}
		case CheckerState.RequiereWinOneWithToleranceToLoose:
			{
				missionState = WinOneCheckWithTolerance ();
				break;
			}
		}
		if (missionState == MissionState.Winned) {
			
			Win ();
		}
		if (missionState == MissionState.Loosed) {
			Loose ();
		}
	}
	private MissionState WinAllCheck()
	{
		var missionState = MissionState.InProgress;
		if  (WinCondicions.TrueForAll (x=>x.GetState()==MissionCondicionState.Winned))
			missionState = MissionState.Winned;
		if  (WinCondicions.Find (x=>x.GetState()==MissionCondicionState.Loosed)!=null)
			missionState = MissionState.Loosed;
		return missionState;
	}
	private MissionState WinOneCheck()
	{
		var missionState = MissionState.InProgress;
		if  (WinCondicions.Find (x=>x.GetState()==MissionCondicionState.Winned)!=null)
			missionState = MissionState.Winned;
		if  (WinCondicions.Find (x=>x.GetState()==MissionCondicionState.Loosed)!=null)
			missionState = MissionState.Loosed;
		return missionState;
	}
	private MissionState WinOneCheckWithTolerance()
	{
		var missionState = MissionState.InProgress;
		if  (WinCondicions.TrueForAll (x=>x.GetState()==MissionCondicionState.Winned))
			missionState = MissionState.Winned;
		if  (WinCondicions.TrueForAll (x=>x.GetState()==MissionCondicionState.Loosed))
			missionState = MissionState.Loosed;
		return missionState;
	}
	public void Win()
	{
		WinReaction.GetPremy (GameField.objRef.LevelObject.premy,ExpCoef,TreasurePointsCoef);
		BattleEventsManager.Events.BattleWinned.Invoke ();
	}
	public void Loose()
	{
		BattleEventsManager.Events.BattleLoosed.Invoke ();
		Restart ();
	}
	void Restart()
	{
		SceneManager.LoadScene (SceneManager.GetActiveScene ().name);
	}
	public void Initialization()
	{
		missionState = MissionState.InProgress;
		foreach (AbstractMissionWinCondicion winCondicion in WinCondicions) {
			winCondicion.Initialization (this);
		}
	}
	public string GetDescripcion()
	{		
		if (WinCondicions.Count > 0) {			
			var descripcion = WinCondicions[0].GetDescripcion();
			if (WinCondicions.Count>1)
			for (int i = 1; i < WinCondicions.Count; i++) {					
				descripcion = descripcion + "\n" + WinCondicions [i].GetDescripcion ();
			}
			return descripcion;
		} else
			return "";
	}
}
