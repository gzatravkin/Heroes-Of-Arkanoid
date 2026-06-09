using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UpgradePanel : MonoBehaviour {
	public GameObject panel;
	public GameObject UpgradeButton;
	public Button LearnButton;
	public SpellDescripcionPanel spellDescripcionPanel;
	private SO_AbstractSkill current;
	public void Initialization()
	{
		ShowPanel (Saves.SaveSystem.GetCurrentCharacterData ().claseIndex);
	}
	void ShowPanel(int Clase)
	{
		panel.KillAllChilds ();
		var character = Saves.SaveSystem.GetCurrentCharacterData ();
		character.ReloadSpellLevels ();
		var hero = SO_Configuraciones.obj.Heroes [Clase];
		var allSkills = hero.GetAllSkills ();
		SO_AbstractSkill lastOpennedSkill = null;
		foreach (SO_AbstractSkill abstrSkill in allSkills) {
			var t = (GameObject)Instantiate (UpgradeButton, panel.transform);
			t.SetZ (panel.transform.position.z);
			t.transform.localScale = Vector3.one;
			t.GetComponent<SelectSkillButton> ().Initialization (abstrSkill);
			int clase = Clase;
			var skillButton = t.GetComponent<Button> ();
			skillButton.onClick.AddListener(()=>SelectSkill(clase,abstrSkill));
			Debug.Log ("Agrege event");
			skillButton.interactable = abstrSkill.IsOpenned (character);
			if (abstrSkill.IsOpenned(character))
				lastOpennedSkill = abstrSkill;
		}
		if (current==null)
			SelectSkill (Clase, lastOpennedSkill);
	}
	void RefreshData()
	{
		ShowPanel (Saves.SaveSystem.GetCurrentCharacterData ().claseIndex);
		RefreshDescripcion(Saves.SaveSystem.GetCurrentCharacterData ().claseIndex, current);
	}
	void RefreshDescripcion(int clase, SO_AbstractSkill skill)
	{
		spellDescripcionPanel.SetSpell (skill, clase);
		LearnButton.interactable = CanLearnSkill (skill);
	}
	void SelectSkill(int clase, SO_AbstractSkill skill)
	{
		if (current == skill) {
			UpgradeCurrent ();
		} else {
			current = skill;
		}
		RefreshDescripcion (clase, skill);
	}
	public void ReturnAll()
	{
		Saves.SaveSystem.GetCurrentCharacterData ().ReturnUpgardes ();
		RefreshData ();
	}
	bool CanLearnSkill(SO_AbstractSkill skill)
	{
		if (current != null && current.GetCurrentLevel () < current.GetMaxLevel ()) {
			var Character = Saves.SaveSystem.GetCurrentCharacterData ();
			return CustomMarket.CanBuy (current.GetPointsToUpgrade (current.GetCurrentLevel () + 1), Character.LibrePoints);				
		}
		return false;
	}
	public void UpgradeCurrent()
	{
		if (CanLearnSkill (current)) {
			var Character = Saves.SaveSystem.GetCurrentCharacterData ();
			var hero = SO_Configuraciones.obj.Heroes [Character.claseIndex];
			if (CustomMarket.TryToBuy (current.GetPointsToUpgrade (current.GetCurrentLevel () + 1), ref Character.LibrePoints)) {
				Character.spellData.UpLevel (hero.GetSkillIndex(current));
			}
		}
	}
	void Start()
	{
		Initialization ();
		GlobalEvents.SkillsChanged.AddListener (() => RefreshData ());
	}

}
