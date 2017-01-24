﻿// Decompiled with JetBrains decompiler
// Type: SynapseGaming.LightingSystem.Lights.SpotLight
// Assembly: SynapseGaming-SunBurn-Pro, Version=1.3.2.8, Culture=neutral, PublicKeyToken=c23c60523565dbfd
// MVID: A5F03349-72AC-4BAA-AEEE-9AB9B77E0A39
// Assembly location: C:\Projects\BlackBox\StarDrive\SynapseGaming-SunBurn-Pro.dll

using Microsoft.Xna.Framework;
using ns3;
using SynapseGaming.LightingSystem.Core;
using SynapseGaming.LightingSystem.Shadows;
using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace SynapseGaming.LightingSystem.Lights
{
  /// <summary>
  /// Provides spotlight information for rendering lighting and shadows.
  /// </summary>
  [Serializable]
  public class SpotLight : IMovableObject, INamedObject, IEditorObject, ISerializable, ILight, IPointSource, IDirectionalSource, ISpotSource, IShadowSource
  {
    private bool bool_0 = true;
    private Vector3 vector3_0 = new Vector3(0.7f, 0.6f, 0.5f);
    private float float_1 = 1f;
    private float float_2 = 0.5f;
    private float float_3 = 1f;
    private float float_4 = 0.2f;
    private bool bool_2 = true;
    private float float_5 = 10f;
    private float float_6 = 45f;
    private Matrix matrix_0 = Matrix.Identity;
    private string string_0 = "";
    private bool bool_1;
    private int int_0;
    private ObjectType objectType_0;
    private float float_0;
    private ShadowType shadowType_0;
    private float float_7;
    private IShadowSource ishadowSource_0;
    private BoundingBox boundingBox_0;
    private BoundingSphere boundingSphere_0;

    /// <summary>
    /// Turns illumination on and off without removing the light from the scene.
    /// </summary>
    public bool Enabled
    {
      get
      {
        return this.bool_0;
      }
      set
      {
        this.bool_0 = value;
      }
    }

    /// <summary>Direct lighting color given off by the light.</summary>
    public Vector3 DiffuseColor
    {
      get
      {
        return this.vector3_0;
      }
      set
      {
        this.vector3_0 = value;
      }
    }

    /// <summary>Intensity of the light.</summary>
    public float Intensity
    {
      get
      {
        return this.float_1;
      }
      set
      {
        this.float_1 = value;
      }
    }

    /// <summary>
    /// Provides softer indirect-like illumination without "hot-spots".
    /// </summary>
    public bool FillLight
    {
      get
      {
        return this.bool_1;
      }
      set
      {
        this.bool_1 = value;
      }
    }

    /// <summary>
    /// Controls how quickly lighting falls off over distance (only available in deferred rendering).
    /// Value ranges from 0.0f to 1.0f.
    /// </summary>
    public float FalloffStrength
    {
      get
      {
        return this.float_0;
      }
      set
      {
        this.float_0 = MathHelper.Clamp(value, 0.0f, 1f);
      }
    }

    /// <summary>
    /// The combined light color and intensity (provided for convenience).
    /// </summary>
    public Vector3 CompositeColorAndIntensity
    {
      get
      {
        return this.vector3_0 * this.float_1;
      }
    }

    /// <summary>Bounding area of the light's influence.</summary>
    public BoundingBox WorldBoundingBox
    {
      get
      {
        return this.boundingBox_0;
      }
    }

    /// <summary>Bounding area of the light's influence.</summary>
    public BoundingSphere WorldBoundingSphere
    {
      get
      {
        return this.boundingSphere_0;
      }
    }

    /// <summary>
    /// Shadow source the light's shadows are generated from.
    /// Allows sharing shadows between point light sources.
    /// </summary>
    public IShadowSource ShadowSource
    {
      get
      {
        if (this.ishadowSource_0 == null)
          throw new ArgumentException("ShadowSource is null. This can result in poor rendering performance.");
        return this.ishadowSource_0;
      }
      set
      {
        if (value == null)
          this.ishadowSource_0 = (IShadowSource) this;
        else
          this.ishadowSource_0 = value;
      }
    }

    /// <summary>
    /// Defines the type of objects that cast shadows from the light.
    /// Does not affect an object's ability to receive shadows.
    /// </summary>
    public ShadowType ShadowType
    {
      get
      {
        return this.shadowType_0;
      }
      set
      {
        this.shadowType_0 = value;
      }
    }

    /// <summary>Position in world space of the shadow source.</summary>
    public Vector3 ShadowPosition
    {
      get
      {
        return this.matrix_0.Translation;
      }
    }

    /// <summary>Adjusts the visual quality of casts shadows.</summary>
    public float ShadowQuality
    {
      get
      {
        return this.float_2;
      }
      set
      {
        this.float_2 = MathHelper.Clamp(value, 0.0f, 1f);
      }
    }

    /// <summary>Main property used to eliminate shadow artifacts.</summary>
    public float ShadowPrimaryBias
    {
      get
      {
        return this.float_3;
      }
      set
      {
        this.float_3 = value;
      }
    }

    /// <summary>
    /// Additional fine-tuned property used to eliminate shadow artifacts.
    /// </summary>
    public float ShadowSecondaryBias
    {
      get
      {
        return this.float_4;
      }
      set
      {
        this.float_4 = value;
      }
    }

    /// <summary>
    /// Enables independent level-of-detail per cubemap face on point-based lights.
    /// </summary>
    public bool ShadowPerSurfaceLOD
    {
      get
      {
        return this.bool_2;
      }
      set
      {
        this.bool_2 = value;
      }
    }

    /// <summary>Unused.</summary>
    public bool ShadowRenderLightsTogether
    {
      get
      {
        return false;
      }
    }

    /// <summary>Position in world space of the light.</summary>
    public Vector3 Position
    {
      get
      {
        return this.matrix_0.Translation;
      }
      set
      {
        this.matrix_0.Translation = value;
        ++this.int_0;
        this.method_0();
      }
    }

    /// <summary>
    /// Maximum distance in world space of the light's influence.
    /// </summary>
    public float Radius
    {
      get
      {
        return this.float_5;
      }
      set
      {
        this.float_5 = value;
        this.method_0();
      }
    }

    /// <summary>Direction in world space of the light's influence.</summary>
    public Vector3 Direction
    {
      get
      {
        return this.matrix_0.Forward;
      }
      set
      {
        Matrix matrix = Matrix.Identity;
        if (value != Vector3.Zero)
          matrix = Class13.smethod_14(Vector3.Forward, Vector3.Normalize(value));
        matrix.Translation = this.matrix_0.Translation;
        this.matrix_0 = matrix;
        this.method_0();
      }
    }

    /// <summary>Angle in degrees of the light's influence.</summary>
    public float Angle
    {
      get
      {
        return this.float_6;
      }
      set
      {
        this.float_6 = value;
        this.method_0();
      }
    }

    /// <summary>Intensity of the light's 3D light beam.</summary>
    public float Volume
    {
      get
      {
        return this.float_7;
      }
      set
      {
        this.float_7 = value;
      }
    }

    /// <summary>World space transform of the light.</summary>
    public Matrix World
    {
      get
      {
        return this.matrix_0;
      }
      set
      {
        this.matrix_0 = value;
        ++this.int_0;
        this.method_0();
      }
    }

    /// <summary>
    /// Indicates the object bounding area spans the entire world and
    /// the object is always visible.
    /// </summary>
    public bool InfiniteBounds
    {
      get
      {
        return false;
      }
    }

    /// <summary>
    /// Indicates the current move. This value increments each time the object
    /// is moved (when the World transform changes).
    /// </summary>
    public int MoveId
    {
      get
      {
        return this.int_0;
      }
    }

    /// <summary>
    /// Defines how movement is applied. Updates to Dynamic objects
    /// are automatically applied, where Static objects must be moved
    /// manually using [manager].Move().
    /// 
    /// Important note: ObjectType can be changed at any time, HOWEVER managers
    /// will only see the change after removing and resubmitting the object.
    /// </summary>
    public ObjectType ObjectType
    {
      get
      {
        return this.objectType_0;
      }
      set
      {
        this.objectType_0 = value;
      }
    }

    /// <summary>The object's current name.</summary>
    public string Name
    {
      get
      {
        return this.string_0;
      }
      set
      {
        this.string_0 = value;
      }
    }

    /// <summary>
    /// Notifies the editor that this object is partially controlled via code. The editor
    /// will display information to the user indicating some property values are
    /// overridden in code and changes may not take effect.
    /// </summary>
    public bool AffectedInCode { get; set; }

    /// <summary>Creates a new SpotLight instance.</summary>
    public SpotLight()
    {
      this.ishadowSource_0 = (IShadowSource) this;
      this.method_0();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    protected SpotLight(SerializationInfo serializationInfo_0, StreamingContext streamingContext_0)
    {
      Vector3 gparam_0 = new Vector3();
      foreach (SerializationEntry serializationEntry in serializationInfo_0)
      {
        switch (serializationEntry.Name)
        {
          case "Enabled":
            Class28.smethod_0<bool>(ref this.bool_0, serializationInfo_0, "Enabled");
            continue;
          case "DiffuseColor":
            Class28.smethod_0<Vector3>(ref this.vector3_0, serializationInfo_0, "DiffuseColor");
            continue;
          case "Intensity":
            Class28.smethod_0<float>(ref this.float_1, serializationInfo_0, "Intensity");
            continue;
          case "FillLight":
            Class28.smethod_0<bool>(ref this.bool_1, serializationInfo_0, "FillLight");
            continue;
          case "FalloffStrength":
            Class28.smethod_0<float>(ref this.float_0, serializationInfo_0, "FalloffStrength");
            continue;
          case "ShadowType":
            Class28.smethod_1<ShadowType>(ref this.shadowType_0, serializationInfo_0, "ShadowType");
            continue;
          case "Position":
            Class28.smethod_0<Vector3>(ref gparam_0, serializationInfo_0, "Position");
            this.Position = gparam_0;
            continue;
          case "Radius":
            Class28.smethod_0<float>(ref this.float_5, serializationInfo_0, "Radius");
            continue;
          case "Direction":
            Class28.smethod_0<Vector3>(ref gparam_0, serializationInfo_0, "Direction");
            this.Direction = gparam_0;
            continue;
          case "Angle":
            Class28.smethod_0<float>(ref this.float_6, serializationInfo_0, "Angle");
            continue;
          case "Volume":
            Class28.smethod_0<float>(ref this.float_7, serializationInfo_0, "Volume");
            continue;
          case "Name":
            Class28.smethod_0<string>(ref this.string_0, serializationInfo_0, "Name");
            continue;
          case "ShadowQuality":
            Class28.smethod_0<float>(ref this.float_2, serializationInfo_0, "ShadowQuality");
            continue;
          case "ShadowPrimaryBias":
            Class28.smethod_0<float>(ref this.float_3, serializationInfo_0, "ShadowPrimaryBias");
            continue;
          case "ShadowSecondaryBias":
            Class28.smethod_0<float>(ref this.float_4, serializationInfo_0, "ShadowSecondaryBias");
            continue;
          case "ShadowPerSurfaceLOD":
            Class28.smethod_0<bool>(ref this.bool_2, serializationInfo_0, "ShadowPerSurfaceLOD");
            continue;
          default:
            continue;
        }
      }
      this.method_0();
    }

    private void method_0()
    {
      float num = (float) Math.Tanh((double) MathHelper.ToRadians(MathHelper.Clamp(this.float_6, 1f / 1000f, 179.99f) * 0.5f)) * this.float_5;
      this.boundingBox_0 = Class13.smethod_5(new BoundingBox(new Vector3(-num, -num, -this.float_5), new Vector3(num, num, 0.0f)), this.matrix_0);
      this.boundingSphere_0 = BoundingSphere.CreateFromBoundingBox(this.boundingBox_0);
    }

    /// <summary>
    /// Returns a hash code that uniquely identifies the shadow source
    /// and its current state.  Changes to ShadowPosition affects the
    /// hash code, which is used to trigger updates on related shadows.
    /// </summary>
    /// <returns>Shadow hash code.</returns>
    public int GetShadowSourceHashCode()
    {
      return this.ShadowPosition.GetHashCode();
    }

    /// <summary>Returns a String that represents the current Object.</summary>
    /// <returns></returns>
    public override string ToString()
    {
      return Class13.smethod_2((INamedObject) this);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="info"></param>
    /// <param name="context"></param>
    [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      info.AddValue("Name", (object) this.Name);
      info.AddValue("Enabled", this.Enabled);
      info.AddValue("DiffuseColor", (object) this.DiffuseColor);
      info.AddValue("Intensity", this.Intensity);
      info.AddValue("FillLight", this.FillLight);
      info.AddValue("FalloffStrength", this.FalloffStrength);
      info.AddValue("ShadowType", (object) this.ShadowType);
      info.AddValue("Position", (object) this.Position);
      info.AddValue("Radius", this.Radius);
      info.AddValue("Direction", (object) this.Direction);
      info.AddValue("Angle", this.Angle);
      info.AddValue("Volume", this.Volume);
      info.AddValue("ShadowQuality", this.ShadowQuality);
      info.AddValue("ShadowPrimaryBias", this.ShadowPrimaryBias);
      info.AddValue("ShadowSecondaryBias", this.ShadowSecondaryBias);
      info.AddValue("ShadowPerSurfaceLOD", this.ShadowPerSurfaceLOD);
    }
  }
}
