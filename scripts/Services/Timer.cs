using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Timer : MonoBehaviour {
	public Text timer;
	public void SetTime(float time)
	{
		timer.text = Mathf.Round(time).ToString();
	}
	public void DestroyTimer()
	{
		Destroy (this.gameObject);
	}
}
