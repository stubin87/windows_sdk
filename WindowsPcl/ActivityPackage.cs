﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AdjustSdk.Pcl
{
    public class ActivityPackage
    {
        public ActivityKind ActivityKind { get; set; }

        public string ClientSdk { get; private set; }

        public Dictionary<string, string> Parameters { get; private set; }

        public string Path { get; private set; }

        public string Suffix { get; set; }

        private ActivityPackage()
        {
        }

        public ActivityPackage(ActivityKind activityKind, string clientSdk, Dictionary<string, string> parameters)
        {
            ActivityKind = activityKind;
            ClientSdk = clientSdk;
            Parameters = parameters;
            Path = ActivityKindUtil.GetPath(ActivityKind);
            Suffix = ActivityKindUtil.GetSuffix(Parameters);
        }

        public string FailureMessage()
        {
            return Util.f("Failed to track {0}{1}", ActivityKindUtil.ToString(ActivityKind), Suffix);
        }

        public override string ToString()
        {
            return Util.f("{0}{1}", ActivityKindUtil.ToString(ActivityKind), Suffix);
        }

        public string GetExtendedString()
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendFormat("Path:      {0}\n", Path);
            stringBuilder.AppendFormat("ClientSdk: {0}\n", ClientSdk);

            if (Parameters != null)
            {
                stringBuilder.AppendFormat("Parameters:");
                var sortedParameters = new SortedDictionary<string,string>(Parameters);
                foreach (var keyValuePair in sortedParameters)
                {
                    stringBuilder.AppendFormat("\n\t\t{0} {1}", keyValuePair.Key.PadRight(16, ' '), keyValuePair.Value);
                }
            }

            return stringBuilder.ToString();
        }

        #region Serialization

        // does not close stream received. Caller is responsible to close if it wants it
        internal static void SerializeToStream(Stream stream, ActivityPackage activityPackage)
        {
            var writer = new BinaryWriter(stream);

            writer.Write(activityPackage.Path);
            writer.Write("");
            writer.Write(activityPackage.ClientSdk);
            writer.Write(ActivityKindUtil.ToString(activityPackage.ActivityKind));
            writer.Write(activityPackage.Suffix);

            var parametersArray = activityPackage.Parameters.ToArray();
            writer.Write(parametersArray.Length);
            for (int i = 0; i < parametersArray.Length; i++)
            {
                writer.Write(parametersArray[i].Key);
                writer.Write(parametersArray[i].Value);
            }
        }

        // does not close stream received. Caller is responsible to close if it wants it
        internal static ActivityPackage DeserializeFromStream(Stream stream)
        {
            ActivityPackage activityPackage = null;
            var reader = new BinaryReader(stream);

            activityPackage = new ActivityPackage();
            activityPackage.Path = reader.ReadString();
            reader.ReadString(); //activityPackage.UserAgent
            activityPackage.ClientSdk = reader.ReadString();
            activityPackage.ActivityKind = ActivityKindUtil.FromString(reader.ReadString());
            activityPackage.Suffix = reader.ReadString();

            var parameterLength = reader.ReadInt32();
            activityPackage.Parameters = new Dictionary<string, string>(parameterLength);

            for (int i = 0; i < parameterLength; i++)
            {
                activityPackage.Parameters.Add(
                    reader.ReadString(),
                    reader.ReadString()
                );
            }

            return activityPackage;
        }

        // does not close stream received. Caller is responsible to close if it wants it
        internal static void SerializeListToStream(Stream stream, List<ActivityPackage> activityPackageList)
        {
            var writer = new BinaryWriter(stream);

            var activityPackageArray = activityPackageList.ToArray();
            writer.Write(activityPackageArray.Length);
            for (int i = 0; i < activityPackageArray.Length; i++)
            {
                ActivityPackage.SerializeToStream(stream, activityPackageArray[i]);
            }
        }

        // does not close stream received. Caller is responsible to close if it wants it
        internal static List<ActivityPackage> DeserializeListFromStream(Stream stream)
        {
            List<ActivityPackage> activityPackageList = null;
            var reader = new BinaryReader(stream);

            var activityPackageLength = reader.ReadInt32();
            activityPackageList = new List<ActivityPackage>(activityPackageLength);

            for (int i = 0; i < activityPackageLength; i++)
            {
                activityPackageList.Add(
                    ActivityPackage.DeserializeFromStream(stream)
                );
            }

            return activityPackageList;
        }

        #endregion Serialization
    }
}