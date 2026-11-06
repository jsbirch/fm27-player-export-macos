using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM26PlayerExport.Handlers;

public abstract class GenericScrolledTableHandler : IExportHandler
{
	private const int WAIT_FRAMES = 4;

	private const int ZERO_STEPS_MAX = 3;

	private const int ERROR_RETRIES_MAX = 8;

	private int _errorRetries;

	protected VisualElement _captureView;

	protected List<string> _captureHeaders;

	private List<string> _captureRowHeaders;

	private HashSet<string> _seenKeys;

	private float _lastScrollY;

	private int _scrollAttempts;

	private int _maxScrollAttempts;

	private int _maxRows;

	private int _capturedRowCount;

	private int _zeroSteps;

	private int _endSteps;

	private bool _diagLogged;

	private int _captureWait;

	private bool _isComplete;

	private bool _outputStarted;

	private bool _outputFinalized;

	private StreamWriter _csvWriter;

	private StreamWriter _htmlWriter;

	private string _csvFile;

	private string _htmlFile;

	private static VisualElement _cellRowBest;

	private static int _cellRowBestCount;

	protected string FilePrefix { get; set; } = "export_";


	public virtual bool TryStartCapture(VisualElement root, out string errorMessage)
	{
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Expected O, but got Unknown
		//IL_02f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fc: Expected O, but got Unknown
		//IL_0406: Unknown result type (might be due to invalid IL or missing references)
		//IL_040d: Expected O, but got Unknown
		//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
		errorMessage = string.Empty;
		VisualElement val = null;
		VisualElement val2 = null;
		VisualElement val3 = UIUtils.FindByName(root, "playertable") ?? UIUtils.FindByName(root, "client-object-viewer-table");
		if (val3 != null)
		{
			val = UIUtils.FindByName(val3, "column-headers");
			val2 = UIUtils.FindByName(val3, "View");
		}
		if (val == null || val2 == null)
		{
			List<VisualElement> list = new List<VisualElement>();
			FindAllByName(root, "column-headers", list);
			foreach (VisualElement item in list)
			{
				object obj = UIUtils.FindByName(item.parent, "View");
				if (obj == null)
				{
					VisualElement parent = item.parent;
					obj = ((((parent != null) ? parent.parent : null) != null) ? UIUtils.FindByName(item.parent.parent, "View") : null);
					if (obj == null)
					{
						VisualElement parent2 = item.parent;
						object obj2;
						if (parent2 == null)
						{
							obj2 = null;
						}
						else
						{
							VisualElement parent3 = parent2.parent;
							obj2 = ((parent3 != null) ? parent3.parent : null);
						}
						obj = ((obj2 != null) ? UIUtils.FindByName(item.parent.parent.parent, "View") : null);
					}
				}
				VisualElement val4 = (VisualElement)obj;
				if (val4 != null && val4.childCount > 0)
				{
					val = item;
					val2 = val4;
					break;
				}
			}
		}
		if (val == null || val2 == null)
		{
			errorMessage = "[FM26Export.GenericTable] Nenhuma tabela de lista ('playertable' ou generica) encontrada na UI.";
			return false;
		}
		_captureView = val2;
		_captureHeaders = new List<string>();
		for (int i = 1; i < val.childCount; i++)
		{
			VisualElement el = val.ElementAt(i);
			List<string> list2 = new List<string>();
			UIUtils.CollectAllTexts(el, list2);
			string text = string.Empty;
			foreach (string item2 in list2)
			{
				string text2 = item2.ToLowerInvariant();
				if (!text2.Contains("sort") && !text2.Contains("orden") && !text2.Contains("order") && item2.Length > text.Length)
				{
					text = item2;
				}
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				string text3 = UIUtils.CollectFirstTooltip(el);
				string text4 = UIUtils.CollectFirstText(el);
				text = ((!string.IsNullOrWhiteSpace(text3)) ? text3 : text4);
			}
			_captureHeaders.Add((text != null) ? UIUtils.Esc(text) : $"Col{i}");
		}
		if (_captureHeaders.Count == 0)
		{
			_captureHeaders.Add("Dados");
		}
		if (!IsValidScreen(root, _captureHeaders))
		{
			errorMessage = "[FM26Export] Tabela encontrada, mas rejeitada pelo handler generico/filho. Headers: " + string.Join(", ", _captureHeaders);
			return false;
		}
		_captureRowHeaders = new List<string>(_captureHeaders);
		_captureHeaders = BuildExportHeaders(_captureRowHeaders);
		ManualLogSource log = Plugin.Log;
		bool flag = false;
		BepInExInfoLogInterpolatedStringHandler val5 = new BepInExInfoLogInterpolatedStringHandler(25, 2, out flag);
		if (flag)
		{
			((BepInExLogInterpolatedStringHandler)val5).AppendLiteral("[FM26Export] Headers (");
			((BepInExLogInterpolatedStringHandler)val5).AppendFormatted<int>(_captureHeaders.Count);
			((BepInExLogInterpolatedStringHandler)val5).AppendLiteral("): ");
			((BepInExLogInterpolatedStringHandler)val5).AppendFormatted<string>(string.Join(" | ", _captureHeaders));
		}
		log.LogInfo(val5);
		_seenKeys = new HashSet<string>();
		_scrollAttempts = 0;
		_maxRows = PluginConfig.EffectiveMaxRowsToExport;
		_maxScrollAttempts = PluginConfig.GetMaxScrollAttemptsForRows(_maxRows);
		_capturedRowCount = 0;
		_zeroSteps = 0;
		_endSteps = 0;
		_lastScrollY = -1f;
		_diagLogged = false;
		_errorRetries = 0;
		_isComplete = false;
		_outputStarted = false;
		_outputFinalized = false;
		_csvWriter = null;
		_htmlWriter = null;
		_csvFile = null;
		_htmlFile = null;
		ScrollView firstAncestorOfType = _captureView.GetFirstAncestorOfType<ScrollView>();
		if (firstAncestorOfType != null)
		{
			firstAncestorOfType.scrollOffset = Vector2.zero;
		}
		_captureWait = PluginConfig.EffectiveScrollStepDelayFrames;
		ManualLogSource log2 = Plugin.Log;
		val5 = new BepInExInfoLogInterpolatedStringHandler(75, 3, out flag);
		if (flag)
		{
			((BepInExLogInterpolatedStringHandler)val5).AppendLiteral("[FM26Export] Captura de lista (");
			((BepInExLogInterpolatedStringHandler)val5).AppendFormatted<string>(FilePrefix);
			((BepInExLogInterpolatedStringHandler)val5).AppendLiteral(") iniciada. Limite=");
			((BepInExLogInterpolatedStringHandler)val5).AppendFormatted<int>(_maxRows);
			((BepInExLogInterpolatedStringHandler)val5).AppendLiteral(" linhas | scroll safety=");
			((BepInExLogInterpolatedStringHandler)val5).AppendFormatted<int>(_maxScrollAttempts);
			((BepInExLogInterpolatedStringHandler)val5).AppendLiteral(".");
		}
		log2.LogInfo(val5);
		return true;
	}

