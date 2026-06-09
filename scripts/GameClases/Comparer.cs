using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Comparer {

	public static int floatSortFunction(float a, float b)
	{
		if (a > b)
			return 1;
		if (a < b)
			return -1;
		return 0;
	}
}
