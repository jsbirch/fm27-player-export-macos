using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using SI.Bindable;
using UnityEngine.UIElements;

namespace FM26PlayerExport.Handlers;

public static class UIUtils
{
	public static string GameLang = "pt";

	public static string GetTrans(string key)
	{
		if (GameLang == "en")
		{
			switch (key)
			{
			case "Amarelo":
				return "Yellow";
			case "Vermelho":
				return "Red";
			case "Sub In":
				return "Sub In";
			case "Sub Out":
				return "Sub Out";
			case "Lesão":
				return "Injured";
			}
		}
		else if (GameLang == "es")
		{
			switch (key)
			{
			case "Amarelo":
				return "Amarilla";
			case "Vermelho":
				return "Roja";
			case "Sub In":
				return "Entra";
			case "Sub Out":
				return "Sale";
			case "Lesão":
				return "Lesión";
			}
		}
		else
		{
			if (key == "Sub In")
			{
				return "Entra";
			}
			if (key == "Sub Out")
			{
				return "Sai";
			}
		}
		return key;
	}

	public static string GetText(VisualElement el, bool allowRenderedTextFallback = true, bool allowTooltipFallback = true)
	{
		if (el == null)
		{
			return null;
		}
		try
		{
			TextElement val = ((Il2CppObjectBase)el).TryCast<TextElement>();
			if (val != null && !string.IsNullOrWhiteSpace(val.text))
			{
				return StripHtml(val.text.Trim());
			}
		}
		catch
		{
		}
		try
		{
			SIText val2 = ((Il2CppObjectBase)el).TryCast<SIText>();
			if (val2 != null && !string.IsNullOrWhiteSpace(((TextElement)val2).text))
			{
				return StripHtml(((TextElement)val2).text.Trim());
			}
		}
		catch
		{
		}
		if (allowRenderedTextFallback)
		{
			try
			{
				TextElement val3 = ((Il2CppObjectBase)el).TryCast<TextElement>();
				if (val3 != null && !string.IsNullOrWhiteSpace(val3.m_RenderedText))
				{
					return StripHtml(val3.m_RenderedText.Trim());
				}
			}
			catch
			{
			}
		}
		try
		{
			Label val4 = ((Il2CppObjectBase)el).TryCast<Label>();
			if (val4 != null && !string.IsNullOrWhiteSpace(((TextElement)val4).text))
			{
				return StripHtml(((TextElement)val4).text.Trim());
			}
		}
		catch
		{
		}
		if (allowTooltipFallback)
		{
			try
			{
				string tooltip = el.tooltip;
				if (!string.IsNullOrWhiteSpace(tooltip))
				{
					return StripHtml(tooltip.Trim());
				}
			}
			catch
			{
			}
		}
		return null;
	}

