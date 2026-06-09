using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player_BarController : AbstractPlayerController {
	private BarMove bar;
	[HideInInspector]
	public BarLogic barLogic;
	[HideInInspector]
	public BarMove barMove;
	[HideInInspector]
	public Bar_GraficController barGraficController;
	public float DefaultBallSpeed=15;
	public int DefaultSize = 1;
	public float MinBallSpeed;
	[HideInInspector]
	public float CurrentBallSpeed;
	[HideInInspector]
	public int CurrentSize;
	public void InitializarComponents()
	{
		barLogic = GetComponent<BarLogic> ();
		barGraficController = GetComponent<Bar_GraficController> ();				
		barMove = GetComponent<BarMove> ();
	}
	public override void SetDefaultCaracteristicas ()
	{
		base.SetDefaultCaracteristicas ();
		barMove.MovimientSpeed = barMove.defaultMovimientSpeed;
		CurrentSize = DefaultSize;
		CurrentBallSpeed = DefaultBallSpeed;
	}
	public override void RecalculateBonuses ()
	{
		base.RecalculateBonuses ();
		CurrentBallSpeed = Mathf.Max (MinBallSpeed, CurrentBallSpeed);
		barGraficController.SetSizeLevel (CurrentSize);
	}
	public void SetSize(int size)
	{
		DefaultSize = size;
		barGraficController.SetSizeLevel (size);
	}
	public override bool IsInGame ()
	{
		return !(barLogic.BallLocked != null&&barLogic.Balls.Count==1);//true если есть только один мяч, и он залочен
	}
	public override void Initialization ()
	{
		base.Initialization ();
		InitializarComponents ();
		bonuses = new List<AbstractBonus> ();
		RecalculateBonuses ();
	}
	public List<BallController> GetBalls()
	{
		return barLogic.Balls;
	}
	public BallController GetRandomBall()
	{
		if (barLogic.Balls.Count > 0)
			return (barLogic.Balls [Random.Range (0, barLogic.Balls.Count)]);
		else
			return null;
	}
}
