using System;
using UnityEngine;

namespace FM26PlayerExport;

public class ExportBehaviour : MonoBehaviour
{
	public ExportBehaviour(IntPtr ptr)
		: base(ptr)
	{
	}

	private void Update()
	{
		try
		{
			Plugin.Tick();
		}
		catch
		{
		}
	}
}
