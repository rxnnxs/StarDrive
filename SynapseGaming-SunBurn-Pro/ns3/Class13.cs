﻿// Decompiled with JetBrains decompiler
// Type: ns3.Class13
// Assembly: SynapseGaming-SunBurn-Pro, Version=1.3.2.8, Culture=neutral, PublicKeyToken=c23c60523565dbfd
// MVID: A5F03349-72AC-4BAA-AEEE-9AB9B77E0A39
// Assembly location: C:\Projects\BlackBox\StarDrive\SynapseGaming-SunBurn-Pro.dll

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SynapseGaming.LightingSystem.Core;
using System;

namespace ns3
{
  internal class Class13
  {
    private static float[] float_0 = new float[4];
    private static BoundingFrustum boundingFrustum_0 = new BoundingFrustum(Matrix.Identity);
    private static Vector3[] vector3_0 = new Vector3[8];
    private static float float_1 = 1f / (float) Math.Log(2.0);

    public static float smethod_0(float float_2)
    {
      return (float) Math.Log((double) float_2) * Class13.float_1;
    }

    public static void smethod_1(Vector3 vector3_1, float float_2, float float_3, out Vector3 vector3_2, out Vector3 vector3_3)
    {
      float num1 = 1f + float_2;
      float num2 = 1f - float_2;
      vector3_2 = vector3_1 * 2f * new Vector3(num1, 1f, num2) * float_3;
      vector3_3 = vector3_1 * 2f * new Vector3(num2, 1f, num1) * (1f - float_3);
    }

    public static string smethod_2(INamedObject inamedObject_0)
    {
      Type type = inamedObject_0.GetType();
      return string.Format("\"{0}\" - {1}", (object) (inamedObject_0.Name ?? string.Empty), (object) type.Name);
    }

    public static Vector3 smethod_3(Vector3 vector3_1, int int_0, float float_2)
    {
      Class13.float_0[0] = vector3_1.X;
      Class13.float_0[1] = vector3_1.Y;
      Class13.float_0[2] = vector3_1.Z;
      Class13.float_0[int_0] = float_2;
      return new Vector3(Class13.float_0[0], Class13.float_0[1], Class13.float_0[2]);
    }

    public static Vector3 smethod_4(Vector3 vector3_1, int int_0, int int_1, int int_2)
    {
      Class13.float_0[0] = vector3_1.X;
      Class13.float_0[1] = vector3_1.Y;
      Class13.float_0[2] = vector3_1.Z;
      return new Vector3(Class13.float_0[int_0], Class13.float_0[int_1], Class13.float_0[int_2]);
    }

    public static BoundingBox smethod_5(BoundingBox boundingBox_0, Matrix matrix_0)
    {
      boundingBox_0.GetCorners(Class13.vector3_0);
      for (int index = 0; index < Class13.vector3_0.Length; ++index)
        Class13.vector3_0[index] = Vector3.Transform(Class13.vector3_0[index], matrix_0);
      return Class13.smethod_11(Class13.vector3_0);
    }

    public static BoundingSphere smethod_6(BoundingSphere boundingSphere_0, Matrix matrix_0)
    {
      Vector3 scale;
      Quaternion rotation;
      Vector3 translation;
      matrix_0.Decompose(out scale, out rotation, out translation);
      float num = Math.Max(Math.Abs(scale.X), Math.Max(Math.Abs(scale.Y), Math.Abs(scale.Z)));
      boundingSphere_0.Center = Vector3.Transform(boundingSphere_0.Center, matrix_0);
      boundingSphere_0.Radius *= num;
      return boundingSphere_0;
    }

    public static BoundingBox smethod_7(BoundingBox boundingBox_0, float float_2)
    {
      Vector3 vector3_1 = (boundingBox_0.Max - boundingBox_0.Min) * 0.5f;
      Vector3 vector3_2 = boundingBox_0.Max - vector3_1;
      Vector3 vector3_3 = vector3_1 * float_2;
      return new BoundingBox(vector3_2 - vector3_3, vector3_2 + vector3_3);
    }

