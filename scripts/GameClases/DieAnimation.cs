using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public class DieAnimation{
	public GameObject dieObject;
	public float TimeToClean=10f;
	public bool KeepParent;
	public bool SetOldSprite = false;
	public DieAnimation(GameObject obj)
	{
		dieObject = obj;
		TimeToClean = 10f;
	}
	public DieAnimation()
	{
		TimeToClean=10f;
	}
	public void Die(GameObject author)
	{
		if (dieObject == null)
			return;
		var t = MonoBehaviour.Instantiate (dieObject, author.transform.position, author.transform.rotation);
		t.transform.localScale = author.transform.lossyScale;
		if (KeepParent)
			t.transform.parent = author.transform.parent;
		else
			t.SetParentByName ("DieAnimationes");
		if (SetOldSprite) {
			var newSpriteRenderer = t.GetComponent<SpriteRenderer> ();
			var oldSpriteRenderer = author.gameObject.GetComponent<SpriteRenderer> ();
			if (newSpriteRenderer!=null&&oldSpriteRenderer!=null)
			{
				newSpriteRenderer.sprite = oldSpriteRenderer.sprite;
			}
		}
		if (TimeToClean>0)
		MonoBehaviour.Destroy (t.gameObject, TimeToClean);
	}
}
