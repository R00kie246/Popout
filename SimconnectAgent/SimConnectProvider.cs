﻿using MSFSPopoutPanelManager.DomainModel.Profile;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using static MSFSPopoutPanelManager.SimConnectAgent.SimDataDefinitions;

namespace MSFSPopoutPanelManager.SimConnectAgent
{
    public class SimConnectProvider
    {
        private const int MSFS_DATA_REFRESH_TIMEOUT = 500;
        private const int MSFS_HUDBAR_DATA_REFRESH_TIMEOUT = 200;

        private readonly SimConnector _simConnector;

        private bool _isHandlingCriticalError;
        private List<SimDataItem> _requiredSimData;

        private System.Timers.Timer _requiredRequestDataTimer;
        private System.Timers.Timer _hudBarRequestDataTimer;
        private bool _isPowerOnForPopOut;
        private bool _isAvionicsOnForPopOut;
        private bool _isTrackIRManaged;
        private bool _isHudBarDataActive;
        private HudBarType _activeHudBarType;

        public event EventHandler OnConnected;
        public event EventHandler OnDisconnected;
        public event EventHandler<bool> OnIsInCockpitChanged;
        public event EventHandler OnFlightStarted;
        public event EventHandler OnFlightStopped;
        public event EventHandler OnException;
        public event EventHandler<List<SimDataItem>> OnSimConnectDataRequiredRefreshed;
        public event EventHandler<List<SimDataItem>> OnSimConnectDataHudBarRefreshed;
        public event EventHandler<string> OnActiveAircraftChanged;

        public SimConnectProvider()
        {
            _simConnector = new SimConnector();
            _simConnector.OnConnected += HandleSimConnected;
            _simConnector.OnDisconnected += HandleSimDisconnected;
            _simConnector.OnException += HandleSimException;
            _simConnector.OnReceiveSystemEvent += HandleReceiveSystemEvent;
            _simConnector.OnReceivedRequiredData += HandleRequiredDataReceived;
            _simConnector.OnReceivedHudBarData += HandleHudBarDataReceived;
            _simConnector.OnActiveAircraftChanged += (_, e) => OnActiveAircraftChanged?.Invoke(this, e);

            _isHandlingCriticalError = false;

            _isHudBarDataActive = false;
            _activeHudBarType = HudBarType.None;
        }

        public void Start()
        {
            _simConnector.Stop();
            Thread.Sleep(2000);     // wait for everything to stop
            _simConnector.Start();
        }

        public void Stop(bool appExit)
        {
            _simConnector.Stop();

            if (!appExit)
                OnDisconnected?.Invoke(this, EventArgs.Empty);
        }

        public void StopAndReconnect()
        {
            _simConnector.Stop();
            Thread.Sleep(2000);     // wait for everything to stop
            _simConnector.Restart();
        }

        public void SetHudBarConfig(HudBarType hudBarType)
        {
            if (_hudBarRequestDataTimer.Enabled && _activeHudBarType == hudBarType)
                return;

            _activeHudBarType = hudBarType;
            _isHudBarDataActive = true;

            // shut down data request and wait for the last request to be completed
            _hudBarRequestDataTimer.Stop();
            Thread.Sleep(MSFS_HUDBAR_DATA_REFRESH_TIMEOUT);

            switch (hudBarType)
            {
                case HudBarType.Generic_Aircraft:
                    _simConnector.SetSimConnectHudBarDataDefinition(SimDataDefinitionType.GenericHudBar);
                    _hudBarRequestDataTimer.Start();
                    break;
                case HudBarType.PMDG_737:
                    _simConnector.SetSimConnectHudBarDataDefinition(SimDataDefinitionType.Pmdg737HudBar);
                    _hudBarRequestDataTimer.Start();
                    break;
                default:
                    _simConnector.SetSimConnectHudBarDataDefinition(SimDataDefinitionType.NoHudBar);
                    _hudBarRequestDataTimer.Stop();
                    break;
            }
        }

        public void StopHudBar()
        {
            _hudBarRequestDataTimer.Stop();
        }

