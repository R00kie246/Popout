﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MSFSPopoutPanelManager.Model
{
    public class UserProfile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public UserProfile()
        {
            PanelSourceCoordinates = new ObservableCollection<PanelSourceCoordinate>();
            PanelConfigs = new ObservableCollection<PanelConfig>();
            BindingPlaneTitle = new ObservableCollection<string>();
            IsLocked = false;
        }

        public int ProfileId { get; set; }

        public string ProfileName { get; set; }

        public bool IsDefaultProfile { get; set; }

        [JsonConverter(typeof(SingleValueArrayConvertor<string>))]
        public ObservableCollection<string> BindingPlaneTitle { get; set; }

        public bool IsLocked { get; set; }

        public ObservableCollection<PanelSourceCoordinate> PanelSourceCoordinates;

        public ObservableCollection<PanelConfig> PanelConfigs { get; set; }

        public bool PowerOnRequiredForColdStart { get; set; }

        public void Reset()
        {
            PanelSourceCoordinates.Clear();
            PanelConfigs.Clear();
            IsLocked = false;
        }

        [JsonIgnore]
        public bool IsActive { get; set; }

        [JsonIgnore]
        public bool HasBindingPlaneTitle
        {
            get { return BindingPlaneTitle.Count > 0; }
        }
    }

    public class SingleValueArrayConvertor<T> : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            object val = new object();

            if(reader.TokenType == JsonToken.String)
            {
                var instance = (string)serializer.Deserialize(reader, typeof(string));
                val = new ObservableCollection<string>() { instance };
            }
            else if(reader.TokenType == JsonToken.StartArray)
            {
                val = serializer.Deserialize(reader, objectType);
            }
            else if(reader.TokenType == JsonToken.Null)
            {
                val = new ObservableCollection<string>();
            }

            return val;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
