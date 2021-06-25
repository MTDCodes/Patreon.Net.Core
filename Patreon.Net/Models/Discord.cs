using Newtonsoft.Json;

namespace Patreon.Net.Models
{
    public class Discord
    {
        [JsonProperty("url")]
        public object Url { get; set; }

        [JsonProperty("user_id")]
        public string UserId { get; set; }
    }
}