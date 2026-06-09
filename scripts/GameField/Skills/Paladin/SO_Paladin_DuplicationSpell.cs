using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Paladin_DuplicationSpell : SO_VisibleSkill {
	public SkillNumberParameter addicionalBallsSize = new SkillNumberParameter(0.2f,0.5f,0.7f);
	public SkillNumberParameter ballsNumber = new SkillNumberParameter (1, 3, 4,true);
	protected override void SpellCast ()
	{
		base.SpellCast ();
		var hero = (Player_BarController)(Saves.SaveSystem.GetCurrentHero ().GetActualBar());
		var ball = hero.GetRandomBall();
		for (int i = 0; i <	ballsNumber.GetInt (); i++) {
			var t = Instantiate (ball.gameObject) as GameObject;
			t.transform.localScale = Vector3.one * addicionalBallsSize;
			t.transform.position =t.transform.position+(Vector3) Random.insideUnitCircle * 1f;
			hero. barLogic.AddBall(t.GetComponent<BallController> ());
		}

	}
}
