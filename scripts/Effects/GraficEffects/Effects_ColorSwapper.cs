using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Effects_ColorSwapper : GraficEffect {	

	public Gradient AnimationData;
	public float PlayTime=2f;
	protected override void Start ()
	{
		base.Start ();
	}
	protected override void Update()
	{
		base.Update();
		spriteRenderer.color = AnimationData.Evaluate (Counter / PlayTime);
		if (Counter >= PlayTime) {
			base.FinAnimation ();
		}
	}
}
