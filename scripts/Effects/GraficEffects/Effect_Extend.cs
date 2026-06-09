using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Effect_Extend : GraficEffect {
	public Vector3 scaleFrom;
	public Vector3 scaleTo;
	public bool SwapScalesOnReplay=true;
	public float TimeToExtend;
	// Use this for initialization
	protected override void Start()
	{
		base.Start();
	}
protected override void SetDefaultParameters ()
	{
		base.SetDefaultParameters ();
		if (SwapScalesOnReplay) {
			var t = scaleTo;
			scaleTo = scaleFrom;
			scaleFrom = t;
		}
		transform.localScale = scaleFrom;

	}
	// Update is called once per frame
	protected override	void Update () {
		base.Update ();
		transform.localScale = Vector3.Lerp (scaleFrom, scaleTo, Counter / TimeToExtend);
		if (Counter >= TimeToExtend)
			FinAnimation ();
	}
}
