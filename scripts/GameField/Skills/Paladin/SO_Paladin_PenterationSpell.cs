using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Paladin_PenterationSpell : SO_VisibleSkill {
	public SkillNumberParameter maxBlocks;
	public GameObject HitterObj;

	protected override void SpellCast ()
	{
		base.SpellCast ();
		var hero = (Player_BarController)(Saves.SaveSystem.GetCurrentHero ().GetActualBar());
		var ball = hero.GetRandomBall();
		var effect = Instantiate (HitterObj, ball.transform.position, Quaternion.identity) as GameObject;
		effect.SetParentWithScaleOne (ball.transform);
		effect.GetComponent<BlockHitter> ().MaxHits = maxBlocks;
	}
}
