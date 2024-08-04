using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VitaminStoreBot
{
    public class IniParser
    {
        private readonly string _iniFilePath;
        private readonly Dictionary<string, Dictionary<string, string>> _data;

        public IniParser(string iniFilePath)
        {
            _iniFilePath = iniFilePath;
            _data = new Dictionary<string, Dictionary<string, string>>();

            LoadIniFile();
        }

        private void LoadIniFile()
        {
            if (!File.Exists(_iniFilePath))
            {
                return;
            }

            string[] lines = File.ReadAllLines(_iniFilePath);
            string currentSection = string.Empty;

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";"))
                {
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Trim('[', ']');
                    if (!_data.ContainsKey(currentSection))
                    {
                        _data[currentSection] = new Dictionary<string, string>();
                    }
                }
                else if (!string.IsNullOrEmpty(currentSection))
                {
                    string[] kvp = line.Split(new char[] { '=' }, 2);
                    if (kvp.Length == 2)
                    {
                        _data[currentSection][kvp[0].Trim()] = kvp[1].Trim();
                    }
                }
            }
        }

        public string Read(string section, string key, string defaultValue = "")
        {
            if (_data.ContainsKey(section) && _data[section].ContainsKey(key))
            {
                return _data[section][key];
            }

            return defaultValue;
        }

        public void Write(string section, string key, string value)
        {
            if (!_data.ContainsKey(section))
            {
                _data[section] = new Dictionary<string, string>();
            }

            _data[section][key] = value;
            SaveIniFile();
        }

        private void SaveIniFile()
        {
            List<string> lines = new List<string>();

            foreach (var section in _data)
            {
                lines.Add($"[{section.Key}]");

                foreach (var kvp in section.Value)
                {
                    lines.Add($"{kvp.Key} = {kvp.Value}");
                }

                lines.Add(string.Empty);
            }

            File.WriteAllLines(_iniFilePath, lines);
        }

        public Dictionary<string, string> GetSection(string section)
        {
            if (_data.ContainsKey(section))
            {
                return _data[section];
            }

            return new Dictionary<string, string>();
        }
    }

}
