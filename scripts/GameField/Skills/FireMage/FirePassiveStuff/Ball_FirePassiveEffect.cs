using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball_FirePassiveEffect : MonoBehaviour {
	public GameObject Explotion;
	public float area;
	public int maxHits,damage;
	public float Velocity = 5;
	private BonusVelocity actualBonus;
	public void Initialization(BallController ball, float area, int maxHits,int damage, float velocity)
	{
		this.area = area;
		this.maxHits = maxHits;
		this.damage = damage;
		ball.BlockHitted.AddListener ((BlockScript arg0) => MakeBoom ());

		var hero = (Player_BarController)Saves.SaveSystem.GetCurrentHero ().GetActualBar ();
		actualBonus = new BonusVelocity (Velocity,1f);
		hero.AddBonus (actualBonus);
	}
	void MakeBoom()
	{		
		var boom = Instantiate (Explotion, transform.position, Quaternion.identity);
		boom.transform.localScale = Vector3.one * area;
		actualBonus.Desactivate ();
		Destroy (this.gameObject);
	}
}
