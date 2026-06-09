using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SO_Paladin_LastDaySpell : SO_VisibleSkill {
	[HideInInspector]
	public SkillNumberParameter MaxHitsForLine = new SkillNumberParameter (3, 4, 5);
	[HideInInspector]
	public SkillNumberParameter Size = new SkillNumberParameter (0.7f, 0.9f, 1.2f);
	[HideInInspector]
	public SkillNumberParameter MaxLines = new SkillNumberParameter (2, 3, 4,true);
	public float MinDelay=0.5f;
	private float LastTime;
	private int LibresLines;
	public GameObject LastDayNuke;
	public GameObject NubeSample;
	public static GameObject Nube;
	protected override void SkillInitialization ()
	{
		base.SkillInitialization ();
		LibresLines = 0;
		LastTime = 0;
		BattleEventsManager.Events.Ball_TopWallCollision.AddListener((x)=>Hitted(x));
	}
	protected override void SpellCast ()
	{
		base.SpellCast ();
		if (Nube == null)
			Nube = Instantiate (NubeSample) as GameObject;
		LibresLines += MaxLines;
	}
	public void Hitted(Vector2 pos)
	{
		if (LibresLines <= 0||LastTime+MinDelay>TimeManager.GetTime(TimeType.RealTime))
			return;		
		LastTime = TimeManager.battleTime;
		LibresLines--;
		if (LibresLines == 0)
			Destroy (Nube);
		var t = Instantiate (LastDayNuke, pos, Quaternion.identity) as GameObject;
		t.transform.localScale = t.transform.localScale * Size;
		t.GetComponent<BlockHitter> ().MaxHits = MaxHitsForLine;
	}
}
