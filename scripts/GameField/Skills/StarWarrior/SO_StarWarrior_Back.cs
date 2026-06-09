using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(menuName="ScriptableObject/Magic/StarWarrior/Back")]
public class SO_StarWarrior_Back : SO_VisibleSkill {	
	private BallController ball;
	public SkillNumberParameter time = new SkillNumberParameter(0,0.5f,1f);
	private float TimeCounter = 0f;
	protected override void SkillInitialization ()
	{
		base.SkillInitialization ();
		BattleEventsManager.Events.UpdateEvent.AddListener (() => Update ());
	}
	protected override void SpellCast ()
	{
		base.SpellCast ();
		var hero = (Player_BarController)(Saves.SaveSystem.GetCurrentHero ().GetActualBar());
		if (ball==null)
			ball = hero.GetRandomBall();
		TimeCounter = time;
	}
	void Update()
	{		
		TimeCounter += -TimeManager.deltaTime;
		if (TimeCounter>0)
		{
			var hero = (Player_BarController)(Saves.SaveSystem.GetCurrentHero ().GetActualBar());
			var pos = (Vector2)hero.transform.position;
			if (ball.rigi.isKinematic==false)
				ball.rigi.velocity = (pos-ball.rigi.position).normalized*ball.rigi.velocity.magnitude;
		}
	}
}
