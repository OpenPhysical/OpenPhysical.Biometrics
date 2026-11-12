// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Desktop.Tests;

using System.ComponentModel;

internal static class CaptureWorkflowWait
{
    internal static async Task<bool> UntilAsync(CaptureViewModel view, Func<bool> ready)
    {
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        PropertyChangedEventHandler changed = (_, _) =>
        {
            if (ready())
                completion.TrySetResult(true);
        };
        view.PropertyChanged += changed;
        try
        {
            if (ready())
                completion.TrySetResult(true);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            view.PropertyChanged -= changed;
        }
    }
}
