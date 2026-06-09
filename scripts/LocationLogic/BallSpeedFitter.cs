using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallSpeedFitter : MonoBehaviour {
	public float Bonus = -2f;
	public float time=-1f;
	// Use this for initialization
	void Start () {
		var hero = ServiceLocator.GetConfiguraciones ().Heroes [Saves.SaveSystem.GetCurrentCharacterData().claseIndex];
		hero.GetActualBar ().AddBonus (new BonusVelocity (Bonus,time));
	}
	

}
