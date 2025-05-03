using DJI.WindowsSDK;
using DJI.WindowsSDK.Mission.Waypoint;
using DJIUWPSample.Commands;
using DJIUWPSample.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Threading.Tasks;

namespace DJIWindowsSDKSample.ViewModels
{
    class WaypointMissionViewModel : ViewModelBase
    {
        private static readonly WaypointMissionViewModel _singleton = new WaypointMissionViewModel();
        public static WaypointMissionViewModel Instance
        {
            get
            {
                return _singleton;
            }
        }

        // Propriedades para inspeção de postes
        private List<PoleLocation> _polesLocations = new List<PoleLocation>();
        private List<PolePhotoInfo> _photosInfo = new List<PolePhotoInfo>();
        private string _polesFilePath;
        private string _parametersFilePath;
        private string _inspectionStatusMessage = "Pronto para carregar ficheiros.";
        private bool _inspectionFilesLoaded = false;

        // Estruturas para armazenar informações de postes e fotografias
        private struct PoleLocation
        {
            public string PoleId;
            public double Latitude;
            public double Longitude;
            public double Altitude;
        }

        private struct PolePhotoInfo
        {
            public string PoleId;
            public double RelativeLatitude;
            public double RelativeLongitude;
            public double RelativeAltitude;
            public double CameraTilt;
        }

