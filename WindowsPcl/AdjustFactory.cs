﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AdjustSdk.Pcl
{
    public static class AdjustFactory
    {
        private static ILogger InternalLogger;
        private static IPackageHandler InternalPackageHandler;
        private static IRequestHandler InternalRequestHandler;
        private static HttpMessageHandler InternalHttpMessageHandler;

        public static ILogger Logger
        {
            get
            {
                // same instance of logger for all calls
                if (InternalLogger == null)
                    InternalLogger = new Logger();
                return InternalLogger;
            }

            set { InternalLogger = value; }
        }

        public static IPackageHandler GetPackageHandler(IActivityHandler activityHandler)
        {
            if (InternalPackageHandler == null)
                return new PackageHandler(activityHandler);
            else
                return InternalPackageHandler;
        }

        public static IRequestHandler GetRequestHandler(IPackageHandler packageHandler)
        {
            if (InternalRequestHandler == null)
                return new RequestHandler(packageHandler);
            else
                return InternalRequestHandler;
        }

        public static HttpMessageHandler GetHttpMessageHandler()
        {
            return InternalHttpMessageHandler;
        }

        public static void SetPackageHandler(IPackageHandler packageHandler)
        {
            InternalPackageHandler = packageHandler;
        }

        public static void SetRequestHandler(IRequestHandler requestHandler)
        {
            InternalRequestHandler = requestHandler;
        }

        public static void SetHttpMessageHandler(HttpMessageHandler httpMessageHandler)
        {
            InternalHttpMessageHandler = httpMessageHandler;
        }
    }
}