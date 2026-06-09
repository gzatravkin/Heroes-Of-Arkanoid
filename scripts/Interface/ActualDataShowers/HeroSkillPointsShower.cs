using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HeroSkillPointsShower : MonoBehaviour {
	public Text text;
	public void Start()
	{		
		SetText ();
	}
	private void SetText()
	{
		if (text==null)
			text= GetComponent<Text> ();
		text.text = Saves.SaveSystem.GetCurrentCharacterData ().LibrePoints.ToString ();;
	}
	void Update()
	{
		SetText ();
	}
}
