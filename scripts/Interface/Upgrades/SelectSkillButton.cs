using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SelectSkillButton : MonoBehaviour {
	public Text levelTarget;
	public Image icoTarget;
	public GameObject LevelPanel, Border, Lock;
	public Color DisabledColor, NormalColor, MaxColor, ZeroColor;
	private SO_AbstractSkill skill;
	public void Initialization(SO_AbstractSkill skill)
	{
		this.skill = skill;
		icoTarget.sprite = skill.ICO_CUADRADO;
		Refresh ();
	}
	void Refresh()
	{
		
		var level = skill.GetCurrentLevel ();
		bool aviable = skill.IsOpenned (Saves.SaveSystem.GetCurrentCharacterData ());
		Lock.SetActive (!aviable);
		Border.SetActive (aviable && level > 0);
		LevelPanel.SetActive (aviable && level > 0);
		if (aviable) {
			if (level > 0 && level < skill.GetMaxLevel ())
				icoTarget.color = NormalColor;
			else {
				if (level == 0) {
					icoTarget.color = ZeroColor;
				}
				else
				{
					icoTarget.color = MaxColor;
				}
			}
		} else {
			icoTarget.color = DisabledColor;
		}
		levelTarget.text =  level.ToString();

	}
	void Update()
	{
		Refresh ();
	}
}
