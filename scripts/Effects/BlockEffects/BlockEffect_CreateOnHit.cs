using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockEffect_CreateOnHit : AbstractBlockEffect {
	[System.Serializable]
	public class ObjectsData
	{
		public GameObject prefab;
		public Sprite spriteToRemplace;
		public int HP;
		public bool IgnoreHP=false;
		public bool makeItChild = true;
	}
	public List<ObjectsData> hitDeppenditive;
	public override void Hitted (int damage, DamageType damageType)
	{
		base.Hitted (damage, damageType);
		var ObjectsToSet = hitDeppenditive.FindAll (x => x.HP == (block.HP)||x.IgnoreHP);
		foreach (var o in ObjectsToSet )			
			Create (o);
	}
	void Create(ObjectsData objData)
	{
		var obj = Instantiate (objData.prefab, transform.position, transform.rotation);
		obj.SetParentWithScaleOne (transform);
		if (!objData.makeItChild)
			obj.SetParentByName ("HitEffects");		
		if (objData.spriteToRemplace!=null)
		{
			var spriteRenderer = obj.GetComponent<SpriteRenderer> ();
			if (spriteRenderer == null)
				spriteRenderer = obj.GetComponentInChildren<SpriteRenderer> ();
			if (spriteRenderer != null)
				spriteRenderer.sprite = objData.spriteToRemplace;
		}
	}
}
