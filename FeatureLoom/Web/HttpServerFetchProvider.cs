﻿using FeatureLoom.Collections;
using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Logging;
using FeatureLoom.MetaDatas;
using FeatureLoom.Time;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Web
{
    public class HttpServerFetchProvider : IMessageSink, IWebRequestHandler
    {
        private CountingRingBuffer<string> ringBuffer;
        private readonly string route;
        private IWebMessageTranslator translator;
        private readonly IWebServer webServer;
        public string Route => route;

        public HttpServerFetchProvider(string route, IWebMessageTranslator translator, int bufferSize = 100, IWebServer webServer = null)
        {
            if (!route.StartsWith("/")) route = "/" + route;
            route = route.TrimEnd("/");
            this.route = route;
            ringBuffer = new CountingRingBuffer<string>(bufferSize);
            this.translator = translator;
            this.webServer = webServer ?? SharedWebServer.WebServer;

            this.webServer.AddRequestHandler(this);
        }

        public void Post<M>(in M message)
        {
            if (translator.TryTranslate(message, out string json))
            {
                ringBuffer.Add(json);
            }
        }

        public void Post<M>(M message)
        {
            if (translator.TryTranslate(message, out string json))
            {
                ringBuffer.Add(json);
            }
        }

        public Task PostAsync<M>(M message)
        {
            Post(message);
            return Task.CompletedTask;
        }

        public async Task<bool> HandleRequestAsync(IWebRequest request, IWebResponse response)
        {
            if (!request.IsGet)
            {
                response.StatusCode = HttpStatusCode.MethodNotAllowed;
                await response.WriteAsync("Use 'GET' to fetch messages!");
                return false;
            }

            try
            {
                long requestedStart = 0;
                if (request.TryGetQueryItem("next", out string requestedStartStr)) long.TryParse(requestedStartStr, out requestedStart);
                int maxWait = 0;
                if (request.TryGetQueryItem("maxWait", out string maxWaitStr)) int.TryParse(maxWaitStr, out maxWait);
                long missed = 0;
                long next = 0;
                bool onlyLatest = false;
                string[] messages = Array.Empty<string>();

                next = ringBuffer.Counter;
                if (requestedStart > next || requestedStart < 0)
                {
                    onlyLatest = true;
                    requestedStart = next < 0 ? 0 : next - 1;
                }
                messages = ringBuffer.GetAvailableSince(requestedStart, out missed);

                if (messages.Length == 0 && maxWait > 0)
                {
                    if (await ringBuffer.WaitHandle.WaitAsync(maxWait.Milliseconds()))
                    {
                        messages = ringBuffer.GetAvailableSince(requestedStart, out missed);
                        next = ringBuffer.Counter;
                    }
                }

                if (onlyLatest) missed = -1;

                StringBuilder sb = new StringBuilder();
                sb.Append(
$@"{{
    ""missed"" : {missed},
    ""next"" : {next},
    ""messages"" : [
");
                for (int i = 0; i < messages.Length; i++)
                {
                    sb.Append(messages[i]);
                    if (i + 1 != messages.Length) sb.Append(",\n");
                }
                sb.Append($@"
                   ]
}}");
                await response.WriteAsync(sb.ToString());
                return true;
            }
            catch (Exception e)
            {
                Log.ERROR(this.GetHandle(), $"Failed while building response! Route:{route}", e.ToString());
                response.StatusCode = HttpStatusCode.InternalServerError;
                return true;
            }
        }
    }
}