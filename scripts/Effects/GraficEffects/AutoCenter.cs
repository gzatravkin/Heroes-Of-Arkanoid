using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_5
[ExecuteInEditMode]
#endif
public class AutoCenter : MonoBehaviour {

	// Use this for initialization
	void Start () {
		transform.localRotation = Quaternion.identity;
		transform.localPosition = Vector3.zero;
	}
	
	// Update is called once per frame
	void Update () {
		transform.localRotation = Quaternion.identity;
		transform.localPosition = Vector3.zero;
	}
}