    public static BoundingSphere smethod_8(BoundingSphere boundingSphere_0, float float_2)
    {
      BoundingSphere boundingSphere = boundingSphere_0;
      boundingSphere.Radius *= float_2;
      return boundingSphere;
    }

    public static Vector3 smethod_9(Vector3 vector3_1, Plane plane_0)
    {
      float num = plane_0.DotCoordinate(vector3_1);
      return vector3_1 - plane_0.Normal * num;
    }

    public static bool smethod_10(Vector3 vector3_1, Vector3 vector3_2, Plane plane_0, ref Vector3 vector3_3)
    {
      Vector3 vector2 = vector3_2 - vector3_1;
      float num1 = Vector3.Dot(plane_0.Normal, vector2);
      if ((double) num1 == 0.0)
        return false;
      float num2 = (float) -((double) Vector3.Dot(plane_0.Normal, vector3_1) + (double) plane_0.D) / num1;
      if ((double) num2 < 0.0 || (double) num2 > 1.0)
        return false;
      vector3_3 = vector3_1 + num2 * vector2;
      return true;
    }

    public static BoundingBox smethod_11(Vector3[] vector3_1)
    {
      if (vector3_1.Length < 1)
        return new BoundingBox();
      BoundingBox boundingBox = new BoundingBox(vector3_1[0], vector3_1[0]);
      for (int index = 1; index < vector3_1.Length; ++index)
      {
        boundingBox.Max = Vector3.Max(boundingBox.Max, vector3_1[index]);
        boundingBox.Min = Vector3.Min(boundingBox.Min, vector3_1[index]);
      }
      return boundingBox;
    }

    public static Plane smethod_12(Vector3 vector3_1, Vector3 vector3_2)
    {
      Vector3 vector2 = Vector3.Normalize(vector3_1);
      Plane plane;
      plane.Normal = vector2;
      plane.D = -Vector3.Dot(vector3_2, vector2);
      return plane;
    }

    public static Matrix smethod_13(Vector3 vector3_1, Vector3 vector3_2, Vector3 vector3_3, Vector3 vector3_4)
    {
      return new Matrix(vector3_1.X, vector3_1.Y, vector3_1.Z, 0.0f, vector3_2.X, vector3_2.Y, vector3_2.Z, 0.0f, vector3_3.X, vector3_3.Y, vector3_3.Z, 0.0f, vector3_4.X, vector3_4.Y, vector3_4.Z, 1f);
    }

    public static Matrix smethod_14(Vector3 vector3_1, Vector3 vector3_2)
    {
      float w = 1f + Vector3.Dot(vector3_1, vector3_2);
      Quaternion quaternion;
      if ((double) w < 0.0001)
      {
        quaternion = new Quaternion(vector3_1.Z, vector3_1.X, vector3_1.Y, 0.0f);
      }
      else
      {
        Vector3 vector3 = Vector3.Cross(vector3_1, vector3_2);
        quaternion = new Quaternion(vector3.X, vector3.Y, vector3.Z, w);
      }
      quaternion.Normalize();
      return Matrix.CreateFromQuaternion(quaternion);
    }

    public static Matrix smethod_15(Vector3 vector3_1, Vector3 vector3_2, Vector3 vector3_3)
    {
      float num = MathHelper.Clamp(Vector3.Dot(vector3_1, vector3_2), 0.0f, 1f);
      return Matrix.CreateFromQuaternion(Quaternion.CreateFromAxisAngle(vector3_3, (float) Math.Acos((double) num)));
    }

    public static int smethod_16(int int_0, int int_1)
    {
      return (int_0 ^ 1) + (int_1 ^ 2);
    }

    public static int smethod_17(int int_0, int int_1, int int_2)
    {
      return (int_0 ^ 1) + (int_1 ^ 2) + (int_2 ^ 3);
    }

    public static int smethod_18(int int_0, int int_1, int int_2, int int_3)
    {
      return (int_0 ^ 1) + (int_1 ^ 2) + (int_2 ^ 3) + (int_3 ^ 4);
    }

