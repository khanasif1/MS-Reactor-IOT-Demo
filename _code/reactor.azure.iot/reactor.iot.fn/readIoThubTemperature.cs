using IoTHubTrigger = Microsoft.Azure.WebJobs.EventHubTriggerAttribute;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventHubs;
using System.Text;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System;

namespace reactor.iot.fn
{
    public static class readIoThubTemperature
    {
        private static HttpClient client = new HttpClient();
        private static string _baseUrl = "http://localhost:61022";

        [FunctionName("readIoThubTemperature")]
        public static void Run([IoTHubTrigger("messages/events",
            Connection = "IoTHubTriggerConnection")]
            EventData message,
            ILogger log)
        {
            string msgString = Encoding.UTF8.GetString(message.Body.Array);
            iotHubMessage _iotMsg = JsonConvert.DeserializeObject<iotHubMessage>(msgString);
            _iotMsg.temperature = Math.Round(_iotMsg.temperature);
            if (_iotMsg.temperature != 0)
            {
                CallWebGaugeAPI(log, _iotMsg);
                log.LogInformation($"Temperature : {_iotMsg.temperature}");
            }
                
            
        }

        private static async Task CallWebGaugeAPI(ILogger log, iotHubMessage _iotMsg)
        {
            using (var _httpclient = new HttpClient())
            {
                HttpResponseMessage _response = await _httpclient.GetAsync($"{_baseUrl}/api/Values/{_iotMsg.temperature}");
                if (!_response.IsSuccessStatusCode)
                {
                    log.LogInformation($"API call has error : {_response.StatusCode}");
                }
            }
        }
    }
    public class iotHubMessage
    {
        public double temperature { get; set; }
    }

}