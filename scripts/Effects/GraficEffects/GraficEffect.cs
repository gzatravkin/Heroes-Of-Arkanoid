using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class GraficEffect : MonoBehaviour {
	public UnityEngine.Events.UnityEvent onFinAnimation;
	[System.Serializable]
	public class OnFinAnimationEffect
	{
		[System.Serializable]
		public enum Effect{
		Repeat, StopAnimation, DisableObj, DestroyObj
		}
		public Effect effect;
		public void Fin(MonoBehaviour animScript)
	{			
			if (effect == Effect.DestroyObj)
				Destroy (animScript.gameObject);
			if (effect == Effect.DisableObj)
				animScript.gameObject.SetActive (false);
	}

	}
	public bool StartOnActive=true;
	public OnFinAnimationEffect onFinAnimationEffect;
	protected SpriteRenderer spriteRenderer;
	public TimeType timeType;
	public bool PermiteReplayBeforeFinished = false;
	protected float Counter;
	protected bool Playing=false;
	protected virtual void Start()
	{
		if (StartOnActive) {
			Playing = true;
		}
		spriteRenderer = GetComponent<SpriteRenderer> ();
	}
	protected virtual void SetDefaultParameters()
	{
		Counter = 0;
	}
	public void BeginAnimation()
	{
		if (PermiteReplayBeforeFinished && Playing) {
			SetDefaultParameters ();
		}
		if (!Playing) {
			SetDefaultParameters ();
			Playing = true;
		}
		
	}
	public void FinAnimation()
	{
		Playing = false;
		onFinAnimationEffect.Fin (this);
		onFinAnimation.Invoke ();
		if (onFinAnimationEffect.effect==OnFinAnimationEffect.Effect.Repeat)
			BeginAnimation ();
	}
	protected virtual void Update()
	{
		if (Playing) {
			Counter += TimeManager.GetDeltaTime (timeType);
		}
	}
}
