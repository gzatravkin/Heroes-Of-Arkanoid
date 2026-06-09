using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
public static class DefaultSettings {	
	public static int DefaultLang=0;
	#if UNITY_EDITOR
	[MenuItem("GameSettings/Editor view/Language/Русский")]
	static void SetDefaultRu()
	{
		DefaultLang = 0;
	}
	[MenuItem("GameSettings/Editor view/Language/Английский")]
	static void SetDefaultEng()
	{
		DefaultLang = 1;
	}
	[MenuItem("GameSettings/Editor view/Language/Испанский")]
	static void SetDefaultEsp()
	{
		DefaultLang = 2;
	}
	#endif
}
