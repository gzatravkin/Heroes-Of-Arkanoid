using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecuperableObject : MonoBehaviour {
	public GameObject obj;
	public void Recuperate()
	{
		if (obj != null) {
			var block = Instantiate (obj, transform.position, transform.rotation);
			block.transform.localScale = transform.localScale;
		}
		Destroy (this.gameObject);
	}

}
