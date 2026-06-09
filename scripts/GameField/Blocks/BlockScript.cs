using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class BlockScript : MonoBehaviour {	
	[HideInInspector]
	public UnityEvent_Int OnHitted = new UnityEvent_Int ();
	[HideInInspector]
	public UnityEvent OnDestoyed= new UnityEvent ();
	private SpriteRenderer sprite;
	private int DefaultHp=2;
	public int HP = 2;
	[HideInInspector]
	public bool Killed = false;
	[HideInInspector]
	public AbstractBlockEffect[] BlockEffects;
	public bool NeedToKill=true;
	public void OnValidate()
	{
		RefreshBlockEffects ();
	}
	public void RefreshBlockEffects()
	{
		BlockEffects = GetComponents<AbstractBlockEffect> ();
		foreach (AbstractBlockEffect abe in BlockEffects)
			abe.Initialization (DefaultHp,this);
	}
	public void GetHit(int damage = 1, DamageType damageType = DamageType.Magic)
	{		
		if (Killed == false) {			
			foreach (AbstractBlockEffect abe in BlockEffects)
				abe.Hitting (ref damage, damageType);
			HP += -damage;
			foreach (AbstractBlockEffect abe in BlockEffects)
				abe.Hitted (damage, damageType);			
			if (HP <= 0)
				Die ();			
		}
		OnHitted.Invoke (damage);
	}
	public bool IsActive()
	{
		return (sprite!=null)&&(sprite.isVisible)&&(gameObject.activeInHierarchy);
	}
	void Start()
	{
		DefaultHp = HP;
		sprite = GetComponent<SpriteRenderer> ();
		RefreshBlockEffects ();
	}
	public void Die()
	{
		Killed = true;
		OnDestoyed.Invoke ();
		BattleController.DestroyBlockWithCallback (this);
		foreach (AbstractBlockEffect abe in BlockEffects)
			abe.Die ();		
	}
}
