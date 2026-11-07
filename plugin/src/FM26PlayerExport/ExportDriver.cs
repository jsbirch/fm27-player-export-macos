using System;
using System.Collections.Generic;
using FM26PlayerExport.Handlers;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

namespace FM26PlayerExport;

public sealed class ExportDriver
{
	private IExportHandler _currentHandler;

	private readonly List<IExportHandler> _availableHandlers = new List<IExportHandler>();

	private bool _started;

	private static int _dumpBudget;

	private void EnsureStarted()
	{
		if (!_started)
		{
			_started = true;
			_availableHandlers.Add(new StaffExportHandler());
			_availableHandlers.Add(new PlayerExportHandler());
			Plugin.Log.LogInfo((object)"[FM26Export] Driver pronto (Update pump). [F9 / Ctrl+P] = exportar.");
		}
	}

	public void Tick()
	{
		try
		{
			EnsureStarted();
			if (Keyboard.current == null)
			{
				return;
			}
			if (_currentHandler == null)
			{
				// Reserve Control+Option+Command+P for the companion's profile request.
				bool extraModifiers = Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed
					|| Keyboard.current.leftMetaKey.isPressed || Keyboard.current.rightMetaKey.isPressed
					|| Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
				bool num = !extraModifiers && (((ButtonControl)Keyboard.current.leftCtrlKey).isPressed || ((ButtonControl)Keyboard.current.rightCtrlKey).isPressed) && ((ButtonControl)Keyboard.current.pKey).wasPressedThisFrame;
				bool wasPressedThisFrame = ((ButtonControl)Keyboard.current.f9Key).wasPressedThisFrame;
				if (num || wasPressedThisFrame)
				{
					Plugin.Log.LogInfo((object)("[FM26Export] Iniciando exportação via atalho: " + (wasPressedThisFrame ? "F9" : "Ctrl+P")));
					StartCapture();
				}
			}
			else if (_currentHandler.CaptureStep())
			{
				_currentHandler.FinishCapture();
				try
				{
					_currentHandler.Cleanup();
				}
				catch
				{
				}
				_currentHandler = null;
				try
				{
					GC.Collect();
					GC.WaitForPendingFinalizers();
					return;
				}
				catch
				{
					return;
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private static int CountDescendants(VisualElement el, int depth)
	{
		if (el == null || depth > 40)
		{
			return 0;
		}
		int num = el.childCount;
		for (int i = 0; i < el.childCount; i++)
		{
			num += CountDescendants(el.ElementAt(i), depth + 1);
		}
		return num;
	}

	internal VisualElement GetMainRoot()
	{
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		VisualElement val = null;
		int num = -1;
		try
		{
			Il2CppArrayBase<UIDocument> val2 = UnityEngine.Object.FindObjectsOfType<UIDocument>();
			if (val2 == null)
			{
				return null;
			}
			foreach (UIDocument item in val2)
			{
				if ((UnityEngine.Object)item == (UnityEngine.Object)null)
				{
					continue;
				}
				VisualElement rootVisualElement = item.rootVisualElement;
				if (rootVisualElement != null)
				{
					int num2 = CountDescendants(rootVisualElement, 0);
					Plugin.Log.LogInfo((object)("[FM26Export] UIDocument root: name='" + rootVisualElement.name + "' children=" + rootVisualElement.childCount + " descendants=" + num2));
					if (rootVisualElement.name == "PanelManager-container")
					{
						return rootVisualElement;
					}
					if (num2 > num)
					{
						num = num2;
						val = rootVisualElement;
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError((object)("[FM26Export] GetMainRoot erro: " + ex.Message));
		}
		if (val != null)
		{
			Plugin.Log.LogInfo((object)("[FM26Export] Usando maior root: name='" + val.name + "' descendants=" + num));
		}
		return val;
	}

	private void StartCapture()
	{
		try
		{
			VisualElement mainRoot = GetMainRoot();
			if (mainRoot == null)
			{
				Plugin.Log.LogError((object)"[FM26Export] Nenhum UIDocument com rootVisualElement encontrado.");
				return;
			}
			foreach (IExportHandler availableHandler in _availableHandlers)
			{
				if (availableHandler.TryStartCapture(mainRoot, out var errorMessage))
				{
					Plugin.Log.LogInfo((object)("[FM26Export] Usando handler: " + availableHandler.GetType().Name));
					_currentHandler = availableHandler;
					return;
				}
				if (!string.IsNullOrEmpty(errorMessage))
				{
					Plugin.Log.LogInfo((object)errorMessage);
				}
			}
			Plugin.Log.LogWarning((object)"[FM26Export] Nenhuma tela suportada encontrada. Dumping estrutura FM27 para diagnóstico:");
			DumpNamedTree(mainRoot);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError((object)("[FM26Export] Erro ao iniciar captura: " + ex.Message));
		}
	}

	private void DumpNamedTree(VisualElement root)
	{
		_dumpBudget = 400;
		DumpNamedTreeRec(root, 0);
	}

	private void DumpNamedTreeRec(VisualElement el, int depth)
	{
		if (el == null || depth > 25 || _dumpBudget <= 0)
		{
			return;
		}
		try
		{
			string text = null;
			try
			{
				text = el.name;
			}
			catch
			{
			}
			List<string> list = new List<string>();
			try
			{
				for (int i = 0; i < el.classList.Count; i++)
				{
					list.Add(el.classList[i]);
				}
			}
			catch
			{
			}
			string text2 = string.Join(",", list);
			if (!string.IsNullOrEmpty(text) || text2.Contains("list") || text2.Contains("table") || text2.Contains("header") || text2.Contains("column") || text2.Contains("row") || text2.Contains("scroll") || text2.Contains("grid") || text2.Contains("star") || text2.Contains("ability") || text2.Contains("potential"))
			{
				_dumpBudget--;
				Plugin.Log.LogInfo((object)("[FM26Export.DIAG] d" + depth + " " + UIUtils.RealTypeName(el) + " name='" + text + "' ch=" + el.childCount + " cls=[" + text2 + "]"));
			}
		}
		catch
		{
		}
		for (int j = 0; j < el.childCount; j++)
		{
			if (_dumpBudget <= 0)
			{
				break;
			}
			DumpNamedTreeRec(el.ElementAt(j), depth + 1);
		}
	}
}
