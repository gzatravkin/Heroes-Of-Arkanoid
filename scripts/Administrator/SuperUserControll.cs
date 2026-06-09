using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SuperUserControll : MonoBehaviour {
	public GameObject levelObject;
	public Text lvlTarget;
	public void Start()
	{
		OpenAllSpells ();
		LevelChange (10);
	}
	void OpenAllSpells()
	{
		var hero = Saves.SaveSystem.GetCurrentHero ();
		for (int i = 0;i<hero.Spells.Length;i++)
			ServiceLocator.spellPanel.ForceOpenSpell (i);
	}
	public void CreateBlocks()
	{
		Instantiate (levelObject, Vector3.zero, Quaternion.identity);
	}
	public void AddRandomItem()
	{
		ItemsManager.GetRandomItem ();
	}
	public void LevelChange(float level)
	{		
		//BattleEventsManager.ReloadAllEvents ();
		ServiceLocator.spellController.AddFreeCasts (10000);
		var skills = Saves.SaveSystem.GetCurrentHero ().GetAllSkills();
		for (int i = 0; i < skills.Count; i++)
			skills [i].SetLevel (Mathf.RoundToInt (level));
		lvlTarget.text = level.ToString ();
		for (int i = 0; i < skills.Count; i++) {
			skills [i].GameIniciacion ();
		}
	}
}
