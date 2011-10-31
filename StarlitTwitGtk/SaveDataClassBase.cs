using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Xml;

namespace StarlitTwitGtk
{
    /// <summary>
    /// xmlとして保存されるデータに必要なメソッドを集めたクラスです。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class SaveDataClassBase<T> where T : class, new()
    {
        /// <summary>
        /// 指定ファイル名でこのインスタンスを保存します。
        /// </summary>
        /// <param name="filePath"></param>
        public virtual void Save(string filePath)
        {
            SaveBase(filePath);
        }

        /// <summary>
        /// 指定ファイル名でこのインスタンスを保存します。
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        protected bool SaveBase(string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            try {
                using (StreamWriter writer = new StreamWriter(filePath)) {
                    serializer.Serialize(writer, this);
                }
            }
            catch (Exception) { return false;}
            return true;
        }

        /// <summary>
        /// 指定ファイルからインスタンスを復元します。
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static T Restore(string filePath)
        {
            if (File.Exists(filePath)) {
                XmlSerializer serializer = new XmlSerializer(typeof(T));
                try {
                    using (FileStream fs = File.OpenRead(filePath)) {
                        using (XmlReader reader = XmlReader.Create(fs)) {
                            if (serializer.CanDeserialize(reader)) {
                                fs.Seek(0, SeekOrigin.Begin);
                                return (T)serializer.Deserialize(fs);
                            }
                        }
                    }
                }
                catch (Exception) { }
            }
            return null;
        }
    }
}
