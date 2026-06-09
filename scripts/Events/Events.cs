using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class UnityEvent_Block : UnityEvent<BlockScript>
{
}

[System.Serializable]
public class UnityEvent_Float : UnityEvent<float>
{
}
[System.Serializable]
public class UnityEvent_Int : UnityEvent<int>
{
}

[System.Serializable]
public class UnityEvent_Position : UnityEvent<Vector2>
{
}
[System.Serializable]
public class UnityEvent_Ball : UnityEvent<BallController>
{
}
[System.Serializable]
public class UnityEvent_Ball_BarCollision : UnityEvent<BallController,float>
{
}
