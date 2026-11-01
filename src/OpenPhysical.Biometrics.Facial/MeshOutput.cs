// SPDX-License-Identifier: Apache-2.0
// Adapted from yakhyo/mediapipe-face-mesh-onnx, copyright 2026 Yakhyokhuja Valikhujaev.
namespace OpenPhysical.Biometrics.Facial;

internal sealed record MeshOutput(float[] Values, double Presence);
