using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Build.Logging.StructuredLogger;

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
            MSBuildLocations.Clear();
            foreach (var msbuild in SettingsService.GetRecentMSBuildLocations())
            {
                MSBuildLocations.Add(msbuild);
            }

            if (MSBuildLocations.Count > 0)
            {
                //CollectionViewSource.GetDefaultView(MSBuildLocations).MoveCurrentToFirst();
            }
        }

        public ObservableCollection<string> MSBuildLocations { get; } = new ObservableCollection<string>();

        public string MSBuildLocation => ""; // CollectionViewSource.GetDefaultView(MSBuildLocations).CurrentItem as string;

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