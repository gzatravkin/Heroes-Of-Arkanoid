using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BattleEventsManager : MonoBehaviour {
	private static BattleEvents events;
	public static UnityEvent OnEventsReload = new UnityEvent ();
	public static BattleEvents Events{
		get{			
			if (events == null)
				events = new BattleEvents ();
			return events;
		}
	}
	public static void ReloadAllEvents()
	{
		events = null;
		events = new BattleEvents();
		OnEventsReload.Invoke ();

	}
	void Update () {
		events.UpdateEvent.Invoke ();
	}
	void Start()
	{
		ReloadAllEvents ();
	}
	void OnDestroy()
	{
		events=null;
	}
}

