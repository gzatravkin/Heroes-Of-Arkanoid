using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saves
{
	public static class SaveSystem
	{
		private static SaveWriter data;
		private static string oriPath;
		private static readonly string fileName = "SaveData.uml";
		private static PlayerData playerData;
		private static GameStatisticData gameStatisticData;
		public static PlayerData GetPlayerData()
		{
			return playerData;
		}
		public static CharactaerData GetCurrentCharacterData()
		{
			return playerData.Characters [GetCurrentCharacterIndex ()];
		}
		public static SO_Hero GetCurrentHero()
		{
			return SO_Configuraciones.obj.Heroes [GetCurrentCharacterData ().claseIndex];
		}
		public static int GetCurrentCharacterIndex()
		{
			return playerData.CurrentHero;
		}
		public static GameStatisticData GetGameStatistic()
		{
			return gameStatisticData;
		}
		public static void Save ()
		{
			data ["PlayerData"] = playerData;
			data ["GameStatisticData"] = gameStatisticData;
			data.Save ();
		}
		//Загрузка только при запуске приложения, все остальное навечно сохраняется
		private static void Load ()
		{		
			data = SaveWriter.Load (oriPath);
			if (!data.TryGetValue ("PlayerData", out playerData))
				playerData = new PlayerData ();
			if (!data.TryGetValue ("GameStatisticData", out gameStatisticData))
				gameStatisticData = new GameStatisticData ();
		}
			
		[RuntimeInitializeOnLoadMethod]
		static void Initialization ()
		{			
			data = new SaveWriter (fileName);			
			if (Application.platform == RuntimePlatform.Android)
				oriPath = System.IO.Path.Combine (Application.persistentDataPath, fileName);
			else
				oriPath = System.IO.Path.Combine (Application.streamingAssetsPath, fileName);				
			if (System.IO.File.Exists (oriPath)) {
				Load ();
			} else {				
				SavesInitialization ();
				Save ();
			}
			SaveValidation ();
		}
		private static void SaveValidation()
		{
			for (int i = 0; i < SO_Configuraciones.obj.Heroes.Length; i++) {
				if (playerData.Characters.Find(x=>x.claseIndex==i)==null)
					playerData.AddCharacter (i);
			}
			if (playerData.CurrentHero > playerData.Characters.Count)
				playerData.CurrentHero = 0;
		}
		private static void SavesInitialization()
		{
			playerData = new PlayerData ();
			for (int i = 0; i < SO_Configuraciones.obj.Heroes.Length; i++) {
				playerData.AddCharacter (i);
			}
			gameStatisticData = new GameStatisticData ();
			SceneManager.LoadScene ("CharacterFirstTime");
		}
	}
		
}