using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class StaticEffects_SwapOfSprite {

	public static IEnumerator SwapSprite(SpriteRenderer spriteRendererActual, Sprite toSet, float timeToSwap)
	{
		GameObject swappObject = new GameObject ("swappGrafic");
		swappObject.SetParentWithScaleOne (spriteRendererActual.transform, Vector3.zero);
		var spriteRenderer_Swap = swappObject.AddComponent<SpriteRenderer> ();
		spriteRenderer_Swap.sprite = toSet;
		spriteRenderer_Swap.sortingLayerID = spriteRendererActual.sortingLayerID;
		spriteRenderer_Swap.sortingOrder = spriteRendererActual.sortingOrder+1;
		float timeStart = Time.time;
		Color colorSwapp = new Color (1, 1, 1, 0);
		while (colorSwapp.a != 1||spriteRenderer_Swap==null) {
			colorSwapp.a = Mathf.Clamp01 ((Time.time - timeStart) / timeToSwap);
			spriteRenderer_Swap.color = colorSwapp;
			yield return new WaitForEndOfFrame ();
		}
		if (spriteRendererActual != null) 
			spriteRendererActual.sprite = toSet;
		MonoBehaviour.Destroy (swappObject);		

	}
}
