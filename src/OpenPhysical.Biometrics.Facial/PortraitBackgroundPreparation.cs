// SPDX-License-Identifier: GPL-2.0-or-later
namespace OpenPhysical.Biometrics.Facial;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

internal sealed class PortraitBackgroundPreparation(ILogger logger) : IDisposable
{
    private readonly PortraitSegmenter segmenter = new(logger);

    internal Result<PortraitAnalysis, FacialError> Prepare(PortraitAnalysis analysis) =>
        analysis.PersonMask.HasValue || analysis.Faces.Length != 1
            ? analysis.Succeed()
            : segmenter
                .Segment(analysis.Image)
                .Map(mask => new PortraitAnalysis(analysis.Image, analysis.Faces)
                {
                    PersonMask = mask,
                });

    public void Dispose() => segmenter.Dispose();
}
