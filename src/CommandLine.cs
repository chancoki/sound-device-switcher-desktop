// CommandLine.cs — argument parsing and the exact argument string baked into
// generated shortcuts. Shared by the GUI (to build arguments) and the entry
// point (to consume them), so the two can never drift apart.

using System;
using System.Collections.Generic;
using System.Text;

namespace AudioSwitch
{
    internal static class CommandLine
    {
        public static string RolesToString(RoleSelection roles)
        {
            if (roles == RoleSelection.All) return "all";

            List<string> parts = new List<string>();
            if ((roles & RoleSelection.Console) != 0) parts.Add("console");
            if ((roles & RoleSelection.Multimedia) != 0) parts.Add("multimedia");
            if ((roles & RoleSelection.Communications) != 0) parts.Add("communications");

            if (parts.Count == 0) return "all";
            return string.Join(",", parts.ToArray());
        }

        public static RoleSelection ParseRoles(string text)
        {
            if (string.IsNullOrEmpty(text)) return RoleSelection.All;
            if (text.Trim().Equals("all", StringComparison.OrdinalIgnoreCase)) return RoleSelection.All;

            RoleSelection roles = RoleSelection.None;
            foreach (string raw in text.Split(','))
            {
                switch (raw.Trim().ToLowerInvariant())
                {
                    case "all": return RoleSelection.All;
                    case "console": roles |= RoleSelection.Console; break;
                    case "multimedia": roles |= RoleSelection.Multimedia; break;
                    case "communications":
                    case "comms": roles |= RoleSelection.Communications; break;
                }
            }
            return roles == RoleSelection.None ? RoleSelection.All : roles;
        }

        /// <summary>Wrap a value in quotes, dropping any quote the value itself contains.</summary>
        public static string Quote(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "") + "\"";
        }

        /// <summary>
        /// The argument string a generated shortcut passes back to this program.
        /// The label is carried along so that, if Windows re-issues the endpoint ID
        /// after the device is unplugged and re-added, the switch can still fall
        /// back to matching by name.
        /// </summary>
        public static string BuildSwitchArguments(AudioEndpoint device, bool notify, RoleSelection roles)
        {
            if (device == null) return "";
            return BuildSwitchArguments(device.Id, device.BestName, notify, roles);
        }

        /// <summary>
        /// The same argument string built from raw values, so the GUI can rebuild an
        /// identical command line for an elevated retry without an endpoint object.
        /// </summary>
        public static string BuildSwitchArguments(string deviceId, string label, bool notify,
                                                  RoleSelection roles)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("--switch ").Append(Quote(deviceId));
            builder.Append(" --label ").Append(Quote(label));
            builder.Append(" --roles ").Append(RolesToString(roles));
            if (notify) builder.Append(" --notify");
            return builder.ToString();
        }

        /// <summary>
        /// Options whose value is free text and may legitimately contain spaces.
        /// Consecutive non-flag tokens after one of these are rejoined, so
        /// "--name 切到 YTL 音箱" means the whole string rather than just "切到".
        /// PowerShell's Start-Process joins an -ArgumentList array with spaces and
        /// does not add quotes, so this is the common case, not an exotic one.
        /// </summary>
        private static readonly string[] TextOptions =
        {
            "name", "label", "dir", "icon", "target", "description"
        };

        private static bool IsTextOption(string key)
        {
            foreach (string option in TextOptions)
            {
                if (string.Equals(option, key, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Parse "--key value" pairs and bare "--flag" switches into a dictionary.
        /// </summary>
        public static Dictionary<string, string> Parse(string[] args)
        {
            Dictionary<string, string> options =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (args == null) return options;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == null || !arg.StartsWith("--")) continue;

                string key = arg.Substring(2);
                if (key.Length == 0) continue;

                bool hasValue = i + 1 < args.Length
                                && args[i + 1] != null
                                && !args[i + 1].StartsWith("--");

                if (!hasValue)
                {
                    options[key] = "true";
                    continue;
                }

                if (IsTextOption(key))
                {
                    StringBuilder joined = new StringBuilder();
                    while (i + 1 < args.Length
                           && args[i + 1] != null
                           && !args[i + 1].StartsWith("--"))
                    {
                        if (joined.Length > 0) joined.Append(' ');
                        joined.Append(args[i + 1]);
                        i++;
                    }
                    options[key] = joined.ToString();
                }
                else
                {
                    options[key] = args[i + 1];
                    i++;
                }
            }
            return options;
        }

        public static string Get(Dictionary<string, string> options, string key)
        {
            string value;
            if (options != null && options.TryGetValue(key, out value)) return value;
            return "";
        }

        public static bool Has(Dictionary<string, string> options, string key)
        {
            return options != null && options.ContainsKey(key);
        }
    }
}
