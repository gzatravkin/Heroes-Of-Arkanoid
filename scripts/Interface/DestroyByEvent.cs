using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DestroyByEvent : MonoBehaviour {
	public GameObject toDestroy;
	void OnValidate()
	{
		if (toDestroy == null)
			toDestroy = this.gameObject;
	}
	public void Destroy()
	{
		Destroy (toDestroy);
	}
}
