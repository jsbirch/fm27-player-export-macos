using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace FM26PlayerExport;

[BepInPlugin("com.koda.fm26.playerexport", "FM27 Player Export + Profile Bridge", "5.2.0")]
public class Plugin : BasePlugin
{
	internal static ManualLogSource Log;

	private static ExportDriver _driver;

	public override void Load()
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected O, but got Unknown
		Log = ((BasePlugin)this).Log;
		Log.LogInfo((object)"[FM27Export v5.2.0] Loaded. F9 or Ctrl+P: table export. FM Profile Bridge: direct profile text.");
		PluginConfig.Init(((BasePlugin)this).Config, Log);
		_driver = new ExportDriver();
		try
		{
			ClassInjector.RegisterTypeInIl2Cpp<ExportBehaviour>();
			GameObject val = new GameObject("FM27PlayerExport")
			{
				hideFlags = (HideFlags)61
			};
			UnityEngine.Object.DontDestroyOnLoad((UnityEngine.Object)val);
			val.AddComponent<ExportBehaviour>();
			Log.LogInfo((object)"[FM26Export] ExportBehaviour injected; driving export on Update loop.");
		}
		catch (Exception ex)
		{
			Log.LogError((object)("[FM26Export] Failed to inject ExportBehaviour: " + ex.Message));
		}
	}

	internal static void Tick()
	{
		if (_driver != null) ProfileRequestBridge.Tick(_driver);
		_driver?.Tick();
	}

	public override bool Unload()
	{
		_driver = null;
		return false;
	}
}
