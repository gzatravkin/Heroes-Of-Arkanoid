using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class SpellDescripcionPanel : MonoBehaviour {
	public Image SkillIco;
	public Text SkillDescripcion,SkillName,SkillLevelValue,SkillPrice;
	public GameObject UpgradedPanel;
	public Button UpgradeCurrentButton;
	public void SetSpell(SO_AbstractSkill skill, int claseIndex)
	{				
		gameObject.SetActive (true);
		SkillIco.sprite = skill.Get_ICO_UPGRADE (skill.GetCurrentLevel());
		SkillName.text = skill.Name.GetStringActual ();
		var Character = Saves.SaveSystem.GetCurrentCharacterData ();
		UpgradeCurrentButton.interactable = CustomMarket.CanBuy(skill.GetPointsToUpgrade (skill.GetCurrentLevel() + 1), Character.LibrePoints);
		bool Upgarded = (skill.GetCurrentLevel () > 0);
		UpgradedPanel.SetActive (Upgarded);
			
		if (skill.GetMaxLevel () == skill.GetCurrentLevel()) {
			SkillLevelValue.text = "max";
			UpgradeCurrentButton.interactable = false;
		}
		else			
			SkillLevelValue.text = skill.GetCurrentLevel().ToString();		
		if (skill.GetCurrentLevel () < skill.GetMaxLevel ()) {
			SkillPrice.gameObject.SetActive (true);
			SkillPrice.text = skill.GetPointsToUpgrade (skill.GetCurrentLevel () + 1).ToString ();
		} else {			
			SkillPrice.gameObject.SetActive (false);
		}
		SkillDescripcion.text = "\t\t"+skill.GetDescripcion ();
	}

}
