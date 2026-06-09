using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
[CreateAssetMenu(menuName="ScriptableObject/Items/BallModificator")]
public class SO_ItemBallModificator : SO_AbstractItem {
	public GameObject ballEffect;
	public float[] lvlSize;
	protected override void ItemInitialization ()
	{
		base.ItemInitialization ();
		Debug.Log ("Level " + Level.ToString ());
		BattleEventsManager.Events.BallAdded.AddListener((BallController arg0) => SetBallEffect(arg0));
	}
	public void SetBallEffect(BallController ball)
	{
		ball.transform.localScale = ball.transform.localScale * lvlSize [Level];
		if (ballEffect != null) {
			var effect = Instantiate (ballEffect, ball.transform.position, ball.transform.rotation);
			effect.SetParentWithScaleOne (ball.transform);
		}
	}
}
