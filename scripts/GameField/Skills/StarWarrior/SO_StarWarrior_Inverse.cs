using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="ScriptableObject/Magic/StarWarrior/Inverse")]
public class SO_StarWarrior_Inverse  : SO_VisibleSkill {			
		protected override void SpellCast ()
		{
		base.SpellCast ();
		var hero = (Player_BarController)(Saves.SaveSystem.GetCurrentHero ().GetActualBar());
		var ball = hero.GetRandomBall();
		ball.rigi.velocity = new Vector2 (ball.rigi.velocity.x, Mathf.Abs (ball.rigi.velocity.y));
		}		
	}