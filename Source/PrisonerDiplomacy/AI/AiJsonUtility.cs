using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PrisonerDiplomacy
{
    internal static class AiJsonUtility
    {
        public static bool TryDeserialize<T>(string json, out T value) where T : class
        {
            value = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                using (MemoryStream stream = new MemoryStream(bytes, false))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    value = serializer.ReadObject(stream) as T;
                    return value != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool TrySerialize<T>(T value, out string json) where T : class
        {
            json = null;
            if (value == null)
            {
                return false;
            }

            try
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    serializer.WriteObject(stream, value);
                    json = Encoding.UTF8.GetString(stream.ToArray());
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
