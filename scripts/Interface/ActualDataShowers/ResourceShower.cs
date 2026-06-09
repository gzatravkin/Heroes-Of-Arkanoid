using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ResourceShower : MonoBehaviour {
	public Text text;
	public void Start()
	{		
		SetText ();
		GlobalEvents.ResourceChanged.AddListener (() => SetText());
	}
	private void SetText()
	{
		if (text==null)
			text= GetComponent<Text> ();
		text.text = Saves.SaveSystem.GetPlayerData ().resources.ToString ();
	}
}
