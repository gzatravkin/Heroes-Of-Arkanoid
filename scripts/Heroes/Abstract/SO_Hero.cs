using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="ScriptableObject/Hero")]
public class SO_Hero : ScriptableObject {
	public Sprite SPRITE_INTERFACE_ICO;
	public Sprite SPRITE_INTERFACE_CHOISE;
	public TextTranslation Name,Descripcion;
	public HeroRoad road;
	public SO_InvisibleSkill[] Skills;
	public SO_VisibleSkill[] Spells;
	public SO_SpellController SpellSystem;
	public AbstractPlayerController PlayerObj;
	public int StartLifes;
	public int StartBalls;

	private AbstractPlayerController ActualPlayerObj;
	public AbstractPlayerController GetActualBar()
	{
		return ActualPlayerObj;
	}
	public virtual void GameInitialization(Saves.SpellData.Skill[] spellData)
	{		
		SetSpellLevelsData (spellData);
		var recourcePanel = RecourcePanel.objRef;
		recourcePanel.GameInitialization(SpellSystem);
		SpellSystem.GameInitialization (this,recourcePanel);
		var spellPanel = SpellPanel_List.objRef;
		spellPanel.GameInitialization (SpellSystem);
		var lifePanel = LifeManager.objRef;
		lifePanel.Initialization (StartLifes,StartBalls);
		ActualPlayerObj = (Instantiate (PlayerObj.gameObject, SO_Configuraciones.obj.StartBarPosition, Quaternion.identity) as GameObject).GetComponent<AbstractPlayerController>();
		ActualPlayerObj.Initialization ();
		var skills = GetAllSkills ();
		foreach (SO_AbstractSkill s in skills)
			if (s.GetCurrentLevel()>0)
				s.GameIniciacion ();		
	}
	public List<SO_AbstractSkill> GetAllSkills()
	{
		var t = new List<SO_AbstractSkill> ();
		t.AddRange (Skills);
		t.AddRange (Spells);
		return t;
	}
	public int GetSkillIndex(SO_AbstractSkill skill)
	{
		return GetAllSkills ().FindIndex (x=>x==skill);
	}
	public void SetSpellLevelsData(Saves.SpellData.Skill[] Levels)
	{
		var skills = GetAllSkills ();
		for (int i = 0; i < Levels.Length && i < skills.Count; i++) {
			skills [i].SetLevel(Levels [i].Level);
		}			
	}
	public bool IsOpenned()
	{
		return true;
	}
}
