using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using StructuredLogViewer;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class BuildParametersScreen : ObservableObject
    {
        public event Action BuildRequested;
        public event Action CancelRequested;

        public BuildParametersScreen()
        {
            UpdateMSBuildLocations();
        }

        public void SaveSelectedMSBuild()
        {
            SettingsService.AddRecentMSBuildLocation(MSBuildLocation);
        }

        public void UpdateMSBuildLocations()
        {
            MSBuildLocations.Clear();
            foreach (var msbuild in SettingsService.GetRecentMSBuildLocations())
            {
                // our list might have gotten stale, so double-check file existence here
                if (File.Exists(msbuild))
                {
                    MSBuildLocations.Add(msbuild);
                }
            }

            if (MSBuildLocations.Count > 0)
            {
                MSBuildLocation = MSBuildLocations[0];
            }
        }

        public ObservableCollection<string> MSBuildLocations { get; } = new ObservableCollection<string>();

        public string MSBuildLocation
        {
            get => _msBuildLocation;
            set
            {
                _msBuildLocation = value;
                RaisePropertyChanged();
            }
        }

        private string prefixArguments;
        public string PrefixArguments
        {
            get
            {
                return prefixArguments;
            }

            set
            {
                prefixArguments = value;
                RaisePropertyChanged();
            }
        }

        public string MSBuildArguments { get; set; }
        public string PostfixArguments { get; set; }

        private ICommand buildCommand;
        public ICommand BuildCommand => buildCommand ?? (buildCommand = new Command(Build));
        private void Build() => BuildRequested?.Invoke();

        private ICommand cancelCommand;
        public ICommand CancelCommand => cancelCommand ?? (cancelCommand = new Command(Cancel));
        private void Cancel() => CancelRequested?.Invoke();

        private ICommand copyCommand;
        public ICommand CopyCommand => copyCommand ?? (copyCommand = new Command(Copy));
        private void Copy()
        {
            string commandLine = $@"{HostedBuild.QuoteIfNeeded(MSBuildLocation)} {PrefixArguments} {MSBuildArguments} {PostfixArguments}";
            ClipboardService.SetText(commandLine);
        }

        private ICommand browseForMSBuildCommand;
        private string _msBuildLocation;

        public ICommand BrowseForMSBuildCommand => browseForMSBuildCommand ?? (browseForMSBuildCommand = new Command(OnBrowseForMSBuild));

        public event Action BrowseForMSBuild;

        private void OnBrowseForMSBuild()
        {
            BrowseForMSBuild?.Invoke();
            UpdateMSBuildLocations();
        }
    }
}