        public void TurnOnPower(bool isRequiredForColdStart)
        {
            if (!isRequiredForColdStart || _requiredSimData == null)
                return;

            // Wait for _simData.AtcOnParkingSpot to refresh
            Thread.Sleep(MSFS_DATA_REFRESH_TIMEOUT + 500);

            var planeInParkingSpot = Convert.ToBoolean(_requiredSimData.Find(d => d.PropertyName == PropName.PlaneInParkingSpot).Value);

            if (!planeInParkingSpot) 
                return;

            Debug.WriteLine("Turn On Battery Power...");

            _isPowerOnForPopOut = true;
            _simConnector.TransmitActionEvent(ActionEvent.MASTER_BATTERY_SET, 1);
        }

        public void TurnOffPower(bool isRequiredForColdStart)
        {
            if (!isRequiredForColdStart || _requiredSimData == null)
                return;

            if (!_isPowerOnForPopOut)
                return;

            Debug.WriteLine("Turn Off Battery Power...");

            _simConnector.TransmitActionEvent(ActionEvent.MASTER_BATTERY_SET, 0);
            _isPowerOnForPopOut = false;
        }

        public void TurnOnAvionics(bool isRequiredForColdStart)
        {
            if (!isRequiredForColdStart || _requiredSimData == null)
                return;

            var planeInParkingSpot = Convert.ToBoolean(_requiredSimData.Find(d => d.PropertyName == PropName.PlaneInParkingSpot).Value);
            
            if (!planeInParkingSpot)
                return;

            Debug.WriteLine("Turn On Avionics...");

            _isAvionicsOnForPopOut = true;
            _simConnector.TransmitActionEvent(ActionEvent.AVIONICS_MASTER_SET, 1);
        }

        public void TurnOffAvionics(bool isRequiredForColdStart)
        {
            if (!isRequiredForColdStart || _requiredSimData == null)
                return;

            if (!_isAvionicsOnForPopOut) 
                return;

            Debug.WriteLine("Turn Off Avionics...");

            _simConnector.TransmitActionEvent(ActionEvent.AVIONICS_MASTER_SET, 0);
            _isAvionicsOnForPopOut = false;
        }

        public void TurnOnTrackIR()
        {
            if (_requiredSimData == null)
                return;

            if (!_isTrackIRManaged)
                return;

            Debug.WriteLine("Turn On TrackIR...");
            SetTrackIREnable(true);
            _isTrackIRManaged = false;
        }

        public void TurnOffTrackIR()
        {
            if (_requiredSimData == null) 
                return;

            var trackIREnable = Convert.ToBoolean(_requiredSimData.Find(d => d.PropertyName == PropName.TrackIREnable).Value);

            if (!trackIREnable) 
                return;

            Debug.WriteLine("Turn Off TrackIR...");

            SetTrackIREnable(false);
            _isTrackIRManaged = true;
            Thread.Sleep(200);
        }

        public void TurnOnActivePause()
        {
            Debug.WriteLine("Active Pause On...");

            _simConnector.TransmitActionEvent(ActionEvent.PAUSE_SET, 1);
        }

        public void TurnOffActivePause()
        {
            Debug.WriteLine("Active Pause Off...");

            _simConnector.TransmitActionEvent(ActionEvent.PAUSE_SET, 0);
        }

        public void IncreaseSimRate()
        {
            _simConnector.TransmitActionEvent(ActionEvent.SIM_RATE_INCR, 1);
            Thread.Sleep(200);
        }

        public void DecreaseSimRate()
        {
            _simConnector.TransmitActionEvent(ActionEvent.SIM_RATE_DECR, 1);
            Thread.Sleep(200);
        }

        public void SetCockpitCameraZoomLevel(int zoomLevel)
        {
            _simConnector.SetDataObject(WritableVariableName.CockpitCameraZoom, Convert.ToDouble(zoomLevel));
        }

        public void SetCameraRequestAction(int actionEnum)
        {
            _simConnector.SetDataObject(WritableVariableName.CameraRequestAction, Convert.ToDouble(actionEnum));
        }

        public void SetCameraViewTypeAndIndex0(int actionEnum)
        {
            _simConnector.SetDataObject(WritableVariableName.CameraViewTypeAndIndex0, Convert.ToDouble(actionEnum));
        }

        public void SetCameraViewTypeAndIndex1(int actionEnum)
        {
            _simConnector.SetDataObject(WritableVariableName.CameraViewTypeAndIndex1, Convert.ToDouble(actionEnum));
        }

