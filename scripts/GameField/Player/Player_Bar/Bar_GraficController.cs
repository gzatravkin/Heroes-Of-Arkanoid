using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bar_GraficController : MonoBehaviour {
	public GameObject[] SizeObjs = new GameObject[0];
	private GameObject CurrentGraficObject;
	private Animator currentAnimator;
	private float DeltaPosX;
	private float LastX;
	public int size = 1;
	void Start()
	{
		DeltaPosX = 0f;
		LastX = transform.position.x;
		BattleEventsManager.Events.SpellCasted.AddListener (() => SetTrigger ("SpellCasted"));
	}
	public void SetTrigger(string Trigger)
	{
		if (currentAnimator != null) {
			currentAnimator.SetTrigger (Trigger);
		}
	}
	public float GetWidthOfSprite(Sprite sprite)
	{
		return sprite.bounds.size.x;
	}
	public void SetSizeLevel(int n)
	{
		n = Mathf.Clamp (n,0, SizeObjs.Length-1);
		size = n;
		SetSize(n);
	}
	void Update()
	{
		DeltaPosX = transform.position.x - LastX;
		LastX = transform.position.x;
		if (currentAnimator != null) {			
			currentAnimator.SetFloat ("Speed", DeltaPosX);
		}
	}
	private void SetSize (int size)
	{					
		if (CurrentGraficObject != null)
			Destroy (CurrentGraficObject);
		CurrentGraficObject = Instantiate(SizeObjs[size],transform.position,Quaternion.identity);
		CurrentGraficObject.SetParentWithScaleOne (transform);
		currentAnimator = CurrentGraficObject.GetComponent<Animator> ();
		var col = GetComponent<BoxCollider2D> ();
		col.size = new Vector2 (GetWidthOfSprite(CurrentGraficObject.GetComponent<SpriteRenderer>().sprite)*2, col.size.y);
	}
}
