﻿using Newtonsoft.Json;
using PCLStorage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AdjustSdk.Pcl
{
    public static class Util
    {
        public const string BaseUrl = "https://app.adjust.com";
        private static ILogger Logger { get { return AdjustFactory.Logger; } }

        internal static string GetStringEncodedParameters(Dictionary<string, string> parameters)
        {
            if (parameters.Count == 0) return "";
            var firstPair = parameters.First();

            var stringBuilder = new StringBuilder(EncodedQueryParameter(firstPair, isFirstParameter: true));

            foreach (var pair in parameters.Skip(1))// skips the first "&"
            {
                stringBuilder.Append(EncodedQueryParameter(pair));
            }

            return stringBuilder.ToString();
        }

        public static bool DeleteFile(string filename)
        {
            try
            {
                var localStorage = FileSystem.Current.LocalStorage;
                var activityStateFile = localStorage.GetFileAsync(filename).Result;
                if (activityStateFile != null)
                {
                    activityStateFile.DeleteAsync().Wait();

                    //Logger.Debug("File {0} deleted", filename);
                    return true;
                }
                else
                {
                    //Logger.Debug("File {0} doesn't exist to delete", filename);
                    return false;
                }
            }
            catch (PCLStorage.Exceptions.FileNotFoundException)
            {
                //Logger.Debug("File {0} doesn't exist to delete", filename);
                return false;
            }
            catch (Exception)
            {
                //Logger.Error("Error deleting {0} file", filename);
                return false;
            }
        }

        public static string f(string message, params object[] parameters)
        {
            return String.Format(CultureInfo.InvariantCulture, message, parameters);
        }

        internal static bool IsFileNotFound(this Exception ex)
        {
            // check if the exception type is File Not Found (FNF)
            if (ex is PCLStorage.Exceptions.FileNotFoundException
                || ex is FileNotFoundException)
                return true;

            // if the exception is an aggregate of exceptions
            // we'll check each of them recursively if they are FNF
            var agg = ex as AggregateException;
            if (agg != null)
            {
                foreach (var innerEx in agg.InnerExceptions)
                {
                    if (innerEx.IsFileNotFound())
                        return true;
                }
            }

            // if the exception it's not FNF and doesn't have an inner exception
            // then it's not a FNF
            if (ex.InnerException == null)
                return false;

            // check recursively if the inner exception is FNF
            if (ex.InnerException.IsFileNotFound())
                return true;

            // if all checks fails, the exception must not be a FNF
            return false;
        }

        internal static async Task<T> DeserializeFromFileAsync<T>(string fileName,
            Func<Stream, T> ObjectReader,
            Func<T> defaultReturn,
            string objectName)
            where T : class
        {
            try
            {
                var localStorage = FileSystem.Current.LocalStorage;

                var activityStateFile = await localStorage.GetFileAsync(fileName);

                if (activityStateFile == null)
                {
                    throw new PCLStorage.Exceptions.FileNotFoundException(fileName);
                }

                T output;
                using (var stream = await activityStateFile.OpenAsync(FileAccess.Read))
                {
                    output = ObjectReader(stream);
                }
                Logger.Debug("Read {0}: {1}", objectName, output);

                // successful read
                return output;
            }
            catch (Exception ex)
            {
                if (ex.IsFileNotFound())
                {
                    Logger.Verbose("{0} file not found", objectName);
                }
                else
                {
                    Logger.Error("Failed to read file {0} ({1})", objectName, ex.Message);
                }
            }

            // fresh start
            return defaultReturn();
        }

        internal static async Task SerializeToFileAsync<T>(string fileName, Action<Stream, T> ObjectWriter, T input, string objectName)
            where T : class
        {
            try
            {
                var localStorage = FileSystem.Current.LocalStorage;
                var newActivityStateFile = await localStorage.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                using (var stream = await newActivityStateFile.OpenAsync(FileAccess.ReadAndWrite))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    ObjectWriter(stream, input);
                }
                Logger.Debug("Wrote {0}: {1}", objectName, input);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to write to file {0} ({1})", fileName, ex.Message);
            }
        }

        private static string EncodedQueryParameter(KeyValuePair<string, string> pair, bool isFirstParameter = false)
        {
            if (isFirstParameter)
                return Util.f("{0}={1}", Uri.EscapeDataString(pair.Key), Uri.EscapeDataString(pair.Value));
            else
                return Util.f("&{0}={1}", Uri.EscapeDataString(pair.Key), Uri.EscapeDataString(pair.Value));
        }

        internal static double SecondsFormat(this DateTime? date)
        {
            if (date == null)
                return -1;

            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            TimeSpan diff = date.Value.ToUniversalTime() - origin;
            return diff.TotalSeconds;
        }

        internal static double SecondsFormat(this TimeSpan? timeSpan)
        {
            if (timeSpan == null)
                return -1;
            else
                return timeSpan.Value.TotalSeconds;
        }

        internal static string Quote(this string input)
        {
            if (input == null || !input.Contains(" "))
                return input;

            return Util.f("'{0}'", input);
        }

        internal static string DateFormat(DateTime value)
        {
            var timeZone = value.ToString("zzz");
            var rfc822TimeZone = timeZone.Remove(3, 1);
            var sDTwOutTimeZone = value.ToString("yyyy-MM-ddTHH:mm:ss");
            var sDateTime = Util.f("{0}Z{1}", sDTwOutTimeZone, rfc822TimeZone);

            return sDateTime;
        }

        #region Serialization

        internal static Int64 SerializeTimeSpanToLong(TimeSpan? timeSpan)
        {
            if (timeSpan.HasValue)
                return timeSpan.Value.Ticks;
            else
                return -1;
        }

        internal static Int64 SerializeDatetimeToLong(DateTime? dateTime)
        {
            if (dateTime.HasValue)
                return dateTime.Value.Ticks;
            else
                return -1;
        }

        internal static TimeSpan? DeserializeTimeSpanFromLong(Int64 ticks)
        {
            if (ticks == -1)
                return null;
            else
                return new TimeSpan(ticks);
        }

        internal static DateTime? DeserializeDateTimeFromLong(Int64 ticks)
        {
            if (ticks == -1)
                return null;
            else
                return new DateTime(ticks);
        }

        internal static T TryRead<T>(Func<T> readStream, Func<T> defaultValue)
        {
            T result;
            try
            {
                result = readStream();
            }
            catch (EndOfStreamException)
            {
                result = defaultValue();
            }

            return result;
        }

        #endregion Serialization

        internal static Dictionary<string, string> ParseJsonResponse(HttpResponseMessage httpResponseMessage)
        {
            if (httpResponseMessage == null) { return null; }

            using (var content = httpResponseMessage.Content)
            {
                var sResponse = content.ReadAsStringAsync().Result;
                return BuildJsonDict(sResponse, httpResponseMessage.IsSuccessStatusCode);
            }
        }

        internal static Dictionary<string, string> ParseJsonExceptionResponse(HttpWebResponse httpWebResponse)
        {
            if (httpWebResponse == null) { return null; }

            using (var streamResponse = httpWebResponse.GetResponseStream())
            using (var streamReader = new StreamReader(streamResponse))
            {
                var sResponse = streamReader.ReadToEnd();
                return BuildJsonDict(sResponse, false);
            }
        }

        internal static string GetDictionaryValue(Dictionary<string, string> dic, string key)
        {
            string value;
            if (!dic.TryGetValue(key, out value))
            {
                return null;
            }
            return value;
        }

        internal static Dictionary<string, string> BuildJsonDict(string sResponse, bool IsSuccessStatusCode)
        {
            Logger.Verbose("Response: {0}", sResponse);

            if (sResponse == null) { return null; }

            Dictionary<string, string> jsonDic = null;
            try
            {
                jsonDic = JsonConvert.DeserializeObject<Dictionary<string, string>>(sResponse);
            }
            catch (Exception e)
            {
                Logger.Error("Failed to parse json response ({0})", e.Message);
            }

            if (jsonDic == null) { return null; }

            string message;
            if (!jsonDic.TryGetValue("message", out message))
            {
                message = "No message found";
            }

            if (IsSuccessStatusCode)
            {
                Logger.Info("{0}", message);
            }
            else
            {
                Logger.Error("{0}", message);
            }

            return jsonDic;
        }

        internal static HttpClient BuildHttpClient(string clientSdk)
        {
            var httpClient = new HttpClient(AdjustFactory.GetHttpMessageHandler());

            httpClient.Timeout = new TimeSpan(0, 1, 0);
            httpClient.DefaultRequestHeaders.Add("Client-SDK", clientSdk);

            return httpClient;
        }
    }
}