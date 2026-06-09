using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PalletButtonData{	
	public string Name;
	public UnityAction action;
	public Sprite Ico;
	public System.Predicate<Object> OpenCondition;
	public Object openTarget;

	public bool IsOpenned()
	{
		if (OpenCondition == null || openTarget == null)
			return true;
		else
			return OpenCondition.Invoke (openTarget);
	}

	public PalletButtonData()
	{
	}
	public PalletButtonData(string name, UnityAction action)
	{
		this.Name = name;
		this.action = action;
	}
	public PalletButtonData(string name, Sprite Ico, UnityAction action)
	{
		this.Name = name;
		this.Ico = Ico;
		this.action = action;
	}
	public PalletButtonData(string name)
	{
		this.Name = name;
	}

}
