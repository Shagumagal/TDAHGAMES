using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System;

public class SessionLogger : MonoBehaviour
{
    private readonly List<Dictionary<string, object>> phases = new();
    private string participantId = "demo";
    private DateTime startUtc;

    public void StartSession(string pid = null)
    {
        participantId = string.IsNullOrEmpty(pid) ? participantId : pid;
        startUtc = DateTime.UtcNow;
    }

    public void AppendPhaseSummary(string phaseName, Dictionary<string, object> data)
    {
        if (data == null) data = new Dictionary<string, object>();
        data["phase"] = phaseName;
        data["ts"] = DateTime.UtcNow.ToString("o");
        phases.Add(data);
    }

    public void FlushToDisk()
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.AppendFormat("\"participant_id\":\"{0}\",", Escape(participantId));
        sb.AppendFormat("\"ts_start\":\"{0}\",", startUtc.ToString("o"));
        sb.AppendFormat("\"ts_end\":\"{0}\",", DateTime.UtcNow.ToString("o"));
        sb.Append("\"phases\":[");
        for (int i = 0; i < phases.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append(DictToJson(phases[i]));
        }
        sb.Append("]}");

        var path = Path.Combine(Application.persistentDataPath,
            $"farm_session_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Debug.Log("[SessionLogger] Guardado: " + path);
    }

    private static string DictToJson(Dictionary<string, object> d)
    {
        var sb = new StringBuilder(); sb.Append("{");
        bool first = true;
        foreach (var kv in d)
        {
            if (!first) sb.Append(",");
            first = false;
            sb.AppendFormat("\"{0}\":{1}", Escape(kv.Key), AnyToJson(kv.Value));
        }
        sb.Append("}");
        return sb.ToString();
    }

    private static string AnyToJson(object v)
    {
        if (v == null) return "null";
        if (v is string s) return "\"" + Escape(s) + "\"";
        if (v is bool b) return b ? "true" : "false";
        if (v is int or long or float or double or decimal) return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        if (v is Dictionary<string, object> dict) return DictToJson(dict);
        if (v is IEnumerable<object> list)
        {
            var sb = new StringBuilder(); sb.Append("[");
            bool first = true;
            foreach (var it in list)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append(AnyToJson(it));
            }
            sb.Append("]");
            return sb.ToString();
        }
        // fallback: ToString como string
        return "\"" + Escape(v.ToString()) + "\"";
    }

    private static string Escape(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
