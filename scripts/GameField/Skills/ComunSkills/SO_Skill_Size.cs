using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Skill_Size : SO_InvisibleSkill {

protected override void SkillInitialization ()
	{
		base.SkillInitialization ();
		((Player_BarController)Saves.SaveSystem.GetCurrentHero ().GetActualBar ()).SetSize (GetCurrentLevel ());
		Debug.Log (GetCurrentLevel ());
	}
}