	public static string StripHtml(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			return Regex.Replace(s, "<[^>]+>", string.Empty).Trim();
		}
		return s;
	}

	public static string CollectFirstText(VisualElement el, int d = 0, bool allowRenderedTextFallback = true, bool allowTooltipFallback = true)
	{
		if (el == null || d > 20)
		{
			return null;
		}
		string text = GetText(el, allowRenderedTextFallback, allowTooltipFallback);
		if (text != null)
		{
			return text;
		}
		for (int i = 0; i < el.childCount; i++)
		{
			string text2 = CollectFirstText(el.ElementAt(i), d + 1, allowRenderedTextFallback, allowTooltipFallback);
			if (text2 != null)
			{
				return text2;
			}
		}
		return null;
	}

	public static string CollectFirstTooltip(VisualElement el, int d = 0)
	{
		if (el == null || d > 20)
		{
			return null;
		}
		try
		{
			string tooltip = el.tooltip;
			if (!string.IsNullOrWhiteSpace(tooltip))
			{
				return StripHtml(tooltip.Trim());
			}
		}
		catch
		{
		}
		for (int i = 0; i < el.childCount; i++)
		{
			string text = CollectFirstTooltip(el.ElementAt(i), d + 1);
			if (text != null)
			{
				return text;
			}
		}
		return null;
	}

	public static string CollectAllTextsJoined(VisualElement el, int d = 0, bool allowRenderedTextFallback = true, bool allowTooltipFallback = true)
	{
		if (el == null || d > 20)
		{
			return "";
		}
		List<string> list = new List<string>();
		CollectAllTexts(el, list, 0, allowRenderedTextFallback, allowTooltipFallback);
		return string.Join(" ", list).Trim();
	}

	public static void CollectAllTexts(VisualElement el, List<string> out_, int d = 0, bool allowRenderedTextFallback = true, bool allowTooltipFallback = true)
	{
		if (el == null || d > 20)
		{
			return;
		}
		string text = GetText(el, allowRenderedTextFallback, allowTooltipFallback);
		if (text != null && !out_.Contains(text))
		{
			out_.Add(text);
		}
		if (allowTooltipFallback)
		{
			try
			{
				string tooltip = el.tooltip;
				if (!string.IsNullOrWhiteSpace(tooltip))
				{
					string item = StripHtml(tooltip.Trim());
					if (!out_.Contains(item))
					{
						out_.Add(item);
					}
				}
			}
			catch
			{
			}
		}
		for (int i = 0; i < el.childCount; i++)
		{
			CollectAllTexts(el.ElementAt(i), out_, d + 1, allowRenderedTextFallback, allowTooltipFallback);
		}
	}

	public static StarRatingResult TryReadStarRating(VisualElement cell)
	{
		List<List<string>> list = new List<List<string>>();
		CollectStarClassLists(cell, cell, list, 0);
		if (list.Count == 0)
		{
			return null;
		}
		if (!StarRatingParser.TryParseRating(list, out var result))
		{
			Plugin.Log.LogWarning((object)("[FM26Export] Unrecognised star cell structure; exporting blank. Classes: " + string.Join(" | ", list.ConvertAll((List<string> classes) => string.Join(",", classes)))));
			return null;
		}
		return result;
	}

	public static string FormatStarRating(float rating, bool blankWhenZero)
	{
		if (blankWhenZero && rating <= 0f)
		{
			return string.Empty;
		}
		return rating.ToString("0.#", CultureInfo.InvariantCulture).Replace(".", ",");
	}

	private static bool IsVisibleForStarRead(VisualElement element, VisualElement cell)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Invalid comparison between Unknown and I4
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Invalid comparison between Unknown and I4
		for (VisualElement val = element; val != null; val = val.parent)
		{
			try
			{
				if ((int)val.resolvedStyle.display == 1 || (int)val.resolvedStyle.visibility == 1 || val.resolvedStyle.opacity <= 0f)
				{
					return false;
				}
			}
			catch
			{
			}
			if (val == cell)
			{
				break;
			}
		}
		return true;
	}

	private static void CollectStarClassLists(VisualElement element, VisualElement cell, List<List<string>> starClassLists, int depth)
	{
		if (element == null || depth > 12 || !IsVisibleForStarRead(element, cell))
		{
			return;
		}
		try
		{
			List<string> list = new List<string>();
			for (int i = 0; i < element.classList.Count; i++)
			{
				list.Add(element.classList[i]);
			}
			if (element.childCount == 0 && StarRatingParser.Classify(list) != 0)
			{
				starClassLists.Add(list);
			}
		}
		catch
		{
		}
		for (int j = 0; j < element.childCount; j++)
		{
			CollectStarClassLists(element.ElementAt(j), cell, starClassLists, depth + 1);
		}
	}

	public static string RowKey(List<string> vals)
	{
		if (vals == null || vals.Count == 0)
		{
			return string.Empty;
		}
		return string.Join("|", vals);
	}

	public static VisualElement FindByName(VisualElement el, string name)
	{
		if (el == null)
		{
			return null;
		}
		if (el.name == name)
		{
			return el;
		}
		for (int i = 0; i < el.childCount; i++)
		{
			VisualElement val = FindByName(el.ElementAt(i), name);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	public static string Esc(string v)
	{
		if (string.IsNullOrEmpty(v))
		{
			return string.Empty;
		}
		v = v.Replace("\r", " ").Replace("\n", " ");
		string text = new string(new char[1] { '"' });
		if (v.Contains(";") || v.Contains(text))
		{
			v = text + v.Replace(text, text + text) + text;
		}
		return v;
	}

	public static string LerIconesComoTexto(VisualElement el, int d = 0)
	{
		if (el == null || d > 6)
		{
			return "";
		}
		List<string> list = new List<string>();
		try
		{
			for (int i = 0; i < el.classList.Count; i++)
			{
				string text = el.classList[i].ToLower();
				if (text.Contains("yellow"))
				{
					list.Add(GetTrans("Amarelo"));
				}
				if (text.Contains("red"))
				{
					list.Add(GetTrans("Vermelho"));
				}
				if (text.Contains("sub") && !text.Contains("subject"))
				{
					if (text.Contains("on") || text.Contains("in"))
					{
						list.Add(GetTrans("Sub In"));
					}
					else if (text.Contains("off") || text.Contains("out"))
					{
						list.Add(GetTrans("Sub Out"));
					}
					else
					{
						list.Add("Sub");
					}
				}
				if (text.Contains("injur"))
				{
					list.Add(GetTrans("Lesão"));
				}
				if (text.Contains("condition") || text.Contains("heart") || text.Contains("sharpness"))
				{
					list.Add("Coração");
				}
				if (text.Contains("fatigue") || text.Contains("tired"))
				{
					list.Add("Fadigado");
				}
			}
			string tooltip = el.tooltip;
			if (!string.IsNullOrWhiteSpace(tooltip) && list.Count > 0)
			{
				string text2 = StripHtml(tooltip);
				if (text2.Length < 40)
				{
					string text3 = list[list.Count - 1];
					if (text3 != GetTrans("Sub In") && text3 != GetTrans("Sub Out"))
					{
						list[list.Count - 1] = text2;
					}
				}
			}
		}
		catch
		{
		}
		for (int j = 0; j < el.childCount; j++)
		{
			string text4 = LerIconesComoTexto(el.ElementAt(j), d + 1);
			if (string.IsNullOrEmpty(text4))
			{
				continue;
			}
			string[] array = text4.Split(new string[1] { " | " }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string item in array)
			{
				if (!list.Contains(item))
				{
					list.Add(item);
				}
			}
		}
		return string.Join(" | ", list);
	}

	public static string RealTypeName(VisualElement el)
	{
		try
		{
			IntPtr intPtr = IL2CPP.il2cpp_object_get_class(((Il2CppObjectBase)el).Pointer);
			if (intPtr == IntPtr.Zero)
			{
				return "?";
			}
			string text = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_namespace(intPtr)) ?? "";
			string text2 = Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(intPtr)) ?? "?";
			return string.IsNullOrEmpty(text) ? text2 : (text + "." + text2);
		}
		catch
		{
			return "?";
		}
	}

	public static string ProbeText(VisualElement el)
	{
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			SIText val = ((Il2CppObjectBase)el).TryCast<SIText>();
			if (val == null)
			{
				stringBuilder.Append("cast=NULL ");
			}
			else
			{
				stringBuilder.Append("cast=OK ");
				try
				{
					stringBuilder.Append("text='" + (((TextElement)val).text ?? "<null>") + "' ");
				}
				catch (Exception ex)
				{
					stringBuilder.Append("text!EX:" + ex.GetType().Name + ":" + ex.Message + " ");
				}
				try
				{
					stringBuilder.Append("finalSet=" + val.m_finalTextSet + " ");
				}
				catch (Exception ex2)
				{
					stringBuilder.Append("finalSet!EX:" + ex2.GetType().Name + " ");
				}
			}
		}
		catch (Exception ex3)
		{
			stringBuilder.Append("cast!EX:" + ex3.GetType().Name + ":" + ex3.Message + " ");
		}
		try
		{
			TextElement val2 = ((Il2CppObjectBase)el).TryCast<TextElement>();
			if (val2 == null)
			{
				stringBuilder.Append("te=NULL ");
			}
			else
			{
				try
				{
					stringBuilder.Append("m_Text='" + (val2.m_Text ?? "<null>") + "' ");
				}
				catch (Exception ex4)
				{
					stringBuilder.Append("m_Text!EX:" + ex4.GetType().Name + " ");
				}
				try
				{
					stringBuilder.Append("rendered='" + (val2.m_RenderedText ?? "<null>") + "' ");
				}
				catch (Exception ex5)
				{
					stringBuilder.Append("rendered!EX:" + ex5.GetType().Name + " ");
				}
				try
				{
					stringBuilder.Append("elided='" + (val2.elidedText ?? "<null>") + "' ");
				}
				catch (Exception ex6)
				{
					stringBuilder.Append("elided!EX:" + ex6.GetType().Name + " ");
				}
			}
		}
		catch (Exception ex7)
		{
			stringBuilder.Append("te!EX:" + ex7.GetType().Name + " ");
		}
		return stringBuilder.ToString();
	}

	public static VisualElement FindLeaf(VisualElement el)
	{
		while (el != null && el.childCount > 0)
		{
			el = el.ElementAt(el.childCount - 1);
		}
		return el;
	}

	public static string DiagCell(VisualElement el, int d = 0)
	{
		if (el == null || d > 12)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		try
		{
			for (int i = 0; i < el.classList.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append(el.classList[i]);
			}
		}
		catch
		{
		}
		string text = GetText(el) ?? string.Empty;
		string text2 = string.Empty;
		try
		{
			text2 = el.name ?? string.Empty;
		}
		catch
		{
		}
		StringBuilder stringBuilder2 = new StringBuilder();
		stringBuilder2.Append($"{new string('-', d)}{RealTypeName(el)}[name={text2},cls={stringBuilder},ch={el.childCount},txt={text}] ");
		for (int j = 0; j < el.childCount; j++)
		{
			stringBuilder2.Append(DiagCell(el.ElementAt(j), d + 1));
		}
		return stringBuilder2.ToString();
	}
}