        private void SetTrackIREnable(bool enable)
        {
            _simConnector.SetDataObject(WritableVariableName.TrackIREnable, enable ? Convert.ToDouble(1) : Convert.ToDouble(0));
        }

        private void HandleSimConnected(object source, EventArgs e)
        {
            // Setup required data request timer
            _requiredRequestDataTimer = new()
            {
                Interval = MSFS_DATA_REFRESH_TIMEOUT
            };
            _requiredRequestDataTimer.Start();
            _requiredRequestDataTimer.Elapsed += (_, _) =>
            {
                try
                {
                    _simConnector.RequestRequiredData(); 
                    _simConnector.ReceiveMessage();
                }
                catch
                {
                    // ignored
                }
            };

            // Setup hudbar data request timer
            _hudBarRequestDataTimer = new()
            {
                Interval = MSFS_HUDBAR_DATA_REFRESH_TIMEOUT,
            };
            _hudBarRequestDataTimer.Stop();
            _hudBarRequestDataTimer.Elapsed += (_, _) =>
            {
                try
                {
                    _simConnector.RequestHudBarData();
                }
                catch
                {
                    // ignored
                }
            };

            if (_isHudBarDataActive)
                SetHudBarConfig(_activeHudBarType);

            OnConnected?.Invoke(this, EventArgs.Empty);
        }

        private void HandleSimDisconnected(object source, EventArgs e)
        {
            _requiredRequestDataTimer.Stop();
            _hudBarRequestDataTimer.Stop();
            OnDisconnected?.Invoke(this, EventArgs.Empty);
            StopAndReconnect();
        }

        private void HandleSimException(object source, string e)
        {
            OnException?.Invoke(this, EventArgs.Empty);

            _requiredRequestDataTimer.Stop();
            _hudBarRequestDataTimer.Stop();

            if (!_isHandlingCriticalError)
            {
                _isHandlingCriticalError = true;     // Prevent restarting to occur in parallel
                StopAndReconnect();
                _isHandlingCriticalError = false;
            }
        }

        private void HandleRequiredDataReceived(object sender, List<SimDataItem> e)
        {
            _requiredSimData = e;
            DetectFlightStartedOrStopped(e);
            OnSimConnectDataRequiredRefreshed?.Invoke(this, e);
        }

        private void HandleHudBarDataReceived(object sender, List<SimDataItem> e)
        {
            OnSimConnectDataHudBarRefreshed?.Invoke(this, e);
        }

        private const int CAMERA_STATE_COCKPIT = 2;
        private const int CAMERA_STATE_LOAD_SCREEN = 11;
        private const int CAMERA_STATE_HOME_SCREEN = 15;
        private int _currentCameraState = -1;

        private void DetectFlightStartedOrStopped(List<SimDataItem> simData)
        {
            // Determine is flight started or ended
            var cameraState = Convert.ToInt32(simData.Find(d => d.PropertyName == PropName.CameraState).Value);

            if (_currentCameraState == cameraState)
                return;

            if (cameraState == CAMERA_STATE_COCKPIT)
                OnIsInCockpitChanged?.Invoke(this, true);

            switch (_currentCameraState)
            {
                case CAMERA_STATE_HOME_SCREEN:
                case CAMERA_STATE_LOAD_SCREEN:
                    if (cameraState == CAMERA_STATE_COCKPIT)
                    {
                        _currentCameraState = cameraState;
                        OnFlightStarted?.Invoke(this, EventArgs.Empty);
                    }

                    break;
                case CAMERA_STATE_COCKPIT:
                    if (cameraState == CAMERA_STATE_LOAD_SCREEN || cameraState == CAMERA_STATE_HOME_SCREEN)
                    {
                        _currentCameraState = cameraState;
                        OnFlightStopped?.Invoke(this, EventArgs.Empty);
                        OnIsInCockpitChanged?.Invoke(this, false);

                        _isHudBarDataActive = false;
                        _hudBarRequestDataTimer.Stop();
                    }

                    break;
            }

            if (cameraState is CAMERA_STATE_COCKPIT or CAMERA_STATE_HOME_SCREEN or CAMERA_STATE_LOAD_SCREEN)
                _currentCameraState = cameraState;
        }

        private void HandleReceiveSystemEvent(object sender, SimConnectEvent e)
        {
            // TBD
        }
    }
}
