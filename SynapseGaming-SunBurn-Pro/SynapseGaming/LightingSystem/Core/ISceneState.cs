﻿// Decompiled with JetBrains decompiler
// Type: SynapseGaming.LightingSystem.Core.ISceneState
// Assembly: SynapseGaming-SunBurn-Pro, Version=1.3.2.8, Culture=neutral, PublicKeyToken=c23c60523565dbfd
// MVID: A5F03349-72AC-4BAA-AEEE-9AB9B77E0A39
// Assembly location: C:\Projects\BlackBox\StarDrive\SynapseGaming-SunBurn-Pro.dll

using Microsoft.Xna.Framework;

namespace SynapseGaming.LightingSystem.Core
{
  /// <summary>
  /// Interface that provides a base for objects exposing scene, frame, and view data to the the lighting system.
  /// </summary>
  public interface ISceneState
  {
    /// <summary>The scene's current view matrix.</summary>
    Matrix View { get; }

    /// <summary>The scene's inverse view matrix.</summary>
    Matrix ViewToWorld { get; }

    /// <summary>The scene's current projection matrix.</summary>
    Matrix Projection { get; }

    /// <summary>The scene's inverse projection matrix.</summary>
    Matrix ProjectionToView { get; }

    /// <summary>The scene's combined view and projection matrix.</summary>
    Matrix ViewProjection { get; }

    /// <summary>
    /// The scene's combined inverse view and inverse projection matrix.
    /// </summary>
    Matrix ProjectionToWorld { get; }

    /// <summary>The scene's current view frustum.</summary>
    BoundingFrustum ViewFrustum { get; }

    /// <summary>
    /// Indicates the rendering pass is drawing to the screen (or to a
    /// target copied to the screen).
    /// </summary>
    bool RenderingToScreen { get; }

    /// <summary>
    /// Determines if primitive culling mode should be flipped to accommodate
    /// inverted windings caused by mirrored view or projection transforms.
    /// </summary>
    bool InvertedWindings { get; }

    /// <summary>Indicates the projection is 2D.</summary>
    bool OrthographicProjection { get; }

    /// <summary>The scene's current game time.</summary>
    GameTime GameTime { get; }

    /// <summary>The current frame id.</summary>
    int FrameId { get; }

    /// <summary>The scene's current environment.</summary>
    ISceneEnvironment Environment { get; }

    /// <summary />
    void ApplyEditorUpdate(Matrix view, Matrix viewtoworld, Matrix projection);
  }
}
