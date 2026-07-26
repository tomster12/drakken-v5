using System.IO;
using UnityEngine;

namespace Drakken.Config
{
    public static class NetworkConfigLoader
    {
        public static NetworkConfigFile Load()
        {
            var path = Path.Combine(Application.dataPath, "../network.json");

            if (!File.Exists(path))
            {
                var config = new NetworkConfigFile();

                File.WriteAllText(
                    path,
                    JsonUtility.ToJson(config, true));

                return config;
            }

            return JsonUtility.FromJson<NetworkConfigFile>(
                File.ReadAllText(path));
        }
    }
}
