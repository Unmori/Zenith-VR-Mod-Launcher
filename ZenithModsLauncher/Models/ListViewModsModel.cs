using System.Collections.Generic;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ZenithModsLauncher.Models
{
    public class ListViewModsModel
    {
        public string Name { get; set; }
        public string HumanName { get; set; }
        public string Description { get; set; }
        public bool? IsEnabled { get; set; }
        /// <summary>
        /// Maybe is null
        /// </summary>
        public List<ModSettingsView> ModSettings { get; set; }
    }

    public class ModSettingsView
    {
        public string Name { get; set; }
        public string HumanName { get; set; }
        public string Description { get; set; }
        public ModSettingsType Type { get; set; }

        private JToken _value;
        public dynamic Value
        {
            get
            {
                switch (Type)
                {
                    case ModSettingsType.B:
                        return _value.Value<bool>();
                    case ModSettingsType.C:
                        return _value.Value<string>();
                    case ModSettingsType.I:
                        return _value.Value<int>();
                    case ModSettingsType.S:
                        return _value.Value<string>();
                    default:
                        return _value.Value<string>();
                }
            }
            set { _value = value; }
        }
    }


    public class ModSettingsViewInner
    {
        public string ModName { get; set; }
        public string Name { get; set; }
        public string HumanName { get; set; }
        public string Description { get; set; }

        public bool BoolValue { get; set; }
        public bool IsBoolValue { get; set; }
        public Visibility IsBoolValueVisible => IsBoolValue ? Visibility.Visible : Visibility.Collapsed;

        public int? IntValue { get; set; }
        public bool IsIntValue { get; set; }
        public Visibility IsIntValueVisible => IsIntValue ? Visibility.Visible : Visibility.Collapsed;

        public string StringValue { get; set; }
        public bool IsStringValue { get; set; }
        public Visibility IsStringValueVisible => IsStringValue ? Visibility.Visible : Visibility.Collapsed;

        public string ColorValue { get; set; }
        public bool IsColorValue { get; set; }
        public Visibility IsColorValueVisible => IsColorValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public class ListViewModsGridModel
    {
        public string Name { get; set; }
        public string HumanName { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; }
    }
}
