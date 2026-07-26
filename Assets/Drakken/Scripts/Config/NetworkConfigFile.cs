using System;

namespace Drakken.Config
{

    [Serializable]
    public class NetworkConfigFile
    {
        public string address = "127.0.0.1";
        public ushort port = 7777;
    }
}
