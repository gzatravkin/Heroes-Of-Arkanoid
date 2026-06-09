using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireWallController : MonoBehaviour {	
	private UnityEngine.Events.UnityAction<BlockScript> action;
	private GameObject FireAnimation;
	private GameObject Ball_FireEffect;
	private List<BlockScript> blocksToPutInFire;
	private List<BlockInFire> blocksInFire;
	private float fireAnimationDelay=0f;
	private float fireExtendDelay = 0f;
	private float currentExtendDelay=0f;
	private float Counter = 0f;
	private float AnimationRandomCircle = 0.2f;
	private int damage=1;
	private int EffectCounter = 2;
	private bool Started;
	private class BlockInFire
	{
		public List<GameObject> FireAnimations;
		public BlockScript block;
		public float TimeToHit;
		public void Hit(int damage)
		{
			if (block != null)
				block.GetHit ();
			foreach (GameObject go in FireAnimations)
				if (go!=null)
					Destroy (go);
		}
	}
	public void PutBlockInFire(BlockScript block, float TimeToHit)
	{
		blocksToPutInFire.Remove (block);
		var blockInFire = new BlockInFire ();
		blockInFire.FireAnimations = new List<GameObject> ();
		int Animations = Random.Range (1, EffectCounter+1);
		for (int i = 0; i < Animations; i++) {			
			var fireObj = (GameObject)Instantiate (FireAnimation);			
			fireObj.transform.parent = block.transform;
			fireObj.transform.position = block.transform.position + (Vector3)Random.insideUnitCircle * AnimationRandomCircle;
			blockInFire.FireAnimations.Add (fireObj);
		}
		blockInFire.block = block;
		blockInFire.TimeToHit = TimeToHit;
		blocksInFire.Add (blockInFire);
	}
	public void Initialization(GameObject FireAnimation, GameObject Ball_FireEffect, BallController ball, int Damage, float Area, int MaxBlocks, int BlocksInAreaToMax, float fireExtendDelay=3f, float fireAnimationDelay=0.5f)
	{		
		this.FireAnimation = FireAnimation;
		this.Ball_FireEffect = Ball_FireEffect;
		this.fireExtendDelay = fireExtendDelay;
		this.fireAnimationDelay = fireAnimationDelay;
		this.damage = Damage;
		action = (arg0) => StartFlame (ball, Area, MaxBlocks, BlocksInAreaToMax);
		ball.BlockHitted.AddListener (action);
		blocksToPutInFire = new List<BlockScript> ();
		blocksInFire = new List<BlockInFire> ();
	}
	private void StartFlame(BallController ball, float Area, int MaxBlocks, int BlocksInAreaToMax)
	{		
		ball.BlockHitted.RemoveListener (action);
		Destroy (Ball_FireEffect);
		var gameField = GameField.objRef;
		var blockList = gameField.GetBlocksInArea(ball.transform.position,Area,true);
		int blockCount = Mathf.RoundToInt(Mathf.Lerp (0,MaxBlocks, (float)blockList.Count/BlocksInAreaToMax));
		blockCount = Mathf.Min (blockCount, blockList.Count);
		if (blockList.Count > 1) {
			blocksToPutInFire = blockList.GetRange (0, blockCount);
			Started = true;
		}
	}
	void Update()
	{		
		if (Started) {			
			if (blocksToPutInFire.Count > 0) {
				blocksToPutInFire.RemoveAll (x => x == null);
				Counter += TimeManager.GetDeltaTime (TimeType.ScaledTime);
				if (Counter > currentExtendDelay) {
					Counter = 0f;
					currentExtendDelay = Random.Range (fireExtendDelay / 2f, fireExtendDelay);
					PutBlockInFire (blocksToPutInFire [Random.Range(0,Mathf.Min(2,blocksToPutInFire.Count-1))], Random.Range (fireAnimationDelay / 2f, fireAnimationDelay));
				}
			}
			for (int i = 0; i < blocksInFire.Count; i++) {
				blocksInFire [i].TimeToHit += -TimeManager.GetDeltaTime (TimeType.ScaledTime);
				if (blocksInFire [i].TimeToHit <= 0 || blocksInFire [i].block == null) {
					blocksInFire [i].Hit (damage);
					blocksInFire.RemoveAt (i);
				}
			}
			if (blocksInFire.Count == 0 && blocksToPutInFire.Count == 0)//если больше нечего делать и огонь уже начался
			Destroy (this.gameObject);
		}
	}
}
