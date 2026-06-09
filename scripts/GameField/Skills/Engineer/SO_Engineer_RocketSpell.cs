using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Engineer_RocketSpell : SO_VisibleSkill {
	public GameObject Rocket;
	public SkillNumberParameter maxHits = new SkillNumberParameter (8, 10, 12);
	public SkillNumberParameter size = new SkillNumberParameter (0.9f, 1.3f, 1.6f);
	public SkillNumberParameter rocketAcceleration = new SkillNumberParameter (5, 8, 12);
	protected override void SpellCast ()
	{
		base.SpellCast ();
		var hero = (Player_BarController)(Saves.SaveSystem.GetCurrentHero ().GetActualBar());
		hero.barGraficController.SetTrigger ("RocketCasted");
		var t = Instantiate (Rocket, hero.transform.position, Quaternion.identity);
		t.transform.localScale = Vector3.one * size;
		t.GetComponent<Engineer_RocketController> ().MaxHits = maxHits;
		t.GetComponent<Engineer_RocketController> ().Acceleration = rocketAcceleration;
	}
}
