using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace GlideRail
{
    public static class GlidePathSerializer
    {
        // ── JSON keys — krótkie żeby zminimalizować rozmiar stringa ──────────
        // {"dur":12.0,"kfs":[{"px":1.2,"py":3.4,"pz":5.6,
        //   "rx":0.1,"ry":0.2,"rz":0.3,"rw":0.9,"spd":1.0},...]}


        public static string Serialize(
            IReadOnlyList<GlideKeyframe> kfs, float duration)
        {
            if (kfs == null || kfs.Count == 0) return null;

            var sb = new StringBuilder();
            sb.Append("{\"dur\":");
            sb.Append(duration.ToString("F2",
                System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"kfs\":[");

            for (int i = 0; i < kfs.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var kf = kfs[i];
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                sb.Append("{\"px\":"); sb.Append(kf.Position.x.ToString("F3", ci));
                sb.Append(",\"py\":"); sb.Append(kf.Position.y.ToString("F3", ci));
                sb.Append(",\"pz\":"); sb.Append(kf.Position.z.ToString("F3", ci));
                sb.Append(",\"rx\":"); sb.Append(kf.Rotation.x.ToString("F4", ci));
                sb.Append(",\"ry\":"); sb.Append(kf.Rotation.y.ToString("F4", ci));
                sb.Append(",\"rz\":"); sb.Append(kf.Rotation.z.ToString("F4", ci));
                sb.Append(",\"rw\":"); sb.Append(kf.Rotation.w.ToString("F4", ci));
                sb.Append(",\"spd\":"); sb.Append(kf.SpeedMultiplier.ToString("F2", ci));
                sb.Append('}');
            }

            sb.Append("]}");

            // JSON → GZip → Base64
            byte[] jsonBytes = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] compressed = GZipCompress(jsonBytes);
            return Convert.ToBase64String(compressed);
        }

        public static List<GlideKeyframe> Deserialize(
            string data, out float duration)
        {
            duration = 12f;
            if (string.IsNullOrWhiteSpace(data)) return null;

            try
            {
                // Base64 → GZip → JSON
                byte[] compressed = Convert.FromBase64String(data.Trim());
                byte[] jsonBytes = GZipDecompress(compressed);
                string json = Encoding.UTF8.GetString(jsonBytes);

                return ParseJson(json, out duration);
            }
            catch (Exception ex)
            {
                GlideRailPlugin.Log.Warning(
                    $"[GlideRail] Deserialize failed: {ex.Message}");
                return null;
            }
        }

        // ── GZip ─────────────────────────────────────────────────────────────

        private static byte[] GZipCompress(byte[] data)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionMode.Compress, true))
                gz.Write(data, 0, data.Length);
            return ms.ToArray();
        }

        private static byte[] GZipDecompress(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var result = new MemoryStream();
            gz.CopyTo(result);
            return result.ToArray();
        }

        // ── Ręczny parser JSON — bez zewnętrznych zależności ─────────────────

        private static List<GlideKeyframe> ParseJson(string json, out float duration)
        {
            duration = 12f;
            var kfs = new List<GlideKeyframe>();
            var ci = System.Globalization.CultureInfo.InvariantCulture;

            // Odczyt wersji — opcjonalny, nie crashuje jeśli brak
            try
            {
                string ver = ExtractValue(json, "\"gliderail_version\":");
                // Usuń cudzysłowy jeśli są
                ver = ver.Trim('"', ' ');
                if (!string.IsNullOrEmpty(ver) && ver != GetPluginVersion())
                    GlideRailPlugin.Log.Warning(
                        $"[GlideRail] File version '{ver}' differs from current '{GetPluginVersion()}'");
            }
            catch { }

            // duration
            duration = float.Parse(ExtractValue(json, "\"dur\":"), ci);

            // keyframes array
            int arrStart = json.IndexOf("\"kfs\":[") + 7;
            int arrEnd = json.LastIndexOf(']');
            string arr = json.Substring(arrStart, arrEnd - arrStart);

            // split by objects
            int depth = 0, objStart = -1;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == '{')
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (arr[i] == '}')
                {
                    depth--;
                    if (depth == 0 && objStart >= 0)
                    {
                        string obj = arr.Substring(objStart, i - objStart + 1);
                        kfs.Add(ParseKeyframe(obj, ci));
                        objStart = -1;
                    }
                }
            }

            return kfs;
        }

        private static GlideKeyframe ParseKeyframe(
            string obj,
            System.Globalization.CultureInfo ci)
        {
            return new GlideKeyframe
            {
                Position = new Vector3(
                    float.Parse(ExtractValue(obj, "\"px\":"), ci),
                    float.Parse(ExtractValue(obj, "\"py\":"), ci),
                    float.Parse(ExtractValue(obj, "\"pz\":"), ci)),
                Rotation = new Quaternion(
                    float.Parse(ExtractValue(obj, "\"rx\":"), ci),
                    float.Parse(ExtractValue(obj, "\"ry\":"), ci),
                    float.Parse(ExtractValue(obj, "\"rz\":"), ci),
                    float.Parse(ExtractValue(obj, "\"rw\":"), ci)),
                SpeedMultiplier =
                    float.Parse(ExtractValue(obj, "\"spd\":"), ci)
            };
        }

        private static string ExtractValue(string json, string key)
        {
            int start = json.IndexOf(key) + key.Length;
            int end = start;
            while (end < json.Length &&
                   json[end] != ',' && json[end] != '}')
                end++;
            return json.Substring(start, end - start).Trim();
        }


        /// <summary>Serializuje do czytelnego JSON</summary>
        public static string SerializeJson(IReadOnlyList<GlideKeyframe> kfs, float duration)
        {
            if (kfs == null || kfs.Count == 0) return null;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"gliderail_version\": \"{GetPluginVersion()}\",");
            sb.AppendLine($"  \"dur\": {duration.ToString("F2", ci)},");
            sb.AppendLine("  \"kfs\": [");
            for (int i = 0; i < kfs.Count; i++)
            {
                var kf = kfs[i];
                sb.Append("    {");
                sb.Append($"\"px\":{kf.Position.x.ToString("F3", ci)},");
                sb.Append($"\"py\":{kf.Position.y.ToString("F3", ci)},");
                sb.Append($"\"pz\":{kf.Position.z.ToString("F3", ci)},");
                sb.Append($"\"rx\":{kf.Rotation.x.ToString("F4", ci)},");
                sb.Append($"\"ry\":{kf.Rotation.y.ToString("F4", ci)},");
                sb.Append($"\"rz\":{kf.Rotation.z.ToString("F4", ci)},");
                sb.Append($"\"rw\":{kf.Rotation.w.ToString("F4", ci)},");
                sb.Append($"\"spd\":{kf.SpeedMultiplier.ToString("F2", ci)}");
                sb.Append(i < kfs.Count - 1 ? "}," : "}");
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>Deserializuje z pliku JSON.</summary>
        public static List<GlideKeyframe> DeserializeJson(string json, out float duration) => ParseJson(json, out duration);  // ten sam parser co już mamy


        private static string GetPluginVersion()
        {
            try
            {
                var attr = typeof(GlideRailPlugin).Assembly
                    .GetCustomAttributes(typeof(MelonLoader.MelonInfoAttribute), false);
                if (attr.Length > 0)
                    return ((MelonLoader.MelonInfoAttribute)attr[0]).Version ?? "unknown";
                return "unknown";
            }
            catch { return "unknown"; }
        }

    }
}