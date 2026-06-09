using UnityEngine;
[System.Serializable]
public class OnFinEffect
{
	public Effect onFinEffect;
	[System.Serializable]
	public enum Effect
	{
		DestroyObject,DestroyScript,DisableScript
	}
	public void Finish(MonoBehaviour script)
	{
		if (onFinEffect == Effect.DestroyObject)
			MonoBehaviour.Destroy (script.gameObject);
		if (onFinEffect == Effect.DestroyScript)
			MonoBehaviour.Destroy (script);
		if (onFinEffect == Effect.DisableScript)
			script.enabled = false;
	}
}
