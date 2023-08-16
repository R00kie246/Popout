﻿using System;

namespace MSFSPopoutPanelManager.Shared
{
    public static class WorkflowStepWithMessage
    {
        public static void Execute(string message, Func<bool> function, bool isSubTask = false)
        {
            if (isSubTask)
                message = "           " + message;

            StatusMessageWriter.WriteMessage(message, StatusMessageType.Info);
            StatusMessageWriter.WriteExecutingStatusMessage();

            var result = function.Invoke();

            StatusMessageWriter.RemoveLastMessage();
            if (result)
                StatusMessageWriter.WriteOkStatusMessage();
            else
                StatusMessageWriter.WriteFailureStatusMessage();
        }

        public static void Execute(string message, Action function, bool isSubTask = false)
        {
            if (isSubTask)
                message = "           " + message;

            StatusMessageWriter.WriteMessage(message, StatusMessageType.Info);
            StatusMessageWriter.WriteExecutingStatusMessage();

            function.Invoke();

            StatusMessageWriter.RemoveLastMessage();
            StatusMessageWriter.WriteOkStatusMessage();
        }
    }
}
