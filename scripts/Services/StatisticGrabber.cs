using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class StatisticGrabber{
	private static GameStatisticData gameStatiticData;
	[RuntimeInitializeOnLoadMethod]
	public static void Initialization()
	{		
		BattleEventsManager.OnEventsReload.AddListener(()=>AddBattleLisenters());
	}
	public static void AddBattleLisenters()
	{
		gameStatiticData = Saves.SaveSystem.GetGameStatistic ();
		BattleEventsManager.Events.TryLoosed.AddListener (() => {
			gameStatiticData.TryesLoosed++;
		});
		BattleEventsManager.Events.BlockDestroyed.AddListener ((arg0) => {
			gameStatiticData.BlockDestroyed++;
		});
		BattleEventsManager.Events.UpdateEvent.AddListener (() => {
			gameStatiticData.timeInGame+=TimeManager.deltaTime;
		});
		BattleEventsManager.Events.BattleLoosed.AddListener (() => {
			gameStatiticData.BattlesLoosed++;
		});
		BattleEventsManager.Events.BattleWinned.AddListener (() => {
			gameStatiticData.BattlesWinned++;
		});
		BattleEventsManager.Events.BattleStarted.AddListener (() => {
			gameStatiticData.BattlesStarted++;
		});
		BattleEventsManager.Events.SpellCasted.AddListener (() => {
			gameStatiticData.SpellCasted++;
		});
		BattleEventsManager.Events.MpLoosed.AddListener ((arg0) => {
			gameStatiticData.MpLoosed+=arg0;
		});
		BattleEventsManager.Events.HpLoosed.AddListener ((arg0) => {
			gameStatiticData.HpLoosed+=arg0;
		});
	}
}
