using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseManager : MonoBehaviour {
	public static PauseManager objRef;
	public List<PauseReason> PauseReasons=new List<PauseReason>();
	public static float UnPauseVelocity=1.5f;
	void Update()
	{		
		if (PauseReasons == null)
			PauseReasons = new List<PauseReason> ();
		PauseReasons.RemoveAll (x => (x == null)||(!x.isActiveAndEnabled)||(!x.gameObject.activeSelf));
		float timeScaleToGo = 1;
		if (PauseReasons.Count > 0)
			timeScaleToGo  = 0;
		if (Time.timeScale < timeScaleToGo)
			Time.timeScale = Mathf.MoveTowards (Time.timeScale, timeScaleToGo, UnPauseVelocity * TimeManager.unscaledDeltaTime);
		else
			Time.timeScale = timeScaleToGo;
	}
	public bool IsPaused()
	{
		return (PauseReasons.Count > 0);
	}
	[RuntimeInitializeOnLoadMethod]
	private static void Initialization()
	{				
		if (GameObject.FindObjectOfType<PauseManager> () == null||objRef==null) {
			GameObject pauseManager = new GameObject ("PauseManager");
			objRef = pauseManager.AddComponent<PauseManager> ();
			pauseManager.SetParentByName ("MultyScenesManagers");
			DontDestroyOnLoad (GameObject.Find ("MultyScenesManagers"));
		}
	}
}