    public static Vector3 smethod_19(Quaternion quaternion_0)
    {
      float w = quaternion_0.W;
      float y = quaternion_0.Y;
      float x = quaternion_0.X;
      float z = quaternion_0.Z;
      return new Vector3() { X = (float) Math.Atan2(2.0 * ((double) w * (double) y + (double) x * (double) z), 1.0 - 2.0 * (Math.Pow((double) y, 2.0) + Math.Pow((double) x, 2.0))), Y = (float) Math.Asin(2.0 * ((double) w * (double) x - (double) z * (double) y)), Z = (float) Math.Atan2(2.0 * ((double) w * (double) z + (double) y * (double) x), 1.0 - 2.0 * (Math.Pow((double) x, 2.0) + Math.Pow((double) z, 2.0))) };
    }

    public static Vector3 smethod_20(Vector3 vector3_1, Vector3 vector3_2, Vector3 vector3_3)
    {
      float num1 = Vector3.DistanceSquared(vector3_2, vector3_3);
      if ((double) num1 <= 0.0)
        return vector3_2;
      float num2 = MathHelper.Clamp(Vector3.Dot(vector3_1 - vector3_2, vector3_3 - vector3_2) / num1, 0.0f, 1f);
      return vector3_2 + num2 * (vector3_3 - vector3_2);
    }

    public static bool smethod_21(Plane plane_0, Plane plane_1, Plane plane_2, out Vector3 vector3_1)
    {
      Vector3 vector2 = Vector3.Cross(plane_1.Normal, plane_2.Normal);
      float num = Vector3.Dot(plane_0.Normal, vector2);
      if ((double) num == 0.0)
      {
        vector3_1 = new Vector3();
        return false;
      }
      vector3_1 = plane_0.D * vector2 + plane_1.D * Vector3.Cross(plane_2.Normal, plane_0.Normal) + plane_2.D * Vector3.Cross(plane_0.Normal, plane_1.Normal);
      vector3_1 /= -num;
      return true;
    }

    public static float smethod_22(float float_2, float float_3, Matrix matrix_0)
    {
      Vector4 vector4 = Vector4.Transform(new Vector4(float_2, float_2, -float_3, 1f), matrix_0);
      if ((double) vector4.W <= 0.0)
        return 0.0f;
      vector4 /= vector4.W;
      float num = Math.Max(Math.Abs(vector4.X), Math.Abs(vector4.Y));
      if ((double) num <= 0.0)
        return 0.0f;
      return num;
    }

    public static float smethod_23(float float_2, Matrix matrix_0, Matrix matrix_1, Matrix matrix_2)
    {
      Vector4 vector = Vector4.Transform(new Vector4(matrix_0.Translation, 1f), matrix_1);
      vector.X = float_2;
      vector.Y = float_2;
      Vector4 vector4 = Vector4.Transform(vector, matrix_2);
      if ((double) vector4.W <= 0.0)
        return 0.0f;
      vector4 /= vector4.W;
      float num = Math.Max(Math.Abs(vector4.X), Math.Abs(vector4.Y));
      if ((double) num <= 0.0)
        return 0.0f;
      return num;
    }

    public static float smethod_24(Matrix matrix_0, Matrix matrix_1, Matrix matrix_2)
    {
      float num = Class13.smethod_23(200f, matrix_0, matrix_1, matrix_2);
      if ((double) num > 0.0)
        return 1f / num;
      return 0.0f;
    }

