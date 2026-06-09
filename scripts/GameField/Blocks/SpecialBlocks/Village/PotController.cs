using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PotController : MonoBehaviour {

	public TimeType	timeType;
	private float Counter;
	public float TimeToChangeState=4f;
	public int pos=0;
	public GameObject[] states;
	// Update is called once per frame
	void Start()
	{
		pos = Random.Range (0, states.Length);
		Counter = Random.Range (0, TimeToChangeState);
		RefreshPos ();
	}
	void RefreshPos()
	{
		for (int i = 0; i < states.Length; i++) {
			if (i==pos)
				states [i].SetActive (true);
			else				
				states [i].SetActive (false);	
		}
	}
	void DieCheck()
	{
		for (int i = 0; i < states.Length; i++) {
			if (states [i] == null)
				Destroy (this.gameObject);
			}
	}
	void Update () {
		DieCheck ();
		Counter += TimeManager.GetDeltaTime (timeType);
		if (Counter >= TimeToChangeState) {
			Counter = 0;
			pos++;
			if (pos >= states.Length)
				pos = 0;
			RefreshPos ();
		}
	}
}
