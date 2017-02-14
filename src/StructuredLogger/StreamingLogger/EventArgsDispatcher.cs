using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class EventArgsDispatcher : IEventSource
    {
        public event AnyEventHandler AnyEventRaised;
        public event BuildFinishedEventHandler BuildFinished;
        public event BuildStartedEventHandler BuildStarted;
        public event CustomBuildEventHandler CustomEventRaised;
        public event BuildErrorEventHandler ErrorRaised;
        public event BuildMessageEventHandler MessageRaised;
        public event ProjectFinishedEventHandler ProjectFinished;
        public event ProjectStartedEventHandler ProjectStarted;
        public event BuildStatusEventHandler StatusEventRaised;
        public event TargetFinishedEventHandler TargetFinished;
        public event TargetStartedEventHandler TargetStarted;
        public event TaskFinishedEventHandler TaskFinished;
        public event TaskStartedEventHandler TaskStarted;
        public event BuildWarningEventHandler WarningRaised;

        public void Dispatch(BuildEventArgs buildEvent)
        {
            if (buildEvent is BuildMessageEventArgs)
            {
                MessageRaised?.Invoke(null, (BuildMessageEventArgs)buildEvent);
            }
            else if (buildEvent is TaskStartedEventArgs)
            {
                TaskStarted?.Invoke(null, (TaskStartedEventArgs)buildEvent);
            }
            else if (buildEvent is TaskFinishedEventArgs)
            {
                TaskFinished?.Invoke(null, (TaskFinishedEventArgs)buildEvent);
            }
            else if (buildEvent is TargetStartedEventArgs)
            {
                TargetStarted?.Invoke(null, (TargetStartedEventArgs)buildEvent);
            }
            else if (buildEvent is TargetFinishedEventArgs)
            {
                TargetFinished?.Invoke(null, (TargetFinishedEventArgs)buildEvent);
            }
            else if (buildEvent is ProjectStartedEventArgs)
            {
                ProjectStarted?.Invoke(null, (ProjectStartedEventArgs)buildEvent);
            }
            else if (buildEvent is ProjectFinishedEventArgs)
            {
                ProjectFinished?.Invoke(null, (ProjectFinishedEventArgs)buildEvent);
            }
            else if (buildEvent is BuildStartedEventArgs)
            {
                BuildStarted?.Invoke(null, (BuildStartedEventArgs)buildEvent);
            }
            else if (buildEvent is BuildFinishedEventArgs)
            {
                BuildFinished?.Invoke(null, (BuildFinishedEventArgs)buildEvent);
            }
            else if (buildEvent is CustomBuildEventArgs)
            {
                CustomEventRaised?.Invoke(null, (CustomBuildEventArgs)buildEvent);
            }
            else if (buildEvent is BuildStatusEventArgs)
            {
                StatusEventRaised?.Invoke(null, (BuildStatusEventArgs)buildEvent);
            }
            else if (buildEvent is BuildWarningEventArgs)
            {
                WarningRaised?.Invoke(null, (BuildWarningEventArgs)buildEvent);
            }
            else if (buildEvent is BuildErrorEventArgs)
            {
                ErrorRaised?.Invoke(null, (BuildErrorEventArgs)buildEvent);
            }

            AnyEventRaised?.Invoke(null, buildEvent);
        }
    }
}
