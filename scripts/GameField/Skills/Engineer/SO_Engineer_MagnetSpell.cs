using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SO_Engineer_MagnetSpell : SO_VisibleSkill {

	public SkillNumberParameter Area = new SkillNumberParameter(4,6,8);
	public SkillNumberParameter Velocity = new SkillNumberParameter(1,3,4);
	public SkillNumberParameter Time = new SkillNumberParameter(5,7,10);
	public List<Rigidbody2D> blocksToMove;
	public BallController ballToFollow;
	private float magnetTime=0;
	protected override void SkillInitialization ()
	{
		base.SkillInitialization ();
		magnetTime = 0;
		BattleEventsManager.Events.UpdateEvent.AddListener (() => Update ());
	}
	protected override void SpellCast ()
	{
		base.SpellCast ();
		magnetTime += Time;

	}
	void Update()
	{
		magnetTime += -TimeManager.unscaledDeltaTime;
		if (magnetTime <= 0) {
			magnetTime = 0;
			return;
		}
		if (ballToFollow == null) {
			var hero = (Player_BarController)(Saves.SaveSystem.GetCurrentHero ().GetActualBar ());
			if (hero!=null)
			ballToFollow = hero.GetRandomBall ();
		}

		if (ballToFollow != null) {
			var blocksAffected = GameField.objRef.GetBlocksInArea (ballToFollow.transform.position, Area);
			blocksToMove.Clear ();
			foreach (BlockScript b in blocksAffected) {
				var rigi = b.GetComponent<Rigidbody2D> ();
				if (rigi != null&&b.GetComponent<Inmagnitable>()==null) {
					blocksToMove.Add (rigi);
					rigi.isKinematic = false;
					rigi.gravityScale = 0f;
				}
			}
			blocksToMove.RemoveAll (x => x == null);
			foreach (Rigidbody2D rigi in blocksToMove) {				
				if (TimeManager.IsInBattle ())
					rigi.velocity = ((Vector2)ballToFollow.transform.position - rigi.position).normalized * Velocity;
				else
					rigi.velocity = Vector2.zero;
			}
		}
	}

}
