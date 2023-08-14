﻿using MSFSPopoutPanelManager.Shared;

namespace MSFSPopoutPanelManager.DomainModel.Setting
{
    public class AutoPopOutSetting : ObservableObject
    {
        public AutoPopOutSetting()
        {
            IsEnabled = true;
        }

        public bool IsEnabled { get; set; }
    }
}
