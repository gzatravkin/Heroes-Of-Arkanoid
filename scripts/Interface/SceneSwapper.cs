using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneSwapper : MonoBehaviour {
	public SceneField Scene;
	public bool Animated;
	public int AnimationCloseIndex;
	public int AnimationOpenIndex;
	void Start()
	{		
		var btn = GetComponent<Button> ();
		if (btn == null)
			btn = gameObject.AddComponent<Button> ();
		for (int i = 0; i < SceneManager.sceneCount; i++)
			if (SceneManager.GetSceneAt (i).name == Scene.SceneName) {		
				btn.interactable = false;
			}
		btn.onClick.AddListener (() => Swap ());
	}
	void Swap()
	{
		if (Animated) 
			MySceneManager.LoadSceneAnimated (Scene.SceneName, AnimationCloseIndex, AnimationOpenIndex);
		 else
			SceneManager.LoadScene (Scene.SceneName);	
	}
}
