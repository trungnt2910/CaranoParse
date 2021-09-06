using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace TimetableApp.Core.Zoom
{
    [Serializable]
    public class ZoomCredentials : ICredentials
    {
        private static bool HasZoomProtocol = false;

        public string ID;
        public string Password;

        static ZoomCredentials()
        {
        }

        [Newtonsoft.Json.JsonIgnore]
        public string Type { get => "Zoom"; }

        [Newtonsoft.Json.JsonIgnore]
        public bool SupportsOneClick { get => HasZoomProtocol; }

        public override string ToString()
        {
            return $"ID: {ID}, Password: {Password}";
        }
    }
}
