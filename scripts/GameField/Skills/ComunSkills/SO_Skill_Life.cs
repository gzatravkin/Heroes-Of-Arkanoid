using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Skill_Life : SO_InvisibleSkill {
	public SkillNumberParameter AddicionalLife;
	protected override void SkillInitialization ()
	{
		base.SkillInitialization ();
		LifeManager.SetDefaultLifes ();
		LifeManager.AddMaxHP(AddicionalLife);
	}

}
