using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Engineer_Passive : SO_InvisibleSkill {
	[HideInInspector]
	public SkillNumberParameter chance = new SkillNumberParameter(0.1f,0.12f,0.15f);
	public GameObject Particle;
	public GameObject Nuke;
	[HideInInspector]
	public SkillNumberParameter cantidadToBoom=new SkillNumberParameter(4,3,2,true);
	[HideInInspector]
	public SkillNumberParameter maxHits=new SkillNumberParameter(4,3,2,true);
	[HideInInspector]
	public SkillNumberParameter nukeSize=new SkillNumberParameter(4,3,2,true);
	public static int BoomCounter = 0;
	protected override void SkillInitialization ()
	{
		base.SkillInitialization ();
		BattleEventsManager.Events.BlockDestroyed.AddListener ((x) => BlockDestroyed (x));
	}
	void BlockDestroyed(BlockScript block)
	{
		if (Random.Range (0, 1f) < chance) {
			var t = Instantiate (Particle, block.transform.position, Quaternion.identity);
			var particle = t.GetComponent<Engineer_PassiveParticle> ();
			particle.BarLanded.AddListener (() => ParticleLanded ());
		}
	}
	void ParticleLanded()
	{
		BoomCounter ++;
		if (BoomCounter >= cantidadToBoom) {
			var t = Instantiate(Nuke,BattleController.GetCurrentPlayerObject ().transform.position+Vector3.up,Quaternion.identity);
			t.GetComponent<BlockHitter> ().MaxHits = maxHits;
			t.transform.localScale = t.transform.localScale * nukeSize;
			BoomCounter = 0;
		}
	}
}
