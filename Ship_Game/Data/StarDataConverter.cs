﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using Microsoft.Xna.Framework.Graphics;

namespace Ship_Game.Data
{
    public abstract class TypeConverter
    {
        public abstract object Convert(object value, Type source);
    }

    public class EnumConverter : TypeConverter
    {
        readonly Type ToEnum;
        public EnumConverter(Type enumType)
        {
            ToEnum = enumType;
        }
        public override object Convert(object value, Type source)
        {
            if (value is string s)
                return Enum.Parse(ToEnum, s, ignoreCase:true);
            if (value is int i)
                return Enum.ToObject(ToEnum, i);
            throw new Exception($"StarDataConverter could not convert '{value}' ({value?.GetType()}) to Enum '{ToEnum.Name}'");
        }
    }

    public class RangeConverter : TypeConverter
    {
        static float Number(object value)
        {
            if (value is float f) return f;
            if (value is int i) return i;
            return float.Parse((string)value, CultureInfo.InvariantCulture);
        }
        public override object Convert(object value, Type source)
        {
            if (value is int i)   return new Range(i);
            if (value is float f) return new Range(f);
            if (!(value is object[] objects) || objects.Length < 2)
                throw new Exception($"StarDataConverter could not convert '{value}' ({value?.GetType()}) to Range");
            return new Range(Number(objects[0]), Number(objects[1]));
        }
    }

    public class LocTextConverter : TypeConverter
    {
        public override object Convert(object value, Type source)
        {
            if (!(value is int id))
                throw new Exception($"StarDataConverter could not convert '{value}' ({value?.GetType()}) to LocText");
            return new LocText(id);
        }
    }

    public class ColorConverter : TypeConverter
    {
        static float ToFloat(object value)
        {
            if (value is int i)   return (float)i;
            if (value is float f) return f;
            throw new Exception($"StarDataConverter could not convert '{value}' ({value?.GetType()}) to Float");
        }

        static byte ToByte(object value)
        {
            if (value is int i)   return (byte)i;
            if (value is float f) return (byte)(int)f;
            throw new Exception($"StarDataConverter could not convert '{value}' ({value?.GetType()}) to Float");
        }

        public override object Convert(object value, Type source)
        {
            if (value is object[] objects)
            {
                if (objects[0] is int)
                {
                    byte r = 255, g = 255, b = 255, a = 255;
                    if (objects.Length >= 1) r = ToByte(objects[0]);
                    if (objects.Length >= 2) g = ToByte(objects[1]);
                    if (objects.Length >= 3) b = ToByte(objects[2]);
                    if (objects.Length >= 4) a = ToByte(objects[3]);
                    return new Color(r, g, b, a);
                }
                else
                {
                    float r = 1f, g = 1f, b = 1f, a = 1f;
                    if (objects.Length >= 1) r = ToFloat(objects[0]);
                    if (objects.Length >= 2) g = ToFloat(objects[1]);
                    if (objects.Length >= 3) b = ToFloat(objects[2]);
                    if (objects.Length >= 4) a = ToFloat(objects[3]);
                    return new Color(r, g, b, a);
                }
            }
            if (value is int i) // short hand to get [i,i,i,i]
            {
                i = i.Clamped(0, 255);
                return new Color((byte)i, (byte)i, (byte)i, (byte)i);
            }
            if (value is float f) // short hand to get [f,f,f,f]
            {
                f = f.Clamped(0f, 1f);
                return new Color(f, f, f, f);
            }
            throw new Exception($"StarDataConverter could not convert '{value}' ({value?.GetType()}) to Color");
        }
    }

    public class IntConverter : TypeConverter
    {
        public override object Convert(object value, Type source)
        {
            if (value is string s)
            {
                int.TryParse(s, out int i);
                return i;
            }

            if (value is float f)
                return (int)f;

            throw new Exception($"StarDataConverter could not convert '{value}' ({value?.GetType()}) to Int");
        }
    }

    public class FloatConverter : TypeConverter
    {
        public override object Convert(object value, Type source)
        {
            if (value is string s)
            {
                float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f);
                return f;
            }

            if (value is int i)
                return (float)i;

            throw new Exception($"StarDataConverter could not convert '{value}' ({value?.GetType()}) to Float");
        }
    }

    public class BoolConverter : TypeConverter
    {
        public override object Convert(object value, Type source)
        {
            if (value is string s)
            {
                return s == "true" || s == "True";
            }

            throw new Exception($"StarDataConverter could not convert '{value}' ({value?.GetType()}) to Bool");
        }
    }

    public class StringConverter : TypeConverter
    {
        public override object Convert(object value, Type source)
        {
            return value.ToString();
        }
    }

    public class DefaultConverter : TypeConverter
    {
        readonly Type ToType;
        public DefaultConverter(Type toType)
        {
            ToType = toType;
        }
        public override object Convert(object value, Type source)
        {
            return System.Convert.ChangeType(value, ToType);
        }
    }

    public static class StarDataConverter
    {
        public static object Convert(object value, Type targetT)
        {
            return System.Convert.ChangeType(value, targetT);
        }
    }
}