	protected abstract bool IsValidScreen(VisualElement root, List<string> headers);

	public bool CaptureStep()
	{
		//IL_0477: Unknown result type (might be due to invalid IL or missing references)
		//IL_047e: Expected O, but got Unknown
		//IL_017b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Expected O, but got Unknown
		//IL_0345: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Unknown result type (might be due to invalid IL or missing references)
		//IL_035b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0360: Unknown result type (might be due to invalid IL or missing references)
		//IL_0395: Unknown result type (might be due to invalid IL or missing references)
		if (_isComplete)
		{
			return true;
		}
		if (_captureWait > 0)
		{
			_captureWait--;
			return false;
		}
		bool flag = false;
		try
		{
			int num = 0;
			for (int i = 0; i < _captureView.childCount; i++)
			{
				VisualElement val = _captureView.ElementAt(i);
				bool flag2 = false;
				try
				{
					flag2 = val.ClassListContains("virtualised-list__item--selected");
				}
				catch
				{
				}
				if (!flag2)
				{
					continue;
				}
				bool flag3 = !_diagLogged && _scrollAttempts == 0 && num == 0;
				List<string> list = ReadRow(val, flag3, _captureRowHeaders);
				if (flag3)
				{
					_diagLogged = true;
				}
				if (list.Count == 0)
				{
					continue;
				}
				string text = UIUtils.RowKey(list);
				if (string.IsNullOrEmpty(text) || _seenKeys.Contains(text))
				{
					continue;
				}
				_seenKeys.Add(text);
				if (!WriteCapturedRow(list))
				{
					_isComplete = true;
					return true;
				}
				num++;
				if (_capturedRowCount >= _maxRows)
				{
					ManualLogSource log = Plugin.Log;
					BepInExWarningLogInterpolatedStringHandler val2 = new BepInExWarningLogInterpolatedStringHandler(52, 1, out flag);
					if (flag)
					{
						((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("[FM26Export] Limite configurado de ");
						((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<int>(_maxRows);
						((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(" linhas atingido.");
					}
					log.LogWarning(val2);
					_isComplete = true;
					return true;
				}
			}
			ScrollView firstAncestorOfType = _captureView.GetFirstAncestorOfType<ScrollView>();
			float num2 = ((firstAncestorOfType != null) ? firstAncestorOfType.scrollOffset.y : 0f);
			_scrollAttempts++;
			bool flag4 = Math.Abs(num2 - _lastScrollY) < 0.5f && _lastScrollY >= 0f;
			if (num == 0)
			{
				_zeroSteps++;
			}
			else
			{
				_zeroSteps = 0;
			}
			if (flag4 && num == 0)
			{
				_endSteps++;
			}
			else
			{
				_endSteps = 0;
			}
			bool flag5 = _zeroSteps >= 12;
			Plugin.Log.LogInfo((object)("[FM26Export] Step " + _scrollAttempts + ": +" + num + " | total=" + _capturedRowCount + " | scrollY=" + num2.ToString("F0") + " | pinned=" + flag4 + " | end=" + _endSteps + "/3 | stall=" + _zeroSteps + "/12"));
			if (_endSteps >= 3 || _scrollAttempts >= _maxScrollAttempts || flag5)
			{
				if (flag5 && _endSteps < 3)
				{
					Plugin.Log.LogWarning((object)"[FM26Export] Parado por falta de novos dados.");
				}
				if (_scrollAttempts >= _maxScrollAttempts)
				{
					Plugin.Log.LogWarning((object)("[FM26Export] Parado pelo limite de seguranca de scroll (" + _maxScrollAttempts + ")."));
				}
				_isComplete = true;
				return true;
			}
			_lastScrollY = num2;
			float num3;
			if (firstAncestorOfType != null)
			{
				Rect layout = ((VisualElement)firstAncestorOfType).layout;
				if (layout.height > 0f)
				{
					layout = ((VisualElement)firstAncestorOfType).layout;
					num3 = layout.height;
					goto IL_0374;
				}
			}
			num3 = 600f;
			goto IL_0374;
			IL_0374:
			float num4 = Math.Max(60f, num3 / 3f);
			if (firstAncestorOfType != null)
			{
				firstAncestorOfType.scrollOffset = new Vector2(0f, num2 + num4);
			}
			_errorRetries = 0;
			_captureWait = PluginConfig.EffectiveScrollStepDelayFrames;
			return false;
		}
		catch (Exception ex)
		{
			if (ex.Message != null && ex.Message.Contains("dirty repaint"))
			{
				_captureWait = PluginConfig.EffectiveScrollStepDelayFrames;
				return false;
			}
			_errorRetries++;
			if (_errorRetries <= 8)
			{
				Plugin.Log.LogWarning((object)("[FM26Export] Erro transitório no CaptureStep (tentativa " + _errorRetries + "/" + 8 + "): " + ex.Message + " — aguardando e tentando de novo."));
				_captureWait = PluginConfig.EffectiveScrollStepDelayFrames * 4;
				return false;
			}
			ManualLogSource log2 = Plugin.Log;
			BepInExErrorLogInterpolatedStringHandler val3 = new BepInExErrorLogInterpolatedStringHandler(31, 1, out flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val3).AppendLiteral("[FM26Export] Erro CaptureStep: ");
				((BepInExLogInterpolatedStringHandler)val3).AppendFormatted<string>(ex.Message);
			}
			log2.LogError(val3);
			_isComplete = true;
			return true;
		}
	}

	public void FinishCapture()
	{
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Expected O, but got Unknown
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Expected O, but got Unknown
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Expected O, but got Unknown
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d3: Expected O, but got Unknown
		bool flag = false;
		try
		{
			if (_capturedRowCount == 0)
			{
				CloseOutput(deleteFiles: true);
				ManualLogSource log = Plugin.Log;
				BepInExWarningLogInterpolatedStringHandler val = new BepInExWarningLogInterpolatedStringHandler(31, 1, out flag);
				if (flag)
				{
					((BepInExLogInterpolatedStringHandler)val).AppendLiteral("[FM26Export] Nenhum dado para ");
					((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(FilePrefix);
					((BepInExLogInterpolatedStringHandler)val).AppendLiteral(".");
				}
				log.LogWarning(val);
				return;
			}
			CloseOutput(deleteFiles: false);
			ManualLogSource log2 = Plugin.Log;
			BepInExInfoLogInterpolatedStringHandler val2 = new BepInExInfoLogInterpolatedStringHandler(28, 1, out flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("[FM26Export] OK ");
				((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<int>(_capturedRowCount);
				((BepInExLogInterpolatedStringHandler)val2).AppendLiteral(" exportados.");
			}
			log2.LogInfo(val2);
			ManualLogSource log3 = Plugin.Log;
			val2 = new BepInExInfoLogInterpolatedStringHandler(27, 1, out flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("[FM26Export] CSV salvo em: ");
				((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<string>(_csvFile);
			}
			log3.LogInfo(val2);
			ManualLogSource log4 = Plugin.Log;
			val2 = new BepInExInfoLogInterpolatedStringHandler(28, 1, out flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val2).AppendLiteral("[FM26Export] HTML salvo em: ");
				((BepInExLogInterpolatedStringHandler)val2).AppendFormatted<string>(_htmlFile);
			}
			log4.LogInfo(val2);
		}
		catch (Exception ex)
		{
			ManualLogSource log5 = Plugin.Log;
			BepInExErrorLogInterpolatedStringHandler val3 = new BepInExErrorLogInterpolatedStringHandler(33, 1, out flag);
			if (flag)
			{
				((BepInExLogInterpolatedStringHandler)val3).AppendLiteral("[FM26Export] Erro FinishCapture: ");
				((BepInExLogInterpolatedStringHandler)val3).AppendFormatted<string>(ex.Message);
			}
			log5.LogError(val3);
		}
	}

	public virtual void Cleanup()
	{
		CloseOutput(_outputStarted && !_outputFinalized);
		_captureView = null;
		if (_captureHeaders != null)
		{
			_captureHeaders.Clear();
		}
		if (_captureRowHeaders != null)
		{
			_captureRowHeaders.Clear();
		}
		if (_seenKeys != null)
		{
			_seenKeys.Clear();
		}
	}

	private bool WriteCapturedRow(List<string> row)
	{
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Expected O, but got Unknown
		try
		{
			if (row == null || row.Count == 0)
			{
				return true;
			}
			bool flag = true;
			foreach (string item in row)
			{
				if (!string.IsNullOrEmpty(item))
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				return true;
			}
			EnsureOutputStarted();
			_csvWriter.WriteLine(string.Join(";", row.ConvertAll<string>(UIUtils.Esc)));
			_htmlWriter.WriteLine("<tr>");
			foreach (string item2 in row)
			{
				_htmlWriter.WriteLine("\t<td>" + HtmlEsc(item2) + "</td>");
			}
			_htmlWriter.WriteLine("</tr>");
			_capturedRowCount++;
			return true;
		}
		catch (Exception ex)
		{
			ManualLogSource log = Plugin.Log;
			bool flag2 = false;
			BepInExErrorLogInterpolatedStringHandler val = new BepInExErrorLogInterpolatedStringHandler(45, 1, out flag2);
			if (flag2)
			{
				((BepInExLogInterpolatedStringHandler)val).AppendLiteral("[FM26Export] Erro ao gravar linha exportada: ");
				((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(ex.Message);
			}
			log.LogError(val);
			CloseOutput(deleteFiles: true);
			return false;
		}
	}

	private void EnsureOutputStarted()
	{
		//IL_023e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Expected O, but got Unknown
		if (_outputStarted)
		{
			return;
		}
		string text = DateTime.Now.ToString("yyyyMMdd_HHmmss");
		string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Sports Interactive", "Football Manager 27", "FM26PlayerExport by vinteset");
		string text2 = Path.Combine(path, "Exports CSV");
		string text3 = Path.Combine(path, "Exports HTML");
		Directory.CreateDirectory(text2);
		Directory.CreateDirectory(text3);
		_csvFile = Path.Combine(text2, FilePrefix + text + ".csv");
		_htmlFile = Path.Combine(text3, FilePrefix + text + ".html");
		_csvWriter = new StreamWriter(_csvFile, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 65536);
		_htmlWriter = new StreamWriter(_htmlFile, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 65536);
		_csvWriter.WriteLine(string.Join(";", _captureHeaders));
		_htmlWriter.WriteLine("<html>");
		_htmlWriter.WriteLine("<head>");
		_htmlWriter.WriteLine("<meta charset=\"UTF-8\">");
		_htmlWriter.WriteLine("<style type =\"text/css\">");
		_htmlWriter.WriteLine("body,td,th { font-family: Verdana, Arial, Helvetica, sans-serif; font-size: 12px; }");
		_htmlWriter.WriteLine("th { padding: 5px; text-align: left; background-color: #EEEEEE; border: 1px solid #000000; font-weight: bold; }");
		_htmlWriter.WriteLine("td { padding: 4px; border: 1px solid #000000; }");
		_htmlWriter.WriteLine("table { border-collapse: collapse; width: 98%; margin: 20px auto; }");
		_htmlWriter.WriteLine("tr:nth-child(even) { background-color: #F9F9F9; }");
		_htmlWriter.WriteLine("</style>");
		_htmlWriter.WriteLine("</head>");
		_htmlWriter.WriteLine("<body>");
		_htmlWriter.WriteLine("<table border=\"1\">");
		_htmlWriter.WriteLine("<tr>");
		foreach (string captureHeader in _captureHeaders)
		{
			_htmlWriter.WriteLine("\t<th>" + HtmlEsc(captureHeader) + "</th>");
		}
		_htmlWriter.WriteLine("</tr>");
		_outputStarted = true;
		ManualLogSource log = Plugin.Log;
		bool flag = false;
		BepInExInfoLogInterpolatedStringHandler val = new BepInExInfoLogInterpolatedStringHandler(55, 2, out flag);
		if (flag)
		{
			((BepInExLogInterpolatedStringHandler)val).AppendLiteral("[FM26Export] Escrita incremental iniciada: CSV=");
			((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(_csvFile);
			((BepInExLogInterpolatedStringHandler)val).AppendLiteral(" | HTML=");
			((BepInExLogInterpolatedStringHandler)val).AppendFormatted<string>(_htmlFile);
		}
		log.LogInfo(val);
	}

	private void CloseOutput(bool deleteFiles)
	{
		try
		{
			if (_outputStarted && !_outputFinalized && _htmlWriter != null)
			{
				_htmlWriter.WriteLine("</table></body></html>");
			}
		}
		catch
		{
		}
		try
		{
			_csvWriter?.Dispose();
		}
		catch
		{
		}
		try
		{
			_htmlWriter?.Dispose();
		}
		catch
		{
		}
		_csvWriter = null;
		_htmlWriter = null;
		if (deleteFiles)
		{
			TryDelete(_csvFile);
			TryDelete(_htmlFile);
		}
		if (_outputStarted && !deleteFiles)
		{
			_outputFinalized = true;
		}
	}

	private static void TryDelete(string path)
	{
		try
		{
			if (!string.IsNullOrEmpty(path) && File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private static string HtmlEsc(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
			.Replace("\"", "&quot;")
			.Replace("'", "&#39;");
	}

	private static VisualElement FindCellRow(VisualElement start)
	{
		_cellRowBest = start;
		_cellRowBestCount = ((start != null) ? start.childCount : (-1));
		FindCellRowRec(start, 0);
		return _cellRowBest;
	}

	private static void FindCellRowRec(VisualElement el, int depth)
	{
		if (el != null && depth <= 4)
		{
			if (el.childCount > _cellRowBestCount)
			{
				_cellRowBestCount = el.childCount;
				_cellRowBest = el;
			}
			for (int i = 0; i < el.childCount; i++)
			{
				FindCellRowRec(el.ElementAt(i), depth + 1);
			}
		}
	}

	private List<string> ReadRow(VisualElement row, bool diag, List<string> headers)
	{
		List<string> list = new List<string>();
		if (row == null || row.childCount == 0)
		{
			return list;
		}
		// Search the whole row (not just child[0]). On FM27, child[0] is
		// SI.Bindable.ExpandedToggle and the cells sit as siblings on the row.
		VisualElement start = FindCellRow(row);
		if (diag)
		{
			Plugin.Log.LogInfo((object)("[FM26Export] ROW DIAG: row.childCount=" + row.childCount + " cellContainer.childCount=" + start.childCount + " containerType=" + UIUtils.RealTypeName(start)));
			int num = Math.Min(start.childCount, 8);
			for (int i = 0; i < num; i++)
			{
				Plugin.Log.LogInfo((object)("[FM26Export] Celula[" + i + "] DIAG: " + UIUtils.DiagCell(start.ElementAt(i))));
			}
			if (start.childCount > 3)
			{
				Plugin.Log.LogInfo((object)("[FM26Export] PROBE cell[3] leaf: " + UIUtils.ProbeText(UIUtils.FindLeaf(start.ElementAt(3)))));
			}
		}
		for (int j = 1; j < start.childCount; j++)
		{
			VisualElement val = start.ElementAt(j);
			string header = ((headers != null && j - 1 < headers.Count) ? headers[j - 1] : null);
			string text;
			if (j == 1)
			{
				List<string> list2 = new List<string>();
				UIUtils.CollectAllTexts(val, list2, 0, allowRenderedTextFallback: false, allowTooltipFallback: false);
				text = string.Empty;
				foreach (string item in list2)
				{
					if (item.Length > text.Length)
					{
						text = item;
					}
				}
			}
			else
			{
				text = UIUtils.CollectFirstText(val, 0, allowRenderedTextFallback: false, allowTooltipFallback: false) ?? string.Empty;
				if (string.IsNullOrEmpty(text))
				{
					StarRatingResult starRatingResult = UIUtils.TryReadStarRating(val);
					if (starRatingResult != null)
					{
						text = (IsComponentStarHeader(header) ? FormatStarComponents(starRatingResult) : UIUtils.FormatStarRating(starRatingResult.DisplayedStars, blankWhenZero: true));
					}
				}
			}
			list.Add(text);
		}
		return list;
	}

	private static List<string> BuildExportHeaders(List<string> rowHeaders)
	{
		return new List<string>(rowHeaders);
	}

	private static bool IsComponentStarHeader(string header)
	{
		switch (header)
		{
		default:
			return header == "Role Ability Out Of Possession";
		case "Ability":
		case "Potential":
		case "Role Ability":
			return true;
		}
	}

	private static string FormatStarComponents(StarRatingResult rating)
	{
		if (rating == null)
		{
			return string.Empty;
		}
		string text = UIUtils.FormatStarRating(rating.GoldStars, blankWhenZero: false);
		string text2 = UIUtils.FormatStarRating(rating.SilverStars, blankWhenZero: false);
		return text + " (" + text2 + ")";
	}

	private void FindAllByName(VisualElement root, string name, List<VisualElement> results)
	{
		if (root != null)
		{
			if (root.name == name)
			{
				results.Add(root);
			}
			for (int i = 0; i < root.childCount; i++)
			{
				FindAllByName(root.ElementAt(i), name, results);
			}
		}
	}
}
