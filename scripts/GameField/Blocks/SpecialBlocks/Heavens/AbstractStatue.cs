using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AbstractStatue : AbstractBallCollied {
	[HideInInspector]
	public static List<AbstractStatue> statues;
	private float TimeToBeAlly=0;
	private float TimeToReturnLevel=0;
	public TimeType timeType = TimeType.ScaledTime;
	public GameObject[] StatueUpgradeEffects;
	public GameObject StatueAllyEffect;
	protected int Level=0;
	public static void AddAllyTimeToAll(float time)
	{
		statues.RemoveAll (x => x == null);
		var enabledStatues = statues.FindAll (x => x.gameObject.activeInHierarchy);
		foreach (var o in enabledStatues)
			o.AddTimeToBeAlly (time);
	}
	public static void LevelUpToAll(float time)
	{
		statues.RemoveAll (x => x == null);
		var enabledStatues = statues.FindAll (x => x.gameObject.activeInHierarchy);
		foreach (var o in enabledStatues)
			o.LevelUp (time);
	}
	public bool IsAlly()
	{
		return TimeToBeAlly > 0;
	}
	protected virtual void Start () {
		if (statues == null)
			statues = new List<AbstractStatue> ();
		statues.Add (this);
		statues.RemoveAll (x => x == null);
		RefreshGrafic ();
	}
	public void AddTimeToBeAlly(float time)
	{
		var ally = IsAlly ();
			TimeToBeAlly += time;		
		if (IsAlly() != ally) {
			StateChanged (Level, ally);
			RefreshGrafic ();
		}
	}
	public virtual void RefreshGrafic()
	{
		for (int i = 0; i < StatueUpgradeEffects.Length; i++) {
			if (StatueUpgradeEffects[i]!=null)
				StatueUpgradeEffects [i].SetActive (i <= Level);
		}
		if (StatueAllyEffect != null)
			StatueAllyEffect.SetActive (IsAlly ());
	}
	public virtual void LevelUp(float time)
	{
		TimeToReturnLevel += time;
		Level += 1;
		RefreshGrafic ();
		StateChanged ( Level, IsAlly ());
	}
	public virtual void ItsAlly(float time)
	{
		TimeToBeAlly += time;
	}
	public virtual void Update()
	{
		if (TimeToReturnLevel > 0)
			TimeToReturnLevel += -TimeManager.GetDeltaTime (timeType);
		if (TimeToBeAlly > 0)
            TimeToBeAlly += -TimeManager.GetDeltaTime (timeType);
		bool flag = false;
		if (TimeToBeAlly<=0)
		{
			TimeToBeAlly = 0;
			flag = true;
		}
		if (TimeToReturnLevel<=0)
		{
			TimeToReturnLevel = 0;
			Level = 0;
			flag = true;
		}
		if (flag)
			StateChanged (Level, IsAlly ());	
	}
	protected virtual void StateChanged(int level, bool ally)
	{
	}
}
