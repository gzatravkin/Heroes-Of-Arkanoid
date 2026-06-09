using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoSaver : MonoBehaviour {

	[RuntimeInitializeOnLoadMethod]
	public static void Initialization()
	{		
		if (GameObject.FindObjectOfType<AutoSaver> () == null) {
			GameObject autoSaver = new GameObject ("AutoSaver");
			autoSaver.SetParentByName ("MultyScenesManagers");
			autoSaver.AddComponent<AutoSaver> ();
			DontDestroyOnLoad (GameObject.Find ("MultyScenesManagers"));
		}
	}
	void OnApplicationQuit()
	{
		Saves.SaveSystem.Save ();
	}

	void OnApplicationPause(bool pauseStatus)
	{
		Saves.SaveSystem.Save ();
	}
}
