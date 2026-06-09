using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Engineer_RadiationSpell : SO_VisibleSkill {
	public GameObject RadiationObj;
	[HideInInspector]
	public SkillNumberParameter maxHits = new SkillNumberParameter(5,7,9);
	[HideInInspector]
	public SkillNumberParameter size = new SkillNumberParameter(0.8f,1f,1.2f);
protected override void SpellCast ()
	{
		base.SpellCast ();
		var hero = (Player_BarController)(Saves.SaveSystem.GetCurrentHero ().GetActualBar());
		var ball = hero.GetRandomBall();
		var t = Instantiate (RadiationObj, ball.transform.position, Quaternion.identity);
		t.GetComponent<BlockHitter> ().MaxHits = maxHits;

	}
}
