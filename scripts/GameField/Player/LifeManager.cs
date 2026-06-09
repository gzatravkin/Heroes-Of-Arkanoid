using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LifeManager : MonoBehaviour {
	public Image LifeScaleBar;
	public BallShower ballShower;
	public static LifeManager objRef;
	public static int maxBalls;
	private static int Balls;
	private static float Lifes;
	public static float maxLifes;
	public static int GetCurrentBalls()
	{
		return Balls;
	}
	void Awake()
	{
		objRef = this;
	}
	public static void SetCurrentLifes(float lifes)
	{
		Lifes = lifes;
	}
	public static void SetCurrentBalls(int balls)
	{
		Balls = balls;
	}
	public static float GetCurrentLifes()
	{
		return Lifes;
	}
	public static void AddBalls(int count)
	{
		Balls += count;
		objRef.RefreshBalls ();
	}
	public static void AddMaxHP(float hp)
	{
		maxLifes += hp;
		Lifes += hp;
	}
	public static void AddHp(float Hp)
	{
		Lifes += Hp;
		if (Lifes > maxLifes)
			Lifes = maxLifes;
		objRef.RefreshLifes ();
		if (Lifes <= 0)
			MissionWinController.objRef.Loose ();
	}
	public static void GetDamage(float damage)
	{
		Lifes += -damage;
		BattleEventsManager.Events.HpLoosed.Invoke (damage);
		objRef.RefreshLifes ();
		if (Lifes <= 0)
			MissionWinController.objRef.Loose ();
	}
	public static void GetBall()
	{
		Balls--;
		objRef.RefreshBalls ();
	}
	public static void SetDefaultLifes()
	{		
		Lifes = Saves.SaveSystem.GetCurrentHero().StartLifes;
	}
	public static void SetDefaultBalls()
	{
		Balls = maxBalls;
	}
	public void Initialization(float lifes, int balls)
	{
		Lifes = lifes;
		Balls = balls;
		maxLifes = lifes;
		maxBalls = balls;
		ServiceLocator.lifeManager = this;
		RefreshBalls ();
		RefreshLifes ();
	}
	public void RefreshBalls()
	{
		ballShower.BallShow (Balls);
	}
	public void RefreshLifes()
	{
		LifeScaleBar.fillAmount = Lifes / maxLifes;
	}
}
