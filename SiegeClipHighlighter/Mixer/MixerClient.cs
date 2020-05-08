using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
namespace SiegeClipHighlighter.Mixer
{
    class MixerClient
    {
        public static HttpClient httpClient;
        static MixerClient() {
            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("https://mixer.com/api/v1/");
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Gets the recordings of a channel
        /// </summary>
        /// <param name="channelId"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<Recording>> GetRecordingsAsync(uint channelId)
        {
            var response = await httpClient.GetAsync($"recordings?where=channelId:eq:{channelId}");
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IReadOnlyList<Recording>>(json);
        }

        /// <summary>
        /// Gets the recordings of a channel
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="game">The Id of the game</param>
        /// <param name="lastCheckVod">The date to check</param>
        /// <returns></returns>
        public async Task<IReadOnlyList<Recording>> GetRecordingsAsync(uint channelId, uint? game, DateTime? lastCheckVod = null)
        {
            var timeCondition = lastCheckVod.HasValue ? ",createdAt:gt:" + lastCheckVod.Value.ToString("s") + "z" : "";
            var gameCondition = game.HasValue ? ",typeId:eq:" + game.Value : "";

            var response = await httpClient.GetAsync($"recordings?where=channelId:eq:{channelId}{gameCondition}{timeCondition}");
            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IReadOnlyList<Recording>>(json);
        }
    }
}
