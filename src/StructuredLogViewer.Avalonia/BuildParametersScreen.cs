using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Build.Logging.StructuredLogger;
using StructuredLogViewer.Core;

namespace StructuredLogViewer.Avalonia.Controls
{
    public class BuildParametersScreen : ObservableObject
    {
        public event Action BuildRequested;
        public event Action CancelRequested;

        public event Func<System.Threading.Tasks.Task> BrowseForMSBuildRequsted;

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
            var msBuildLocations = SettingsService.GetRecentMSBuildLocations(
                DotnetUtilities.GetMsBuildPathCollection().Reverse());
            MSBuildLocations.Clear();
            foreach (var msbuild in msBuildLocations)
            {
                MSBuildLocations.Add(msbuild);
            }

            MSBuildLocation = MSBuildLocations.FirstOrDefault();
        }

        public ObservableCollection<string> MSBuildLocations { get; } = new ObservableCollection<string>();

        private string msBuildLocation;
        public string MSBuildLocation
        {
            get => msBuildLocation;
            set => SetField(ref msBuildLocation, value);
        }

        private string prefixArguments;
        public string PrefixArguments
        {
            get => prefixArguments;
            set => SetField(ref prefixArguments, value);
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
            string commandLine = $@"{MSBuildLocation.QuoteIfNeeded()} {PrefixArguments} {MSBuildArguments} {PostfixArguments}";
            //Clipboard.SetText(commandLine);
        }

        public async System.Threading.Tasks.Task BrowseForMSBuildAsync()
        {
            if (BrowseForMSBuildRequsted is not null)
                await BrowseForMSBuildRequsted.Invoke();
            UpdateMSBuildLocations();
        }
    }
}