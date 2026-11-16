// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Capture;

using CSharpFunctionalExtensions;

/// <summary>Publishes acknowledged frames and fences queued work when capture freezes.</summary>
/// <typeparam name="TFrame">Immutable frame identity owned by the caller.</typeparam>
/// <typeparam name="TError">The caller's typed no-frame error.</typeparam>
/// <param name="noFrame">Failure returned before a frame has been acknowledged.</param>
public sealed class PreviewPublication<TFrame, TError>(TError noFrame)
    where TFrame : class
{
    private readonly object sync = new();
    private Maybe<TFrame> displayed;
    private long generation;
    private bool live;

    /// <summary>Gets the last successfully acknowledged frame.</summary>
    public Maybe<TFrame> Displayed
    {
        get
        {
            lock (sync)
                return displayed;
        }
    }

    /// <summary>Gets the current publication generation.</summary>
    public long Generation
    {
        get
        {
            lock (sync)
                return generation;
        }
    }

    /// <summary>Gets whether publication is enabled.</summary>
    public bool IsLive
    {
        get
        {
            lock (sync)
                return live;
        }
    }

    /// <summary>Starts a fresh generation without retaining a prior presentation.</summary>
    /// <returns>The generation supplied to publication callbacks.</returns>
    public long Begin()
    {
        lock (sync)
        {
            live = true;
            displayed = default;
            return ++generation;
        }
    }

    /// <summary>Stops publication while retaining the acknowledged frame.</summary>
    /// <returns>The new generation.</returns>
    public long Pause()
    {
        lock (sync)
        {
            live = false;
            return ++generation;
        }
    }

    /// <summary>Invalidates queued publications and clears the displayed frame.</summary>
    /// <returns>The new generation.</returns>
    public long Forget()
    {
        lock (sync)
        {
            displayed = default;
            return ++generation;
        }
    }

    /// <summary>Renders and acknowledges only a callback belonging to the active generation.</summary>
    /// <param name="frame">Frame to render.</param>
    /// <param name="expected">Generation captured before scheduling the callback.</param>
    /// <param name="render">Presentation that must succeed before acknowledgement.</param>
    /// <returns>The presentation outcome, or success when stale work is ignored.</returns>
    public UnitResult<TError> Display(
        TFrame frame,
        long expected,
        Func<TFrame, UnitResult<TError>> render
    )
    {
        lock (sync)
        {
            if (!live || generation != expected)
                return UnitResult.Success<TError>();
            var result = render(frame);
            if (result.IsSuccess)
                displayed = frame;
            return result;
        }
    }

    /// <summary>Freezes exactly the last successfully rendered frame.</summary>
    /// <returns>The retained frame or the supplied no-frame error.</returns>
    public Result<TFrame, TError> Freeze()
    {
        lock (sync)
        {
            if (displayed.HasNoValue)
                return noFrame;
            live = false;
            generation++;
            return displayed.Value;
        }
    }
}
