using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoblinController : AnimatorController {
	public int pos = 0;
	public int AttacksMax = 5;
	public string Stand = "StandAnimation";
	public BlockScript blockScript;
	public GameObject StalaktitPrefab;
	public Vector3 StalaktitPos;
	public float StalaktitRadius;
	public int StalaktitCount=5;
	public List<string> Attacks = new List<string>();
	public List<JumpAnimationData> jumpAnimationData = new List<JumpAnimationData>();
	[System.Serializable]
	public class JumpAnimationData
	{
		public List<string> AnimationStack;
		public int posFrom, posTo;
	}
	public void StalaktitCreation()
	{
		for (int i = 0; i < StalaktitCount; i++) {
			var stalaktit = Instantiate (StalaktitPrefab, StalaktitPos, Quaternion.identity);
			stalaktit.SetZ (0);
			stalaktit.transform.position = stalaktit.transform.position + (Vector3)Random.insideUnitCircle * StalaktitRadius;
		}
	}		
	void MoveTo(int dir)
	{		
		if (this.pos!=dir)
		{
			AnimationStack.AddRange (jumpAnimationData.Find(x=>x.posTo==dir&&x.posFrom==pos).AnimationStack);
			pos = dir;
		}
	}
	protected override void OnAnimationStackFinished ()
	{
		base.OnAnimationStackFinished ();
		int atacks = Random.Range (1, AttacksMax);
		for (int i = 0; i < atacks; i++) {
			AnimationStack.Add(Attacks[Random.Range(0,Attacks.Count)]);
			if (Random.Range(0,1f)<0.5f)
				AnimationStack.Add(Stand);
		}
		MoveTo (Random.Range (-1, 2));
	}
				
}
