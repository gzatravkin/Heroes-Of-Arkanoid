using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Engineer_LightingSpell : SO_VisibleSkill {
	[HideInInspector]
	public List<BlockScript> blocksToHit;
	public float minDelay;
	public static float LastTime;
	public Sprite[] Lightings;
	public GameObject LightingObj;
	[HideInInspector]
	public SkillNumberParameter repeatChance=new SkillNumberParameter(0.25f,0.55f,0.75f);
	protected override void SkillInitialization ()
	{
		base.SkillInitialization ();
		BattleEventsManager.Events.UpdateEvent.AddListener (()=>Update());
	}
	protected override void SpellCast ()
	{
		base.SpellCast ();
		var blocks = GameField.objRef.GetBlocks ();
		int blocksAdded = 0;
		while (blocks.Count>0&&(( Random.Range (0f, 1f) < repeatChance)||blocksAdded==0)){
			var number = Random.Range (0, blocks.Count);
			blocksToHit.Add (blocks [number]);
			blocks.RemoveAt (number);
			blocksAdded++;
		}
	}
	void Update()
	{
		blocksToHit.RemoveAll (x => x == null);
		if (blocksToHit.Count > 0 && LastTime + minDelay < TimeManager.GetTime(TimeType.RealTime)) {			
			LastTime = TimeManager.GetTime (TimeType.RealTime);
			ShowLighting (blocksToHit [0].transform.position);
			blocksToHit [0].GetHit ();
			blocksToHit.RemoveAt (0);
		}
	}
	void ShowLighting(Vector3 Pos)
	{
		var t = Instantiate (LightingObj, Pos, Quaternion.identity);	
		t.GetComponent<SpriteRenderer> ().sprite = Lightings [Random.Range (0, Lightings.Length)];
	}
}
