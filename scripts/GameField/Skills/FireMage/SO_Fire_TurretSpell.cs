using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
public class SO_Fire_TurretSpell : SO_VisibleSkill {	
	public const float MinTimeToWaitBetweenShoots=0.1f;
	public GameObject FireTurret;
	[HideInInspector]
	public SkillNumberParameter MaxTime,MaxBullets,BulletDamage,MaxHits;
	public TimeType timeType = TimeType.BattleTime;

	protected override void SpellCast ()
	{
		var fireTurret = (GameObject)Instantiate (FireTurret);
		fireTurret.SetParentWithScaleOne (BattleController.GetCurrentPlayerObject().transform);
		var turretComponent = fireTurret.GetComponent<BattleFireTurret> ();
		turretComponent.Bullet_Damage = BulletDamage;
		turretComponent.TimeToDie = MaxTime;
		turretComponent.HitsToDie = MaxBullets;
		turretComponent.Bullet_MaxHits = MaxHits;
		turretComponent.MinTimeToWaitBetweenShoots = MinTimeToWaitBetweenShoots;
	}
}
