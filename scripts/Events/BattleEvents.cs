using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine;
public class BattleEvents  {
	public UnityEvent SpellCasted = new UnityEvent();
	public UnityEvent UpdateEvent = new UnityEvent(); //Invoked from BattleEventsManager
	public UnityEvent BattleStarted = new UnityEvent();
	public UnityEvent BattleEnded = new UnityEvent(); 
	public UnityEvent BattleWinned = new UnityEvent(); 
	public UnityEvent BattleLoosed = new UnityEvent(); 

	public UnityEvent_Block BlockDestroyed = new UnityEvent_Block();//MayExist, but Killed

	public UnityEvent_Float HpLoosed = new UnityEvent_Float();
	public UnityEvent_Float MpLoosed = new UnityEvent_Float();

	public UnityEvent TryLoosed = new UnityEvent(); 

	public UnityEvent BallDropped = new UnityEvent(); 
	public UnityEvent_Block BallBlockCollision = new UnityEvent_Block(); 
	public UnityEvent_Ball BallAdded = new UnityEvent_Ball();
	public UnityEvent_Ball_BarCollision Ball_BarCollision = new UnityEvent_Ball_BarCollision();
	public UnityEvent_Position Ball_LeftWallCollision = new UnityEvent_Position();
	public UnityEvent_Position Ball_TopWallCollision = new UnityEvent_Position();
	public UnityEvent_Position Ball_RightWallCollision = new UnityEvent_Position();

	public UnityEvent GameActivated = new UnityEvent(); 
	public UnityEvent GameStoped = new UnityEvent(); 
	public UnityEvent GamePaused = new UnityEvent(); 
	public UnityEvent GameUnpaused = new UnityEvent(); 


}
