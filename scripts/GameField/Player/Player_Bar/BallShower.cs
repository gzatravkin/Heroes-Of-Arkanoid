using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallShower : MonoBehaviour {
	public List<GameObject> Balls;
	public GameObject BallSample;
	public int BallCount;
	void OnValidate()
	{
		BallShow (BallCount);
	}
	public void BallShow(int Count)
	{
		if (BallSample == null)
			return;
		BallCount = Count;
		while ((Balls.Count > Count)) {
			Destroy (Balls [0]);
			Balls.RemoveAt (0);
		}
		while (Balls.Count < Count)
		{
			GameObject Ball = (GameObject)Instantiate (BallSample);
			Ball.SetParentWithScaleOne (transform);
			Balls.Add (Ball);
		}
	}
}
