using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AnimatorController : MonoBehaviour {
	private Animator animator;
	public string Begining="Begining", Hitted="Hitted", Die="Die";
	public BlockScript blockTarget;
	public List<string> AnimationStack;
	protected virtual void Start()
	{
		animator = GetComponent<Animator> ();
		SetAnimation (Begining);
		if (blockTarget != null)
			blockTarget.OnHitted.AddListener (x => OnHitted ());
	}
	void OnHitted()
	{
		if (blockTarget != null && blockTarget.Killed == false &&blockTarget.HP>0) {
			if (Hitted != "")				
				AnimationStack.Insert (0, Hitted);
		} else if (Die != "") {			
			AnimationStack.Clear ();
			SetAnimation (Die);
		}
	}
	protected virtual void OnAnimationStackFinished()
	{
	}
	protected virtual void Update()
	{
		if (animator.GetCurrentAnimatorStateInfo (0).normalizedTime >= 1.0f) {
			if (AnimationStack.Count > 0) {
				SetAnimation (AnimationStack [0]);
				AnimationStack.RemoveAt (0);
			} else {
				OnAnimationStackFinished ();
			}
		}
	}
	public void SetAnimation(string name)
	{		
		animator.PlayInFixedTime (name);
	}
}
