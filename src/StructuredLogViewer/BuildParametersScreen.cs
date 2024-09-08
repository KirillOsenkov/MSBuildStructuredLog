using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
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
            foreach (var msbuild in SettingsService.GetRecentMSBuildLocations(MSBuildLocator.GetMSBuildLocations()))
            {
                MSBuildLocations.Add(msbuild);
            }

            if (MSBuildLocations.Count > 0)
            {
                CollectionViewSource.GetDefaultView(MSBuildLocations).MoveCurrentToFirst();
            }
        }

        public ObservableCollection<string> MSBuildLocations { get; } = new ObservableCollection<string>();

        public string MSBuildLocation => CollectionViewSource.GetDefaultView(MSBuildLocations).CurrentItem as string;

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
            Clipboard.SetText(commandLine);
        }

        private ICommand browseForMSBuildCommand;
        public ICommand BrowseForMSBuildCommand => browseForMSBuildCommand ?? (browseForMSBuildCommand = new Command(BrowseForMSBuild));
        private void BrowseForMSBuild()
        {
            MSBuildLocator.BrowseForMSBuildExe();
            UpdateMSBuildLocations();
        }
    }
}
