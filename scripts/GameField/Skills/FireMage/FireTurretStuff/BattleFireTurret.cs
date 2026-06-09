using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleFireTurret : MonoBehaviour {
	public float MinTimeToWaitBetweenShoots;
	private float LastShootTime;
	public int HitsToDie;
	public float TimeToDie;
	public int Bullet_MaxHits,Bullet_Damage;
	public TimeType timeType;
	public GameObject Bullets, BulletAnimation;
	public float BulletAnimationTime;
	public float BulletMaxTime=5f;
	public GameObject TurretGraficObject;
	void Update()
	{
		TimeToDie += -TimeManager.GetDeltaTime (timeType);
		if (TimeToDie <= 0||HitsToDie<=0)
			Destroy (this.gameObject);
	}
	void Start()
	{
		BattleEventsManager.Events.Ball_BarCollision.AddListener ((x,y)=>Launch());
	}
	void Launch()
	{
		if (LastShootTime+MinTimeToWaitBetweenShoots<TimeManager.GetTime(timeType))
		{
			LastShootTime = TimeManager.GetTime(timeType);
			var t = (GameObject)Instantiate (BulletAnimation);
			t.SetParentWithScaleOne (TurretGraficObject.transform);
			StartCoroutine (Effects_FadeAndDie.FadeAndDie (t.GetComponent<SpriteRenderer> (), BulletAnimationTime));
			var bullets = (GameObject)Instantiate (Bullets, TurretGraficObject.transform.position, transform.rotation);
			bullets.SetParentByName ("Effects");
			var blockHitters = bullets.GetComponentsInChildren<BlockHitter> ();
			foreach (BlockHitter bH in blockHitters) {
				bH.Damage = Bullet_Damage;
				bH.MaxHits = Bullet_MaxHits;
			}
			Destroy (bullets, BulletMaxTime);
		}
	}
}
