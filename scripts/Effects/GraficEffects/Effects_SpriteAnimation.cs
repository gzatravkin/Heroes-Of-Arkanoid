using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Effects_SpriteAnimation : GraficEffect {
	public float TimeToSwap=0.1f;
	public Sprite[] sprites;
	public int CyclePos = 0;
	private int pos = 0;
	public SpriteRenderer get_spriteRenderer {
		get{ 
			if (spriteRenderer == null)
				spriteRenderer = GetComponent<SpriteRenderer> (); 
			return spriteRenderer;
		}
	}
	protected override void Start()
	{
		base.Start ();
		if (sprites.Length > 0)
			get_spriteRenderer.sprite = sprites [0];
	}
	void OnValidate()
	{
		if (sprites.Length > 0)
			get_spriteRenderer.sprite = sprites [0];			
	}
	protected override void SetDefaultParameters ()
	{
		base.SetDefaultParameters ();
		pos = CyclePos;
	}
	protected override void Update()
	{
		base.Update();
		if (Counter >= TimeToSwap) {
			Counter = 0;
			pos++;		
			if (pos >= sprites.Length) {
				FinAnimation ();
				pos = CyclePos;
			}
			if (sprites.Length>0)
				get_spriteRenderer.sprite = sprites [pos];

		}
	}
}
