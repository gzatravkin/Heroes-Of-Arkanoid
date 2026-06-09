using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DieWhenItInvisible : MonoBehaviour {
	private SpriteRenderer spriteRenderer;
	public int MaxFramesInvisibles = 5;
	private int FrameCounter=0;
	// Use this for initialization
	void Start () {
		spriteRenderer = GetComponent<SpriteRenderer> ();
	}
	
	// Update is called once per frame
	void Update () {		
		if (FrameCounter>MaxFramesInvisibles&&!spriteRenderer.isVisible)//All objects in first frame can be invisible
			Destroy (this.gameObject);		
		FrameCounter++;
	}
}
