using System;
using BepInEx.Configuration;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;

namespace FM26PlayerExport;

public static class PluginConfig
{
	public const int DefaultMaxRowsToExport = 5000;

	public const int SafeHardMaxRowsToExport = 10000;

	private const int DefaultMaxScrollAttempts = 500;

	private const int SafeHardMaxScrollAttempts = 20000;

	public const int DefaultScrollStepDelayFrames = 18;

	public static ConfigEntry<int> MaxRowsToExport;

	public static ConfigEntry<int> ScrollStepDelayFrames;

	public static int EffectiveMaxRowsToExport { get; private set; } = 5000;


	public static int EffectiveScrollStepDelayFrames { get; private set; } = 18;


	public static void Init(ConfigFile config, ManualLogSource log)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected O, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Expected O, but got Unknown
		MaxRowsToExport = config.Bind<int>("Export", "MaxRowsToExport", 5000, new ConfigDescription("Maximum number of selected rows exported by list handlers. FM26 player lists currently present only the first 10000 rows in the UI, so higher values are clamped for safety.", (AcceptableValueBase)new AcceptableValueRange<int>(1, 10000), Array.Empty<object>()));
		ScrollStepDelayFrames = config.Bind<int>("Export", "ScrollStepDelayFrames", 18, new ConfigDescription("Frames to wait after each scroll step before reading rows. Higher = slower but more stable export (fewer crashes on long lists); the game runs ~60 fps, so 18 frames is ~0.3 s per step.", (AcceptableValueBase)new AcceptableValueRange<int>(1, 120), Array.Empty<object>()));
		EffectiveScrollStepDelayFrames = Math.Max(1, Math.Min(ScrollStepDelayFrames.Value, 120));
		if (log != null)
		{
			log.LogInfo((object)("[FM26Export.CONFIG] ScrollStepDelayFrames=" + EffectiveScrollStepDelayFrames + "."));
		}
		Refresh(log);
	}

	public static int GetMaxScrollAttemptsForRows(int maxRows)
	{
		int num = maxRows / 2 + 300;
		if (num < 500)
		{
			return 500;
		}
		if (num > 20000)
		{
			return 20000;
		}
		return num;
	}

	private static void Refresh(ManualLogSource log)
	{
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0096: Expected O, but got Unknown
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected O, but got Unknown
		int num = ((MaxRowsToExport != null) ? MaxRowsToExport.Value : 5000);
		int num2 = Math.Max(1, Math.Min(num, 10000));
		bool flag = false;
		ManualLogSource val;
		if (MaxRowsToExport != null && num != num2)
		{
			MaxRowsToExport.Value = num2;
			val = log;
			if (val != null)
			{
				BepInExWarningLogInterpolatedStringHandler val2 = new BepInExWarningLogInterpolatedStringHandler(77, 1, out flag);
				if (flag)
				{
					((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("[FM26Export.CONFIG] MaxRowsToExport fora do intervalo seguro; ajustado para ");
					((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<int>(num2);
					((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(".");
				}
				val.LogWarning(val2);
			}
		}
		EffectiveMaxRowsToExport = num2;
		val = log;
		if (val != null)
		{
			BepInExInfoLogInterpolatedStringHandler val3 = new BepInExInfoLogInterpolatedStringHandler(37, 1, out flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val3).AppendLiteral("[FM26Export.CONFIG] MaxRowsToExport=");
				((BepInExLogInterpolatedStringHandler)val3).AppendFormatted<int>(EffectiveMaxRowsToExport);
				((BepInExLogInterpolatedStringHandler)val3).AppendLiteral(".");
			}
			val.LogInfo(val3);
		}
	}
}