        // Construtor
        private WaypointMissionViewModel()
        {
            DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).IsSimulatorStartedChanged += WaypointMission_IsSimulatorStartedChanged;
            DJISDKManager.Instance.WaypointMissionManager.GetWaypointMissionHandler(0).StateChanged += WaypointMission_StateChanged;
            DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).AltitudeChanged += WaypointMission_AltitudeChanged; ;
            DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).AircraftLocationChanged += WaypointMission_AircraftLocationChanged; ;
            WaypointMissionState = DJISDKManager.Instance.WaypointMissionManager.GetWaypointMissionHandler(0).GetCurrentState();
        }

        // Propriedades para binding
        public string PolesFilePath
        {
            get { return _polesFilePath; }
            set
            {
                _polesFilePath = value;
                OnPropertyChanged(nameof(PolesFilePath));
            }
        }

        public string ParametersFilePath
        {
            get { return _parametersFilePath; }
            set
            {
                _parametersFilePath = value;
                OnPropertyChanged(nameof(ParametersFilePath));
            }
        }

        public string InspectionStatusMessage
        {
            get { return _inspectionStatusMessage; }
            set
            {
                _inspectionStatusMessage = value;
                OnPropertyChanged(nameof(InspectionStatusMessage));
            }
        }

        // Comandos para inspeção de postes
        public ICommand _loadPolesCommand;
        public ICommand LoadPolesCommand
        {
            get
            {
                if (_loadPolesCommand == null)
                {
                    _loadPolesCommand = new RelayCommand(async delegate ()
                    {
                        var picker = new FileOpenPicker();
                        picker.ViewMode = PickerViewMode.List;
                        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                        picker.FileTypeFilter.Add(".csv");
                        picker.FileTypeFilter.Add(".txt");

                        StorageFile file = await picker.PickSingleFileAsync();
                        if (file != null)
                        {
                            PolesFilePath = file.Path;
                            bool success = await LoadPolesLocationFile(file);
                            if (success)
                            {
                                InspectionStatusMessage = $"Ficheiro de postes carregado com sucesso. Total: {_polesLocations.Count} postes.";
                                CheckInspectionFilesLoaded();
                            }
                            else
                            {
                                InspectionStatusMessage = "Erro ao carregar ficheiro de postes.";
                            }
                        }
                    }, delegate () { return true; });
                }
                return _loadPolesCommand;
            }
        }

        public ICommand _loadPhotosCommand;
        public ICommand LoadPhotosCommand
        {
            get
            {
                if (_loadPhotosCommand == null)
                {
                    _loadPhotosCommand = new RelayCommand(async delegate ()
                    {
                        var picker = new FileOpenPicker();
                        picker.ViewMode = PickerViewMode.List;
                        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                        picker.FileTypeFilter.Add(".csv");
                        picker.FileTypeFilter.Add(".txt");

                        StorageFile file = await picker.PickSingleFileAsync();
                        if (file != null)
                        {
                            ParametersFilePath = file.Path;
                            bool success = await LoadPhotosInfoFile(file);
                            if (success)
                            {
                                InspectionStatusMessage = $"Ficheiro de fotografias carregado com sucesso. Total: {_photosInfo.Count} fotografias.";
                                CheckInspectionFilesLoaded();
                            }
                            else
                            {
                                InspectionStatusMessage = "Erro ao carregar ficheiro de fotografias.";
                            }
                        }
                    }, delegate () { return true; });
                }
                return _loadPhotosCommand;
            }
        }

        public ICommand _generateInspectionMission;
        public ICommand GenerateInspectionMission
        {
            get
            {
                if (_generateInspectionMission == null)
                {
                    _generateInspectionMission = new RelayCommand(async delegate ()
                    {
                        if (!_inspectionFilesLoaded)
                        {
                            var messageDialog = new MessageDialog("Carregue primeiro os ficheiros de postes e parâmetros de fotografias.");
                            await messageDialog.ShowAsync();
                            return;
                        }

                        GeneratePolesInspectionMission();

                        // Verificação corrigida: WaypointMission é um tipo de valor
                        if (WaypointMission.waypoints != null && WaypointMission.waypoints.Count > 0)
                        {
                            var messageDialog = new MessageDialog($"Missão gerada com sucesso com {WaypointMission.waypoints.Count} waypoints para fotografia!");
                            await messageDialog.ShowAsync();
                            InspectionStatusMessage = $"Missão gerada: {WaypointMission.waypoints.Count} fotografias em waypoints.";
                        }
                        else
                        {
                            var messageDialog = new MessageDialog("Falha ao gerar missão. Verifique se os IDs dos postes nos dois ficheiros coincidem.");
                            await messageDialog.ShowAsync();
                        }
                    }, delegate () { return _inspectionFilesLoaded; });
                }
                return _generateInspectionMission;
            }
        }

        // Método para verificar se os arquivos foram carregados
        private void CheckInspectionFilesLoaded()
        {
            _inspectionFilesLoaded = (_polesLocations.Count > 0 && _photosInfo.Count > 0);
        }

        // Método para carregar arquivo de localizações de postes
        private async Task<bool> LoadPolesLocationFile(StorageFile file)
        {
            try
            {
                _polesLocations.Clear();
                string content = await FileIO.ReadTextAsync(file);
                string[] lines = content.Split('\n');
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Formato: ID,Latitude,Longitude,Altitude
                    string[] parts = line.Split(',');
                    if (parts.Length >= 4)
                    {
                        PoleLocation pole = new PoleLocation
                        {
                            PoleId = parts[0].Trim(),
                            Latitude = Convert.ToDouble(parts[1].Trim()),
                            Longitude = Convert.ToDouble(parts[2].Trim()),
                            Altitude = Convert.ToDouble(parts[3].Trim())
                        };
                        _polesLocations.Add(pole);
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Método para carregar arquivo de informações de fotografias
        private async Task<bool> LoadPhotosInfoFile(StorageFile file)
        {
            try
            {
                _photosInfo.Clear();
                string content = await FileIO.ReadTextAsync(file);
                string[] lines = content.Split('\n');
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Formato: PoleID,RelativeLat,RelativeLong,RelativeAlt,CameraTilt
                    string[] parts = line.Split(',');
                    if (parts.Length >= 5)
                    {
                        PolePhotoInfo photo = new PolePhotoInfo
                        {
                            PoleId = parts[0].Trim(),
                            RelativeLatitude = Convert.ToDouble(parts[1].Trim()),
                            RelativeLongitude = Convert.ToDouble(parts[2].Trim()),
                            RelativeAltitude = Convert.ToDouble(parts[3].Trim()),
                            CameraTilt = Convert.ToDouble(parts[4].Trim())
                        };
                        _photosInfo.Add(photo);
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Método para gerar missão de inspeção de postes
        private void GeneratePolesInspectionMission()
        {
            // Criar nova missão
            WaypointMission mission = new WaypointMission()
            {
                maxFlightSpeed = 15,
                autoFlightSpeed = 10,
                finishedAction = WaypointMissionFinishedAction.GO_HOME,
                headingMode = WaypointMissionHeadingMode.AUTO,
                flightPathMode = WaypointMissionFlightPathMode.NORMAL,
                gotoFirstWaypointMode = WaypointMissionGotoFirstWaypointMode.SAFELY,
                exitMissionOnRCSignalLostEnabled = false,
                pointOfInterest = new LocationCoordinate2D() { latitude = 0, longitude = 0 },
                gimbalPitchRotationEnabled = true,
                repeatTimes = 0,
                missionID = 0,
                waypoints = new List<Waypoint>()
            };

            // Agrupar fotos por poste
            Dictionary<string, List<PolePhotoInfo>> polePhotosMap = new Dictionary<string, List<PolePhotoInfo>>();
            foreach (PolePhotoInfo photo in _photosInfo)
            {
                if (!polePhotosMap.ContainsKey(photo.PoleId))
                {
                    polePhotosMap[photo.PoleId] = new List<PolePhotoInfo>();
                }
                polePhotosMap[photo.PoleId].Add(photo);
            }

            // Criar waypoints para cada poste
            foreach (PoleLocation pole in _polesLocations)
            {
                if (polePhotosMap.TryGetValue(pole.PoleId, out List<PolePhotoInfo> photos))
                {
                    // Adicionar ponto de aproximação segura
                    Waypoint approachWp = new Waypoint()
                    {
                        location = new LocationCoordinate2D() { latitude = pole.Latitude, longitude = pole.Longitude },
                        altitude = (float)(pole.Altitude + 10.0), // 10 metros acima do poste
                        gimbalPitch = 0,
                        turnMode = WaypointTurnMode.CLOCKWISE,
                        heading = 0,
                        actionRepeatTimes = 1,
                        actionTimeoutInSeconds = 60,
                        cornerRadiusInMeters = 0.2,
                        speed = 5,
                        shootPhotoTimeInterval = -1,
                        shootPhotoDistanceInterval = -1,
                        waypointActions = new List<WaypointAction>()
                    };
                    mission.waypoints.Add(approachWp);

                    // Adicionar waypoints para cada foto
                    foreach (PolePhotoInfo photo in photos)
                    {
                        // Calcular posição absoluta da foto
                        double photoLat = pole.Latitude + photo.RelativeLatitude;
                        double photoLong = pole.Longitude + photo.RelativeLongitude;
                        double photoAlt = pole.Altitude + photo.RelativeAltitude;

                        // Criar waypoint para a foto
                        Waypoint photoWp = new Waypoint()
                        {
                            location = new LocationCoordinate2D()
                            {
                                latitude = photoLat,
                                longitude = photoLong
                            },
                            altitude = (float)photoAlt,
                            gimbalPitch = (float)photo.CameraTilt,
                            turnMode = WaypointTurnMode.CLOCKWISE,
                            heading = 0,
                            actionRepeatTimes = 1,
                            actionTimeoutInSeconds = 60,
                            cornerRadiusInMeters = 0.2,
                            speed = 3,
                            shootPhotoTimeInterval = -1,
                            shootPhotoDistanceInterval = -1,
                            waypointActions = new List<WaypointAction>()
                        };

                        // Adicionar ação para tirar foto
                        photoWp.waypointActions.Add(new WaypointAction()
                        {
                            actionType = WaypointActionType.START_TAKE_PHOTO,
                            actionParam = 0
                        });

                        // Adicionar waypoint à missão
                        mission.waypoints.Add(photoWp);
                    }

                    // Adicionar ponto de saída segura
                    Waypoint exitWp = new Waypoint()
                    {
                        location = new LocationCoordinate2D() { latitude = pole.Latitude, longitude = pole.Longitude },
                        altitude = (float)(pole.Altitude + 10.0), // 10 metros acima do poste
                        gimbalPitch = 0,
                        turnMode = WaypointTurnMode.CLOCKWISE,
                        heading = 0,
                        actionRepeatTimes = 1,
                        actionTimeoutInSeconds = 60,
                        cornerRadiusInMeters = 0.2,
                        speed = 5,
                        shootPhotoTimeInterval = -1,
                        shootPhotoDistanceInterval = -1,
                        waypointActions = new List<WaypointAction>()
                    };
                    mission.waypoints.Add(exitWp);
                }
            }

            // Atualizar a missão
            WaypointMission = mission;
        }

        // Funções existentes - não modificar
        private async void WaypointMission_AircraftLocationChanged(object sender, LocationCoordinate2D? value)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (value.HasValue)
                {
                    AircraftLocation = value.Value;
                }
            });
        }

        private async void WaypointMission_AltitudeChanged(object sender, DoubleMsg? value)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (value.HasValue)
                {
                    AircraftAltitude = value.Value.value;
                }
            });
        }

        private async void WaypointMission_StateChanged(WaypointMissionHandler sender, WaypointMissionStateTransition? value)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                WaypointMissionState = value.HasValue ? value.Value.current : WaypointMissionState.UNKNOWN;
            });
        }

        private async void WaypointMission_IsSimulatorStartedChanged(object sender, BoolMsg? value)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                IsSimulatorStart = value.HasValue && value.Value.value;
            });
        }

        public String SimulatorLatitude { set; get; }
        public String SimulatorLongitude { set; get; }
        public String SimulatorSatelliteCount { set; get; }
        bool _isSimulatorStart = false;
        public bool IsSimulatorStart
        {
            get
            {
                return _isSimulatorStart;
            }
            set
            {
                _isSimulatorStart = value;
                OnPropertyChanged(nameof(IsSimulatorStart));
                OnPropertyChanged(nameof(SimulatorState));
            }
        }
        public String SimulatorState
        {
            get
            {
                return _isSimulatorStart ? "Open" : "Close";
            }
        }
        private WaypointMissionState _waypointMissionState;
        public WaypointMissionState WaypointMissionState
        {
            get
            {
                return _waypointMissionState;
            }
            set
            {
                _waypointMissionState = value;
                OnPropertyChanged(nameof(WaypointMissionState));
            }
        }


        private double _aircraftAltitude = 0;
        public double AircraftAltitude
        {
            get
            {
                return _aircraftAltitude;
            }
            set
            {
                _aircraftAltitude = value;
                OnPropertyChanged(nameof(AircraftAltitude));
            }
        }


        private WaypointMission _waypointMission;
        public WaypointMission WaypointMission
        {
            get { return _waypointMission; }
            set
            {
                _waypointMission = value;
                OnPropertyChanged(nameof(WaypointMission));
            }
        }

        private LocationCoordinate2D _aircraftLocation = new LocationCoordinate2D() { latitude = 0, longitude = 0 };
        public LocationCoordinate2D AircraftLocation
        {
            get
            {
                return _aircraftLocation;
            }
            set
            {
                _aircraftLocation = value;
                OnPropertyChanged(nameof(AircraftLocation));
            }
        }

        // Métodos e propriedades existentes - manter sem alterações
        private Waypoint InitDumpWaypoint(double latitude, double longitude)
        {
            Waypoint waypoint = new Waypoint()
            {
                location = new LocationCoordinate2D() { latitude = latitude, longitude = longitude },
                altitude = 20,
                gimbalPitch = -30,
                turnMode = WaypointTurnMode.CLOCKWISE,
                heading = 0,
                actionRepeatTimes = 1,
                actionTimeoutInSeconds = 60,
                cornerRadiusInMeters = 0.2,
                speed = 0,
                shootPhotoTimeInterval = -1,
                shootPhotoDistanceInterval = -1,
                waypointActions = new List<WaypointAction>()
            };
            return waypoint;
        }

        public ICommand _initWaypointMission;
        public ICommand InitWaypointMission
        {
            get
            {
                if (_initWaypointMission == null)
                {
                    _initWaypointMission = new RelayCommand(delegate ()
                    {
                        double nowLat = AircraftLocation.latitude;
                        double nowLng = AircraftLocation.longitude;
                        WaypointMission mission = new WaypointMission()
                        {
                            waypointCount = 0,
                            maxFlightSpeed = 15,
                            autoFlightSpeed = 10,
                            finishedAction = WaypointMissionFinishedAction.NO_ACTION,
                            headingMode = WaypointMissionHeadingMode.AUTO,
                            flightPathMode = WaypointMissionFlightPathMode.NORMAL,
                            gotoFirstWaypointMode = WaypointMissionGotoFirstWaypointMode.SAFELY,
                            exitMissionOnRCSignalLostEnabled = false,
                            pointOfInterest = new LocationCoordinate2D()
                            {
                                latitude = 0,
                                longitude = 0
                            },
                            gimbalPitchRotationEnabled = true,
                            repeatTimes = 0,
                            missionID = 0,
                            waypoints = new List<Waypoint>()
                            {
                                InitDumpWaypoint(nowLat+0.0001, nowLng+0.00015),
                                InitDumpWaypoint(nowLat+0.0001, nowLng-0.00015),
                                InitDumpWaypoint(nowLat-0.0001, nowLng-0.00015),
                                InitDumpWaypoint(nowLat-0.0001, nowLng+0.00015),
                            }
                        };
                        WaypointMission = mission;
                    }, delegate () { return true; });
                }
                return _initWaypointMission;
            }
        }

        public ICommand _addAction;
        public ICommand AddAction
        {
            get
            {
                if (_addAction == null)
                {
                    _addAction = new RelayCommand(async delegate ()
                    {
                        String dialogMsg = "";
                        do
                        {
                            if (WaypointMission.waypoints.Count < 2)
                            {
                                dialogMsg = "Mission not inited, init mission first!";
                                break;
                            }
                            WaypointMission.waypoints[1].waypointActions.Add(new WaypointAction() { actionType = WaypointActionType.STAY, actionParam = 2000 });
                            dialogMsg = "Add action success! Aircraft would stay at the second waypoint for 2000ms.";
                        } while (false);
                        var messageDialog = new MessageDialog(dialogMsg);
                        await messageDialog.ShowAsync();
                    }, delegate () { return true; });
                }
                return _addAction;
            }
        }

        public ICommand _loadMission;
        public ICommand LoadMission
        {
            get
            {
                if (_loadMission == null)
                {
                    _loadMission = new RelayCommand(async delegate ()
                    {
                        SDKError err = DJISDKManager.Instance.WaypointMissionManager.GetWaypointMissionHandler(0).LoadMission(this.WaypointMission);
                        var messageDialog = new MessageDialog(String.Format("SDK load mission: {0}", err.ToString()));
                        await messageDialog.ShowAsync();
                    }, delegate () { return true; });
                }
                return _loadMission;
            }
        }

        public ICommand _setGroundStationModeEnabled;
        public ICommand SetGroundStationModeEnabled
        {
            get
            {
                if (_setGroundStationModeEnabled == null)
                {
                    _setGroundStationModeEnabled = new RelayCommand(async delegate ()
                    {
                        SDKError err = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).SetGroundStationModeEnabledAsync(new BoolMsg() { value = true });
                        var messageDialog = new MessageDialog(String.Format("Set GroundStationMode Enabled: {0}", err.ToString()));
                        await messageDialog.ShowAsync();
                    }, delegate () { return true; });
                }
                return _setGroundStationModeEnabled;
            }
        }

        public ICommand _uploadMission;
        public ICommand UploadMission
        {
            get
            {
                if (_uploadMission == null)
                {
                    _uploadMission = new RelayCommand(async delegate ()
                    {
                        SDKError err = await DJISDKManager.Instance.WaypointMissionManager.GetWaypointMissionHandler(0).UploadMission();
                        var messageDialog = new MessageDialog(String.Format("Upload mission to aircraft: {0}", err.ToString()));
                        await messageDialog.ShowAsync();
                    }, delegate () { return true; });
                }
                return _uploadMission;
            }
        }

        public ICommand _startMission;
        public ICommand StartMission
        {
            get
            {
                if (_startMission == null)
                {
                    _startMission = new RelayCommand(async delegate ()
                    {
                        var err = await DJISDKManager.Instance.WaypointMissionManager.GetWaypointMissionHandler(0).StartMission();
                        var messageDialog = new MessageDialog(String.Format("Start mission: {0}", err.ToString()));
                        await messageDialog.ShowAsync();
                    }, delegate () { return true; });
                }
                return _startMission;
            }
        }

        public ICommand _startSimulator;
        public ICommand StartSimulator
        {
            get
            {
                if (_startSimulator == null)
                {
                    _startSimulator = new RelayCommand(async delegate ()
                    {
                        try
                        {
                            var latitude = Convert.ToDouble(SimulatorLatitude);
                            var longitude = Convert.ToDouble(SimulatorLongitude);
                            var satelliteCount = Convert.ToInt32(SimulatorSatelliteCount);

                            var err = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).StartSimulatorAsync(new SimulatorInitializationSettings
                            {
                                latitude = latitude,
                                longitude = longitude,
                                satelliteCount = satelliteCount
                            });
                            var messageDialog = new MessageDialog(String.Format("Start Simulator Result: {0}.", err.ToString()));
                            await messageDialog.ShowAsync();
                        }
                        catch
                        {
                            var messageDialog = new MessageDialog("Format error!");
                            await messageDialog.ShowAsync();
                        }
                    }, delegate () { return true; });
                }
                return _startSimulator;
            }
        }

        public ICommand _stopSimulator;
        public ICommand StopSimulator
        {
            get
            {
                if (_stopSimulator == null)
                {
                    _stopSimulator = new RelayCommand(async delegate ()
                    {
                        var err = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).StopSimulatorAsync();
                        var messageDialog = new MessageDialog(String.Format("Stop Simulator Result: {0}.", err.ToString()));
                        await messageDialog.ShowAsync();
                    }, delegate () { return true; });
                }
                return _stopSimulator;
            }
        }
    }
}