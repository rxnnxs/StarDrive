﻿// Decompiled with JetBrains decompiler
// Type: ns4.Attribute0
// Assembly: SynapseGaming-SunBurn-Pro, Version=1.3.2.8, Culture=neutral, PublicKeyToken=c23c60523565dbfd
// MVID: A5F03349-72AC-4BAA-AEEE-9AB9B77E0A39
// Assembly location: C:\Projects\BlackBox\StarDrive\SynapseGaming-SunBurn-Pro.dll

using System;

namespace ns4
{
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
  internal class Attribute0 : Attribute
  {
      public bool OnlyMarkedProperties { get; }

      public Attribute0(bool onlymarkedproperties)
    {
      this.OnlyMarkedProperties = onlymarkedproperties;
    }
  }
}
