using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Effects_FadeAndDie : MonoBehaviour {
	public float TimeToDie=0.5f;
	public void Start()
	{
		StartCoroutine(FadeAndDie(GetComponent<SpriteRenderer>(),TimeToDie));
	}
	public static IEnumerator FadeAndDie(SpriteRenderer spriteRenderer, float timeToDie)
	{
		
		float timeStart = Time.time;
		Color colorSwapp = new Color (1, 1, 1, 1);
		while (colorSwapp.a != 0) {
			colorSwapp.a = (1-Mathf.Clamp01 ((Time.time - timeStart) / timeToDie));
			spriteRenderer.color = colorSwapp;
			yield return new WaitForEndOfFrame ();
		}
		MonoBehaviour.Destroy (spriteRenderer.gameObject);
		//		MonoBehaviour.Destroy (swappObject);

	}
}
