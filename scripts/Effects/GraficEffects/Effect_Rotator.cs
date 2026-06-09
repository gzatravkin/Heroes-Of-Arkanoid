using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Effect_Rotator : GraficEffect {
	public int Pos=0;
	[System.Serializable]
	public class AnimationData
	{
		public Vector3 EulerAngle;
	public float TimeToSwap;
	}
	public AnimationData[] animationData;
	public bool RandomizeStartPos=false;
	protected override void Start()
	{
		base.Start ();
		if (RandomizeStartPos) {
			Pos = Random.Range(0,animationData.Length);
			Counter = Random.Range (0, animationData [Pos].TimeToSwap);
		}
	}
	protected override void SetDefaultParameters ()
	{
		base.SetDefaultParameters ();
		Pos = 0;
	}
protected override void Update ()
	{
		base.Update ();
		if (Counter >= animationData[Pos].TimeToSwap) {
			Counter = 0;
			Pos++;		
			if (Pos >= animationData.Length) {
				Pos = 0;
				base.FinAnimation ();
			}
		}
		int other = Pos + 1;
		if (other >= animationData.Length)
			other = 0;
		var angle1 = Quaternion.Euler (animationData [Pos].EulerAngle);
		var angle2 = Quaternion.Euler (animationData [other].EulerAngle);
		transform.localRotation = Quaternion.Lerp (angle1, angle2, (Counter / animationData [other].TimeToSwap));
			
	}
}
