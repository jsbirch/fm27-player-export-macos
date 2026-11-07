using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FM26PlayerExport.Handlers;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM26PlayerExport;

// Local file handshake with the menu-bar app. UI access stays on Unity's main thread.
internal static class ProfileRequestBridge
{
    private static readonly string BaseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "FM Profile Bridge");
    private static readonly string BridgeDirectory = Path.Combine(BaseDirectory, "Bridge");
    private static DateTime nextPoll;
    private static DateTime nextHeartbeat;

    internal static void Tick(ExportDriver driver)
    {
        if (DateTime.UtcNow < nextPoll) return;
        nextPoll = DateTime.UtcNow.AddMilliseconds(100);
        try
        {
            Directory.CreateDirectory(BridgeDirectory);
            if (DateTime.UtcNow >= nextHeartbeat)
            {
                File.WriteAllText(Path.Combine(BridgeDirectory, "ready"), Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
                nextHeartbeat = DateTime.UtcNow.AddSeconds(1);
            }
            string requestPath = Path.Combine(BridgeDirectory, "request");
            if (!File.Exists(requestPath)) return;
            string id = File.ReadAllText(requestPath).Trim();
            File.Delete(requestPath);
            if (!Guid.TryParseExact(id, "N", out _)) return;
            string response;
            try { response = "OK\n" + Export(driver.GetMainRoot(), id); }
            catch (Exception ex)
            {
                response = "ERROR\n" + ex.Message;
                Plugin.Log.LogWarning((object)("[FM27Profile] " + ex));
            }
            string output = Path.Combine(BridgeDirectory, id + ".response");
            File.WriteAllText(output + ".tmp", response, new UTF8Encoding(false));
            File.Move(output + ".tmp", output);
        }
        catch (Exception ex) { Plugin.Log.LogWarning((object)("[FM27Profile] Bridge: " + ex.Message)); }
    }

    private sealed class Fragment
    {
        internal string Text;
        internal string Path;
        internal Rect Bounds;
    }

    private static string Export(VisualElement root, string id)
    {
        if (root == null) throw new InvalidOperationException("No game interface found. Open a player's Overview or Attributes tab.");
        var fragments = new List<Fragment>();
        int budget = 20000;
        Collect(root, root.worldBound, "", fragments, 0, ref budget);
        var texts = new HashSet<string>(fragments.Select(f => f.Text), StringComparer.OrdinalIgnoreCase);
        int attributes = new[] { "Passing", "Tackling", "Positioning", "Acceleration", "Decisions", "Concentration", "Teamwork", "Stamina", "Finishing", "Reflexes" }.Count(texts.Contains);
        if (attributes < 5 || !texts.Contains("Physical") || !texts.Contains("Mental"))
            throw new InvalidOperationException("Open an individual player's Overview or Attributes tab with the attribute panels visible, then press the shortcut again. English UI is currently required.");

        // Keep every text/value pair in visual reading order; don't infer hidden ratings.
        var ordered = fragments.OrderBy(f => f.Bounds.center.y).ThenBy(f => f.Bounds.x).ToList();
        var rows = new List<List<Fragment>>();
        foreach (var fragment in ordered)
        {
            var row = rows.LastOrDefault();
            if (row == null || Math.Abs(row[0].Bounds.center.y - fragment.Bounds.center.y) > Math.Max(3f, Math.Min(row[0].Bounds.height, fragment.Bounds.height) * 0.4f))
                rows.Add(new List<Fragment> { fragment });
            else row.Add(fragment);
        }
        var body = new StringBuilder();
        body.AppendLine("Football Manager 27 — direct visible profile text");
        body.AppendLine("Source: game UI text; no screenshot or OCR. Current tab only. Masked ranges retained.");
        body.AppendLine();
        foreach (var row in rows)
            body.AppendLine(string.Join(" | ", row.OrderBy(f => f.Bounds.x).Select(f => f.Text)));

        string captures = Path.Combine(BaseDirectory, "Captures");
        Directory.CreateDirectory(captures);
        string directory = Path.Combine(captures, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture) + "_" + id.Substring(0, 8));
        string staging = directory + ".pending";
        Directory.CreateDirectory(staging);
        try
        {
            File.WriteAllText(Path.Combine(staging, "profile.txt"), body.ToString(), new UTF8Encoding(false));
            var diagnostic = new StringBuilder("x\ty\tw\th\ttext\telementPath\n");
            foreach (var f in fragments)
                diagnostic.AppendLine(string.Join("\t", new[] {
                    f.Bounds.x.ToString(CultureInfo.InvariantCulture), f.Bounds.y.ToString(CultureInfo.InvariantCulture),
                    f.Bounds.width.ToString(CultureInfo.InvariantCulture), f.Bounds.height.ToString(CultureInfo.InvariantCulture), f.Text, f.Path }));
            File.WriteAllText(Path.Combine(staging, "ui.tsv"), diagnostic.ToString(), new UTF8Encoding(false));
            Directory.Move(staging, directory);
        }
        catch { if (Directory.Exists(staging)) Directory.Delete(staging, true); throw; }
        Plugin.Log.LogInfo((object)("[FM27Profile] Exported " + fragments.Count + " visible text elements to " + directory));
        return directory;
    }

    private static void Collect(VisualElement element, Rect viewport, string path, List<Fragment> result, int depth, ref int budget)
    {
        if (element == null || depth > 70 || budget-- <= 0) return;
        if ((int)element.resolvedStyle.display == 1 || (int)element.resolvedStyle.visibility == 1 || element.resolvedStyle.opacity <= 0) return;
        string name = element.name ?? "";
        path += "/" + (name.Length == 0 ? "_" : name.Replace("\t", " "));
        Rect bounds = element.worldBound;
        // Unity scroll content can contain offscreen rows. Respect its viewport.
        if (name == "unity-content-viewport")
        {
            float x = Math.Max(viewport.xMin, bounds.xMin), y = Math.Max(viewport.yMin, bounds.yMin);
            viewport = new Rect(x, y, Math.Max(0, Math.Min(viewport.xMax, bounds.xMax) - x), Math.Max(0, Math.Min(viewport.yMax, bounds.yMax) - y));
        }
        string text = UIUtils.GetText(element, allowRenderedTextFallback: true, allowTooltipFallback: false);
        if (!string.IsNullOrWhiteSpace(text) && bounds.width > 0 && bounds.height > 0 && viewport.Overlaps(bounds))
        {
            text = System.Net.WebUtility.HtmlDecode(text).Replace("\t", " ").Replace("\r", " ").Replace("\n", " / ").Trim();
            if (text.Length <= 2000 && !result.Any(f => f.Text == text && Math.Abs(f.Bounds.x - bounds.x) < 1 && Math.Abs(f.Bounds.y - bounds.y) < 1))
                result.Add(new Fragment { Text = text, Path = path, Bounds = bounds });
        }
        for (int i = 0; i < element.childCount; i++)
            Collect(element.ElementAt(i), viewport, path, result, depth + 1, ref budget);
    }
}
