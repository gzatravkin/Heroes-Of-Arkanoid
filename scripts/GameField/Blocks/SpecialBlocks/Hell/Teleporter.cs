using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Teleporter : MonoBehaviour {
	public static List<Teleporter> TeleportObjects;
	public int Color = 0;
	public float minDelayToAcept=0.2f;
	[HideInInspector]
	public float CurrentDelay = 0f;
	public SpriteRenderer ColorTarget;
	void OnValidate()
	{
		if (ColorTarget != null)
			ColorTarget.color = SO_Configuraciones.obj.TeleporterColors [Color];
	}
	// Use this for initialization
	void Start () {
		if (TeleportObjects == null)
			TeleportObjects = new List<Teleporter> ();
		TeleportObjects.RemoveAll (x => x == null);
		TeleportObjects.Add (this);
	}
	void Update()
	{
		if (CurrentDelay >= 0) {
			CurrentDelay += -TimeManager.battleDeltaTime;
		}
	}
	void OnCollisionEnter2D(Collision2D col)
	{
		if (col.rigidbody.isKinematic == false)
			MakeTeleport (col.rigidbody);
	}
	public void TeleportationMaked()
	{
		CurrentDelay = minDelayToAcept;
	}
	public void MakeTeleport(Rigidbody2D obj)
	{
		List<Teleporter> others = TeleportObjects.FindAll((x=>x!=null&&x.Color==this.Color&&x!=this&&x.gameObject.activeInHierarchy&&x.CurrentDelay<=0));			
		if (others.Count > 0) {
			Teleporter randomOther = others [0];
			obj.position = randomOther.transform.position;
			randomOther.TeleportationMaked ();
			TeleportationMaked ();
		}
	}
}
