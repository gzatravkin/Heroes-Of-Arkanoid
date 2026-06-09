using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class MySceneManager : MonoBehaviour {	

	private static IEnumerator LoadSceneWithCallback(GameObject LoadManager, string SceneName, UnityAction callBack)
	{ 	
		DontDestroyOnLoad (LoadManager);	
		SceneManager.LoadScene (SceneName);
		yield return new WaitForSecondsRealtime (0.01f);
		callBack.Invoke ();
		Destroy (LoadManager);
	}
	private static IEnumerator LoadSceneAnimated_Corrunte(GameObject LoadManager, string SceneName, int AnimationClose,int AnimationOpen, UnityAction callBack)
	{ 
		DontDestroyOnLoad (LoadManager);
		var Operation = SceneManager.LoadSceneAsync (SceneName);
		Operation.allowSceneActivation = false;
		if (AnimationClose != -1) {
			var animator = SO_Configuraciones.obj.FadeScreenAnimations [AnimationClose];
			var GO = (GameObject)Instantiate (animator.gameObject);
			animator = GO.GetComponent<Animator> ();
			animator.SetTrigger ("Close");
			yield return new WaitForSecondsRealtime (animator.GetCurrentAnimatorStateInfo (0).length);
		}
		Operation.allowSceneActivation = true;
		yield return Operation;
		if (callBack!=null)
			callBack.Invoke ();
		if (AnimationOpen != -1) {
			var animator = SO_Configuraciones.obj.FadeScreenAnimations [AnimationOpen];
			var GO = (GameObject)Instantiate (animator.gameObject);
			animator = GO.GetComponent<Animator> ();
			animator.SetTrigger ("Open");
			yield return new WaitForSecondsRealtime (animator.GetCurrentAnimatorStateInfo (0).length);
			Destroy (GO);
		}
		Destroy (LoadManager);
	}
	public static void LoadSceneAnimated(string SceneName, int AnimationClose,int AnimationOpen, UnityAction callBack = null)
	{		
		GameObject temp = new GameObject ("LoadManager");
		var animatedSceneManager = temp.AddComponent <MySceneManager> ();
		animatedSceneManager.StartCoroutine(LoadSceneAnimated_Corrunte(animatedSceneManager.gameObject, SceneName,AnimationClose,AnimationOpen,callBack));
	}
	public static void LoadScene_WithCallback(string SceneName, UnityAction callBack = null)
	{		
		GameObject temp = new GameObject ("LoadManager");
		var animatedSceneManager = temp.AddComponent <MySceneManager> ();
		animatedSceneManager.StartCoroutine(LoadSceneWithCallback(animatedSceneManager.gameObject, SceneName,callBack));
	}
}