    public static bool smethod_25(Vector3 vector3_1, Viewport viewport_0, Matrix matrix_0, Matrix matrix_1, Matrix matrix_2, Matrix matrix_3, ref Vector3 vector3_2)
    {
      if ((double) vector3_1.X < (double) viewport_0.X || (double) vector3_1.X - (double) viewport_0.X > (double) viewport_0.Width || ((double) vector3_1.Y < (double) viewport_0.Y || (double) vector3_1.Y - (double) viewport_0.Y > (double) viewport_0.Height))
        return false;
      vector3_2 = viewport_0.Unproject(vector3_1, matrix_3, matrix_1, matrix_0);
      Class13.boundingFrustum_0.Matrix = matrix_1 * matrix_3;
      if ((double) Class13.boundingFrustum_0.Near.DotCoordinate(vector3_2) > 0.0)
        vector3_2 = matrix_2.Translation - vector3_2 - matrix_2.Translation;
      return true;
    }

    public static bool smethod_26(Vector3 vector3_1, Viewport viewport_0, Matrix matrix_0, Matrix matrix_1, Matrix matrix_2, Matrix matrix_3, ref Vector3 vector3_2)
    {
      if ((double) Class13.smethod_12(matrix_2.Forward, matrix_2.Translation).DotCoordinate(vector3_1) <= 0.0)
        return false;
      vector3_2 = viewport_0.Project(vector3_1, matrix_3, matrix_1, matrix_0);
      return true;
    }

    public static Rectangle smethod_27(BoundingBox boundingBox_0, Viewport viewport_0, Matrix matrix_0, Matrix matrix_1)
    {
      if (boundingBox_0.Contains(matrix_1.Translation) == ContainmentType.Contains)
        return new Rectangle(viewport_0.X, viewport_0.Y, viewport_0.Width, viewport_0.Height);
      boundingBox_0.GetCorners(Class13.vector3_0);
      for (int index = 0; index < 8; ++index)
      {
        Vector4 vector4 = Vector4.Transform(Class13.vector3_0[index], matrix_0);
        if ((double) vector4.W == 0.0)
          return new Rectangle(viewport_0.X, viewport_0.Y, viewport_0.Width, viewport_0.Height);
        if ((double) vector4.W < 0.0)
          vector4 /= vector4.W * -0.5f;
        else
          vector4 /= vector4.W;
        vector4.Y *= -1f;
        vector4 = vector4 * 0.5f + Vector4.One * 0.5f;
        Class13.vector3_0[index] = Vector3.Clamp(new Vector3(vector4.X, vector4.Y, vector4.Z), Vector3.Zero, Vector3.One) * new Vector3((float) viewport_0.Width, (float) viewport_0.Height, 0.0f);
      }
      BoundingBox boundingBox = Class13.smethod_11(Class13.vector3_0);
      return new Rectangle(viewport_0.X + (int) boundingBox.Min.X, viewport_0.Y + (int) boundingBox.Min.Y, (int) boundingBox.Max.X - (int) boundingBox.Min.X, (int) boundingBox.Max.Y - (int) boundingBox.Min.Y);
    }

    public static Texture2D smethod_28(GraphicsDevice graphicsDevice_0, Texture2D texture2D_0)
    {
      if (texture2D_0 == null || texture2D_0.Format != SurfaceFormat.Color)
        return texture2D_0;
      int width = texture2D_0.Width;
      int height = texture2D_0.Height;
      byte[] data1 = new byte[width * height * 4];
      byte[] data2 = new byte[width * height];
      Texture2D texture2D = new Texture2D(graphicsDevice_0, width, height, texture2D_0.LevelCount, texture2D_0.TextureUsage, SurfaceFormat.Luminance8);
      for (int level = 0; level < texture2D_0.LevelCount; ++level)
      {
        texture2D_0.GetData<byte>(level, new Rectangle?(), data1, 0, width * height * 4);
        int index1 = 0;
        int num = 0;
        for (int index2 = 0; index2 < height; ++index2)
        {
          for (int index3 = 0; index3 < width; ++index3)
          {
            data2[num++] = data1[index1];
            index1 += 4;
          }
        }
        texture2D.SetData<byte>(level, new Rectangle?(), data2, 0, width * height, SetDataOptions.None);
        int val1_1 = width / 2;
        int val1_2 = height / 2;
        width = Math.Max(val1_1, 1);
        height = Math.Max(val1_2, 1);
      }
      return texture2D;
    }
  }
}
