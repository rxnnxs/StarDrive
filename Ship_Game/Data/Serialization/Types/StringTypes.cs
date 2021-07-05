﻿using System;
using System.IO;
using System.Text;

namespace Ship_Game.Data.Serialization.Types
{
    internal class StringSerializer : TypeSerializer
    {
        public override string ToString() => "StringSerializer";

        public override object Convert(object value)
        {
            return value?.ToString();
        }
        
        public override void Serialize(TextSerializerContext context, object obj)
        {
            string text = obj as string ?? "";
            WriteString(context, text);
        }

        public static void WriteString(TextSerializerContext context, string text)
        {
            StringBuilder sb = context.Buffer;
            sb.Clear();
            Yaml.YamlNode.EscapeString(sb, text);
            context.Writer.Write(sb.ToString());
        }

        public override void Serialize(BinaryWriter writer, object obj)
        {
            string value = (string)obj;
            writer.Write(value);
        }

        public override object Deserialize(BinaryReader reader)
        {
            string value = reader.ReadString();
            return value;
        }
    }

    internal class LocalizedTextSerializer : TypeSerializer
    {
        public override string ToString() => "LocalizedTextSerializer";

        public override object Convert(object value)
        {
            if (value is int id)   return new LocalizedText(id);
            if (value is string s)
            {
                // this is sort of a pre-optimization
                // only set Parse if text contains {id} token bracket
                if (s.IndexOf('{') != -1)
                    return new LocalizedText(s, LocalizationMethod.Parse);
                return new LocalizedText(s, LocalizationMethod.RawText);
            }
            Error(value, "LocalizedText -- expected int or format string");
            return new LocalizedText("INVALID TEXT", LocalizationMethod.RawText);
        }

        public override void Serialize(TextSerializerContext context, object obj)
        {
            var lt = (LocalizedText)obj;
            switch (lt.Method)
            {
                case LocalizationMethod.Id:
                    context.Writer.Write(lt.Id);
                    break;
                case LocalizationMethod.NameId:
                    context.Writer.Write(lt.String);
                    break;
                case LocalizationMethod.RawText:
                    StringSerializer.WriteString(context, lt.String);
                    break;
                case LocalizationMethod.Parse:
                    StringSerializer.WriteString(context, lt.String);
                    break;
            }
        }

        public override void Serialize(BinaryWriter writer, object obj)
        {
            var localizedText = (LocalizedText)obj;
            writer.Write(localizedText.Id);
            writer.Write(localizedText.String);
            writer.Write((int)localizedText.Method);
        }

        public override object Deserialize(BinaryReader reader)
        {
            int id = reader.ReadInt32();
            string str = reader.ReadString();
            var method = (LocalizationMethod)reader.ReadInt32();

            var localizedText = new LocalizedText(id, str, method);
            return localizedText;
        }
    }
}
